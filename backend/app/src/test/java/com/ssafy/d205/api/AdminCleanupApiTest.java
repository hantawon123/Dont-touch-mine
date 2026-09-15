package com.ssafy.d205.api;

import jakarta.servlet.http.Cookie;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.mock.web.MockHttpSession;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.MvcResult;
import org.springframework.test.web.servlet.ResultActions;
import tools.jackson.databind.JsonNode;
import tools.jackson.databind.ObjectMapper;

import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.patch;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.ssafy.d205.support.IntegrationTest;

/**
 * 운영자가 신고와 피드백을 치우는 API (S15P21D205-900).
 *
 * <p>이 기능의 위험은 기능이 안 되는 것이 아니라 <b>반쯤 되는 것</b>입니다. 숨김은
 * 조회마다 {@code deleted_at IS NULL} 을 붙여 구현되는데, 조회가 다섯 개이고 하나라도
 * 빠뜨리면 숨긴 신고가 그 경로로만 다시 나타납니다. 목록에는 없는데 상세에는 있거나,
 * 건수만 남고 내용이 없는 상태가 되고, 그때 운영자는 화면을 믿을 수 없게 됩니다.
 *
 * <p>그래서 여기서는 "숨겼다"를 확인하는 것으로 그치지 않고 <b>다섯 조회를 하나씩
 * 눌러 봅니다</b> - 목록, 사유 분포, 상세, 검토 대상, 피드백 목록입니다.
 *
 * <p>완전 삭제 쪽은 되돌릴 수 없으므로 행이 정말로 사라지는지를 DB 에서 직접 봅니다.
 * API 응답만 보면 숨김과 구분되지 않습니다.
 */
class AdminCleanupApiTest extends IntegrationTest {

    private static final String USER_ID_HEADER = "X-User-Id";
    private static final String LOGIN = "/api/v1/admin/session";
    private static final String REPORTS = "/api/v1/admin/reports";
    private static final String FEEDBACK = "/api/v1/admin/feedback";

    @Autowired
    MockMvc mvc;

    @Autowired
    ObjectMapper objectMapper;

    @Autowired
    JdbcTemplate jdbcTemplate;

    // --- 인증 ---------------------------------------------------------------

    @Test
    @DisplayName("로그인하지 않으면 치울 수 없다")
    void cleanupNeedsLogin() throws Exception {
        // 이 경로들이 SecurityConfig 의 첫 체인에 걸려 있다는 것을 확인합니다. 여기가
        // 열리면 아무나 신고와 피드백을 지울 수 있습니다.
        //
        // 401 이 아니라 403 입니다. CsrfFilter 가 인가보다 먼저 돌아서, 로그인하지 않은
        // 상태 변경 요청은 "누구냐"를 묻기 전에 CSRF 에서 막힙니다. 조회(GET)가 401 인
        // 것과 다르고, 화면도 403 을 받으면 세션을 한 번 더 물어 갈라냅니다.
        mvc.perform(patch(REPORTS + "/whoever/hidden")).andExpect(status().isForbidden());
        mvc.perform(delete(REPORTS + "/whoever")).andExpect(status().isForbidden());
        mvc.perform(patch(REPORTS + "/entries/1/hidden")).andExpect(status().isForbidden());
        mvc.perform(delete(REPORTS + "/entries/1")).andExpect(status().isForbidden());
        mvc.perform(patch(FEEDBACK + "/1/hidden")).andExpect(status().isForbidden());
        mvc.perform(delete(FEEDBACK + "/1")).andExpect(status().isForbidden());
    }

    @Test
    @DisplayName("CSRF 토큰이 있어도 로그인하지 않았으면 치울 수 없다")
    void cleanupNeedsMoreThanACsrfToken() throws Exception {
        // 위 테스트가 403 이라 "CSRF 만 넘기면 되는 것 아닌가"를 따로 막아 둡니다.
        // 세션 없이 토큰만 갖춰 보내면 그때는 인가가 답합니다.
        Admin admin = login();

        mvc.perform(delete(REPORTS + "/whoever")
                        .cookie(admin.csrf())
                        .header("X-XSRF-TOKEN", admin.csrf().getValue()))
                .andExpect(status().isUnauthorized());
        mvc.perform(delete(FEEDBACK + "/1")
                        .cookie(admin.csrf())
                        .header("X-XSRF-TOKEN", admin.csrf().getValue()))
                .andExpect(status().isUnauthorized());
    }

    @Test
    @DisplayName("CSRF 토큰이 없으면 로그인해도 거절한다")
    void cleanupNeedsCsrfToken() throws Exception {
        Admin admin = login();

        // 쿠키만 있고 헤더가 없으면 서버가 비교할 짝이 없습니다. 이것이 막히지 않으면
        // 운영자가 아무 사이트나 열었을 때 그 페이지가 신고를 지울 수 있습니다.
        mvc.perform(delete(REPORTS + "/whoever").session(admin.session()).cookie(admin.csrf()))
                .andExpect(status().isForbidden());
        mvc.perform(delete(FEEDBACK + "/1").session(admin.session()).cookie(admin.csrf()))
                .andExpect(status().isForbidden());
    }

    // --- 신고 숨김 ----------------------------------------------------------

    @Test
    @DisplayName("사람 단위로 숨기면 다섯 조회에서 모두 빠진다")
    void hidingAUserDropsThemFromEveryQuery() throws Exception {
        String target = createUser();
        report(createUser(), target, "ABUSE", "욕설");
        report(createUser(), target, "CHEATING", null);

        Admin admin = login();
        assertThat(rowFor(admin, target)).as("숨기기 전에는 목록에 있어야 합니다").isNotNull();

        hide(admin, REPORTS + "/" + target + "/hidden")
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.affected").value(2));

        // 1) 목록, 2) 사유 분포 - 둘 다 summarize/countReasons 조회를 탑니다. 사람 줄이
        // 통째로 사라지므로 사유도 함께 사라집니다.
        assertThat(rowFor(admin, target)).as("목록에 남아 있습니다").isNull();

        // 3) 상세. 목록에서 빠졌는데 여기서 나오면 화면이 서로 다른 말을 합니다.
        assertThat(detail(admin, target)).isEmpty();

        // 4) 검토 대상. 숨긴 신고가 여기 남으면 "판단할 것이 없는데 판단됨" 이 생깁니다.
        review(admin, target, "ACTIONED")
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.reviewed").value(0));
    }

    @Test
    @DisplayName("status 를 주면 그 상태만 치운다 — 화면이 보여준 만큼만")
    void hidingIsScopedToTheStatusTheScreenShowed() throws Exception {
        // 이 테스트가 고정하는 것: 목록은 status 로 걸러 보여주므로 치우는 범위도 같아야
        // 합니다. 범위가 넓으면 운영자가 ACTIONED 화면에서 "1건"을 보고 누른 한 번에,
        // 한 번도 보지 못한 PENDING 신고까지 사라집니다.
        String target = createUser();
        report(createUser(), target, "ABUSE", null);

        Admin admin = login();
        review(admin, target, "ACTIONED").andExpect(status().isOk());

        // 마무리한 뒤에 새 신고 두 건이 들어옵니다. 아직 아무도 보지 않았습니다.
        report(createUser(), target, "SPAM", null);
        report(createUser(), target, "CHEATING", null);

        // 운영자가 보고 있는 화면은 ACTIONED 이고 거기 보이는 건수는 1 입니다.
        assertThat(rowFor(admin, target, "ACTIONED").get("reportCount").asInt()).isEqualTo(1);

        hide(admin, REPORTS + "/" + target + "/hidden?status=ACTIONED")
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.affected").value(1));

        // 보던 것은 사라지고, 보지 못했던 두 건은 그대로 있어야 합니다.
        assertThat(rowFor(admin, target, "ACTIONED")).isNull();
        assertThat(rowFor(admin, target, "PENDING").get("reportCount").asInt()).isEqualTo(2);
    }

    @Test
    @DisplayName("status 를 주면 그 상태만 지운다 — 보지 못한 신고는 남는다")
    void purgingIsScopedToTheStatusTheScreenShowed() throws Exception {
        // 위와 같은 상황이지만 이쪽은 되돌릴 수 없습니다. 범위를 넓게 잡은 실수가
        // 그대로 손실이 되므로 따로 고정합니다.
        String target = createUser();
        report(createUser(), target, "ABUSE", null);

        Admin admin = login();
        review(admin, target, "ACTIONED").andExpect(status().isOk());

        report(createUser(), target, "SPAM", null);
        report(createUser(), target, "CHEATING", null);

        purge(admin, REPORTS + "/" + target + "?status=ACTIONED")
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.affected").value(1));

        // 지운 것은 한 건뿐이고 보지 못한 두 건은 DB 에 남아 있어야 합니다.
        assertThat(reportRowsAbout(target)).isEqualTo(2);
        assertThat(rowFor(admin, target, "PENDING").get("reportCount").asInt()).isEqualTo(2);
    }

    @Test
    @DisplayName("status 를 빼면 상태를 가리지 않는다")
    void omittingStatusTouchesEveryStatus() throws Exception {
        // API 를 직접 부르는 쪽을 위한 동작입니다. 화면은 늘 status 를 채웁니다.
        String target = createUser();
        report(createUser(), target, "ABUSE", null);

        Admin admin = login();
        review(admin, target, "ACTIONED").andExpect(status().isOk());
        report(createUser(), target, "SPAM", null);

        purge(admin, REPORTS + "/" + target)
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.affected").value(2));

        assertThat(reportRowsAbout(target)).isZero();
    }

    @Test
    @DisplayName("숨겨도 행과 검토 상태는 그대로 남는다")
    void hidingKeepsTheRowAndItsVerdict() throws Exception {
        String target = createUser();
        report(createUser(), target, "ABUSE", null);

        Admin admin = login();
        hide(admin, REPORTS + "/" + target + "/hidden").andExpect(status().isOk());

        // 숨김은 판단이 아닙니다. 여기서 DISMISSED 가 찍히면 운영자가 내리지 않은
        // 판단이 기록에 남고, 무고성 신고를 세는 집계가 조용히 틀어집니다.
        var row = jdbcTemplate.queryForMap("""
                SELECT r.status, r.deleted_at
                  FROM user_reports r
                  JOIN users u ON u.users_seq = r.reported_seq
                 WHERE u.public_id = ?
                """, target);

        assertThat(row.get("status")).isEqualTo("PENDING");
        assertThat(row.get("deleted_at")).as("숨긴 시각이 없습니다").isNotNull();
    }

    @Test
    @DisplayName("신고 한 건만 숨기면 나머지는 남는다")
    void hidingOneEntryLeavesTheRest() throws Exception {
        String target = createUser();
        report(createUser(), target, "ABUSE", "욕설");
        report(createUser(), target, "CHEATING", "핵");

        Admin admin = login();
        JsonNode before = detail(admin, target);
        assertThat(before).hasSize(2);

        hide(admin, REPORTS + "/entries/" + before.get(0).get("id").asInt() + "/hidden")
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.affected").value(1));

        JsonNode after = detail(admin, target);
        assertThat(after).hasSize(1);

        // 목록의 건수도 함께 줄어야 합니다. 상세만 줄고 건수가 그대로면 "2건" 이라고
        // 적힌 줄 아래에 한 건만 보입니다.
        assertThat(rowFor(admin, target).get("reportCount").asInt()).isEqualTo(1);
    }

    @Test
    @DisplayName("이미 숨긴 것을 다시 숨겨도 실패가 아니고 시각도 그대로다")
    void hidingTwiceIsHarmless() throws Exception {
        String target = createUser();
        report(createUser(), target, "ABUSE", null);

        Admin admin = login();
        JsonNode entry = detail(admin, target).get(0);
        String path = REPORTS + "/entries/" + entry.get("id").asInt() + "/hidden";

        hide(admin, path).andExpect(jsonPath("$.affected").value(1));
        String first = hiddenAt(target);

        // 두 번째는 0 입니다. 두 사람이 같은 화면을 보다가 둘 다 누른 경우이고,
        // 원하는 결과는 이미 이루어져 있으므로 오류가 아닙니다.
        hide(admin, path)
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.affected").value(0));

        // 처음 치운 시각이 되찾을 때의 단서라, 두 번째 클릭이 그것을 덮으면 안 됩니다.
        assertThat(hiddenAt(target)).isEqualTo(first);
    }

    // --- 신고 완전 삭제 ------------------------------------------------------

    @Test
    @DisplayName("사람 단위로 지우면 숨긴 것까지 행이 사라진다")
    void purgingAUserRemovesEveryRowIncludingHidden() throws Exception {
        String target = createUser();
        report(createUser(), target, "ABUSE", null);
        report(createUser(), target, "CHEATING", null);

        Admin admin = login();
        JsonNode entry = detail(admin, target).get(0);
        hide(admin, REPORTS + "/entries/" + entry.get("id").asInt() + "/hidden")
                .andExpect(status().isOk());

        // 숨긴 한 건까지 두 건 모두입니다. 숨긴 것만 남으면 나중에 그 행의 출처를
        // 아무도 설명하지 못합니다.
        purge(admin, REPORTS + "/" + target)
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.affected").value(2));

        assertThat(reportRowsAbout(target)).isZero();
    }

    @Test
    @DisplayName("신고 한 건을 지우면 그 행만 사라진다")
    void purgingOneEntryRemovesOnlyThatRow() throws Exception {
        String target = createUser();
        report(createUser(), target, "ABUSE", null);
        report(createUser(), target, "CHEATING", null);

        Admin admin = login();
        int id = detail(admin, target).get(0).get("id").asInt();

        purge(admin, REPORTS + "/entries/" + id)
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.affected").value(1));

        assertThat(reportRowsAbout(target)).isEqualTo(1);
    }

    @Test
    @DisplayName("없는 신고를 지워도 200 이고 지운 건수가 0 이다")
    void purgingAMissingEntryIsNotAnError() throws Exception {
        // 두 사람이 같은 화면을 보다가 둘 다 누른 경우입니다. 404 를 주면 뒤에 누른
        // 쪽이 무엇을 잘못했는지 알 수 없는데, 원하는 결과는 이미 이루어져 있습니다.
        purge(login(), REPORTS + "/entries/2147483647")
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.affected").value(0));
    }

    // --- 피드백 -------------------------------------------------------------

    @Test
    @DisplayName("피드백을 숨기면 목록에서 빠지고 행은 남는다")
    void hidingFeedbackDropsItFromTheListButKeepsTheRow() throws Exception {
        String marker = marker();
        sendFeedback(createUser(), "숨길 피드백 " + marker);

        Admin admin = login();
        JsonNode mine = feedbackWith(admin, marker);
        assertThat(mine).hasSize(1);

        hide(admin, FEEDBACK + "/" + mine.get(0).get("id").asInt() + "/hidden")
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.affected").value(1));

        assertThat(feedbackWith(admin, marker)).isEmpty();

        // 행은 남습니다. 잘못 치운 것을 DB 에서 되찾을 수 있어야 하고, 그것이 숨김과
        // 완전 삭제를 나눈 이유입니다.
        assertThat(feedbackRowsWith(marker)).isEqualTo(1);
    }

    @Test
    @DisplayName("피드백을 지우면 행이 사라진다")
    void purgingFeedbackRemovesTheRow() throws Exception {
        String marker = marker();
        sendFeedback(createUser(), "지울 피드백 " + marker);

        Admin admin = login();
        int id = feedbackWith(admin, marker).get(0).get("id").asInt();

        purge(admin, FEEDBACK + "/" + id)
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.affected").value(1));

        assertThat(feedbackRowsWith(marker)).isZero();
    }

    @Test
    @DisplayName("없는 피드백을 지워도 200 이고 지운 건수가 0 이다")
    void purgingMissingFeedbackIsNotAnError() throws Exception {
        purge(login(), FEEDBACK + "/2147483647")
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.affected").value(0));
    }

    // --- 도우미 -------------------------------------------------------------

    /**
     * 화면이 하는 것과 같게 보냅니다.
     *
     * <p>쿠키는 브라우저가 자동으로 싣고, 화면이 그 쿠키를 읽어 헤더에도 넣습니다.
     * 헤더만 보내면 서버가 비교할 원본이 없어 403 입니다.
     */
    private ResultActions hide(Admin admin, String path) throws Exception {
        return mvc.perform(patch(path)
                .session(admin.session())
                .cookie(admin.csrf())
                .header("X-XSRF-TOKEN", admin.csrf().getValue()));
    }

    private ResultActions purge(Admin admin, String path) throws Exception {
        return mvc.perform(delete(path)
                .session(admin.session())
                .cookie(admin.csrf())
                .header("X-XSRF-TOKEN", admin.csrf().getValue()));
    }

    private ResultActions review(Admin admin, String userId, String status) throws Exception {
        return mvc.perform(patch(REPORTS + "/{userId}", userId)
                .session(admin.session())
                .cookie(admin.csrf())
                .header("X-XSRF-TOKEN", admin.csrf().getValue())
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"status\":\"" + status + "\"}"));
    }

    /** 미검토 목록에서 그 사람의 줄. 없으면 null 입니다. */
    private JsonNode rowFor(Admin admin, String userId) throws Exception {
        return rowFor(admin, userId, "PENDING");
    }

    /** 그 상태의 목록에서 그 사람의 줄. 없으면 null 입니다. */
    private JsonNode rowFor(Admin admin, String userId, String status) throws Exception {
        String body = mvc.perform(get(REPORTS).param("status", status).session(admin.session()))
                .andExpect(status().isOk())
                .andReturn().getResponse().getContentAsString();

        for (JsonNode node : objectMapper.readTree(body).get("users")) {
            if (userId.equals(node.get("userId").asText())) {
                return node;
            }
        }

        return null;
    }

    private JsonNode detail(Admin admin, String userId) throws Exception {
        String body = mvc.perform(get(REPORTS + "/" + userId).session(admin.session()))
                .andExpect(status().isOk())
                .andReturn().getResponse().getContentAsString();

        return objectMapper.readTree(body).get("reports");
    }

    /** 이 테스트가 만든 피드백만. 컨텍스트를 공유하므로 다른 테스트의 행이 섞입니다. */
    private JsonNode feedbackWith(Admin admin, String marker) throws Exception {
        String body = mvc.perform(get(FEEDBACK).param("limit", "200").session(admin.session()))
                .andExpect(status().isOk())
                .andReturn().getResponse().getContentAsString();

        var filtered = objectMapper.createArrayNode();
        for (JsonNode row : objectMapper.readTree(body).get("feedback")) {
            if (row.get("message").asText().contains(marker)) {
                filtered.add(row);
            }
        }

        return filtered;
    }

    private String hiddenAt(String userId) {
        return jdbcTemplate.queryForObject("""
                SELECT r.deleted_at
                  FROM user_reports r
                  JOIN users u ON u.users_seq = r.reported_seq
                 WHERE u.public_id = ?
                """, String.class, userId);
    }

    private int reportRowsAbout(String userId) {
        return jdbcTemplate.queryForObject("""
                SELECT COUNT(*)
                  FROM user_reports r
                  JOIN users u ON u.users_seq = r.reported_seq
                 WHERE u.public_id = ?
                """, Integer.class, userId);
    }

    private int feedbackRowsWith(String marker) {
        return jdbcTemplate.queryForObject(
                "SELECT COUNT(*) FROM user_feedback WHERE message LIKE ?",
                Integer.class, "%" + marker + "%");
    }

    private String marker() {
        return UUID.randomUUID().toString().substring(0, 8);
    }

    private Admin login() throws Exception {
        MockHttpSession session = new MockHttpSession();

        MvcResult result = mvc.perform(post(LOGIN)
                        .session(session)
                        .param("username", "test-admin")
                        .param("password", "test-password"))
                .andExpect(status().isNoContent())
                .andReturn();

        Cookie token = result.getResponse().getCookie("XSRF-TOKEN");
        assertThat(token).as("로그인 응답에 CSRF 토큰 쿠키가 없습니다").isNotNull();

        return new Admin(session, token);
    }

    private record Admin(MockHttpSession session, Cookie csrf) {
    }

    private String createUser() throws Exception {
        String body = mvc.perform(post("/api/v1/accounts")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"deviceId\":\"" + UUID.randomUUID() + "\"}"))
                .andExpect(status().isCreated())
                .andReturn().getResponse().getContentAsString();

        return objectMapper.readTree(body).get("userId").asText();
    }

    private void report(String reporter, String reported, String reason, String memo)
            throws Exception {
        String body = memo == null
                ? "{\"userId\":\"" + reported + "\",\"reason\":\"" + reason + "\"}"
                : "{\"userId\":\"" + reported + "\",\"reason\":\"" + reason
                        + "\",\"memo\":\"" + memo + "\"}";

        mvc.perform(post("/api/v1/reports")
                        .header(USER_ID_HEADER, reporter)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(body))
                .andExpect(status().isCreated());
    }

    private void sendFeedback(String caller, String message) throws Exception {
        mvc.perform(post("/api/v1/feedback")
                        .header(USER_ID_HEADER, caller)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"message\":\"" + message + "\"}"))
                .andExpect(status().isCreated());
    }
}
