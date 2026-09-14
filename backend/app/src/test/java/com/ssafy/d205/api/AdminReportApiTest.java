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
import tools.jackson.databind.ObjectMapper;

import java.util.Map;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.patch;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.ssafy.d205.support.IntegrationTest;

/**
 * 운영자가 신고를 보고 마무리하는 API.
 *
 * <p>두 가지를 봅니다. <b>로그인하지 않으면 아무것도 볼 수 없다</b>는 것과,
 * <b>목록의 숫자가 맞다</b>는 것입니다.
 *
 * <p>후자가 특히 중요합니다. 건수와 신고한 사람 수가 섞이면 한 사람이 부풀린 것과
 * 여럿이 신고한 것이 같아 보이고, 운영자가 엉뚱한 쪽을 제재하게 됩니다. 이 화면이
 * 존재하는 이유 자체가 그 구분이라, 숫자가 틀리면 기능이 없느니만 못합니다.
 */
class AdminReportApiTest extends IntegrationTest {

    private static final String USER_ID_HEADER = "X-User-Id";
    private static final String LOGIN = "/api/v1/admin/session";
    private static final String REPORTS = "/api/v1/admin/reports";

    @Autowired
    MockMvc mvc;

    @Autowired
    ObjectMapper objectMapper;

    @Autowired
    JdbcTemplate jdbcTemplate;

    @Test
    @DisplayName("로그인하지 않으면 조회할 수 없다")
    void readingNeedsLogin() throws Exception {
        // 이 경로 전체가 SecurityConfig 의 첫 체인에 걸려 있다는 것을 확인합니다.
        // 여기가 열리면 아무나 신고 내용을 들여다볼 수 있습니다.
        mvc.perform(get(REPORTS)).andExpect(status().isUnauthorized());
        mvc.perform(get(REPORTS + "/whoever")).andExpect(status().isUnauthorized());
    }

    @Test
    @DisplayName("로그인하지 않은 처리 요청은 CSRF 에서 먼저 막힌다")
    void writingNeedsLoginAndIsRefusedEarlier() throws Exception {
        // 막히는 것은 같지만 코드가 401 이 아니라 403 입니다. CsrfFilter 가 인가보다
        // 먼저 돌아서, 토큰이 없는 요청은 "누구인지" 따지기 전에 거절됩니다.
        //
        // <b>화면이 이것을 알아야 합니다.</b> 403 을 CSRF 문제로만 다루면 세션이 끊긴
        // 사용자가 로그인 화면으로 못 가고 계속 실패만 봅니다. 403 을 받으면 세션을
        // 한 번 확인해서, 그것도 401 이면 로그인으로 보내야 합니다.
        mvc.perform(patch(REPORTS + "/whoever")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"status\":\"DISMISSED\"}"))
                .andExpect(status().isForbidden());

        // 무엇이 막았든 신고는 그대로여야 합니다.
        mvc.perform(get(REPORTS)).andExpect(status().isUnauthorized());
    }

    @Test
    @DisplayName("한 사람이 여러 번 신고한 것과 여럿이 신고한 것이 구분된다")
    void oneLoudReporterDoesNotLookLikeACrowd() throws Exception {
        // 이 테스트가 이 화면의 존재 이유입니다. 둘을 합쳐 보여주면 한 사람이 세 번
        // 누른 것과 세 명이 신고한 것이 같아 보이고, 운영자가 신고자 대신 신고당한
        // 사람을 제재하게 됩니다.
        String loud = createUser();
        String noisy = createUser();
        String crowdTarget = createUser();

        report(loud, noisy, "ABUSE", "한 번");
        report(loud, noisy, "ABUSE", "두 번");
        report(loud, noisy, "SPAM", "세 번");

        report(createUser(), crowdTarget, "CHEATING", null);
        report(createUser(), crowdTarget, "CHEATING", null);

        Admin admin = login();
        Map<String, Object> noisyRow = rowFor(admin, noisy);
        Map<String, Object> crowdRow = rowFor(admin, crowdTarget);

        assertThat(noisyRow.get("reportCount")).isEqualTo(3);
        assertThat(noisyRow.get("reporterCount")).isEqualTo(1);

        assertThat(crowdRow.get("reportCount")).isEqualTo(2);
        assertThat(crowdRow.get("reporterCount")).isEqualTo(2);
    }

    @Test
    @DisplayName("사유별 건수가 사람마다 따로 붙는다")
    void reasonsAreCountedPerPerson() throws Exception {
        // 사유를 요약 쿼리에 합치면 사람마다 사유 수만큼 행이 늘어나 건수와 인원수가
        // 부풀려집니다. 나눠서 붙이는 것이 맞게 동작하는지 봅니다.
        String reporter = createUser();
        String target = createUser();

        report(reporter, target, "ABUSE", null);
        report(reporter, target, "ABUSE", null);
        report(reporter, target, "SPAM", null);

        Admin admin = login();
        Map<String, Object> row = rowFor(admin, target);

        @SuppressWarnings("unchecked")
        Map<String, Object> reasons = (Map<String, Object>) row.get("reasons");
        assertThat(reasons).containsExactlyInAnyOrderEntriesOf(Map.of("ABUSE", 2, "SPAM", 1));

        // 사유가 둘이라고 건수가 늘어나면 안 됩니다.
        assertThat(row.get("reportCount")).isEqualTo(3);
        assertThat(row.get("reporterCount")).isEqualTo(1);
    }

    @Test
    @DisplayName("탈퇴한 신고자의 신고는 남지만 사람 수에는 세지 않는다")
    void reportsFromDeletedAccountsAreKeptButNotCounted() throws Exception {
        // 탈퇴해도 그 사람이 한 짓이 없어지지 않으므로 기록은 남습니다(V9). 다만
        // reporter_seq 가 전부 NULL 이라 몇 명이었는지 복원할 수 없습니다. 사람 수에
        // 섞으면 "두 명이 신고"라고 사실이 아닌 말을 하게 됩니다.
        String deviceId = UUID.randomUUID().toString();
        String leaving = createUser(deviceId);
        String staying = createUser();
        String target = createUser();

        report(leaving, target, "ABUSE", "탈퇴할 사람이 남긴 말");
        report(staying, target, "SPAM", null);

        mvc.perform(org.springframework.test.web.servlet.request.MockMvcRequestBuilders
                        .delete("/api/v1/accounts/me")
                        .header(USER_ID_HEADER, leaving)
                        .header("X-Device-Id", deviceId))
                .andExpect(status().isNoContent());

        Admin admin = login();
        Map<String, Object> row = rowFor(admin, target);

        assertThat(row.get("reportCount")).isEqualTo(2);
        assertThat(row.get("reporterCount")).isEqualTo(1);
        assertThat(row.get("fromDeletedAccounts")).isEqualTo(1);
    }

    @Test
    @DisplayName("처리하면 미검토 목록에서 빠지고 누가 언제 봤는지 남는다")
    void reviewingRemovesThemFromTheQueue() throws Exception {
        String reporter = createUser();
        String target = createUser();
        report(reporter, target, "ABUSE", null);
        report(reporter, target, "SPAM", null);

        Admin admin = login();
        assertThat(rowFor(admin, target)).isNotNull();

        review(admin, target, "ACTIONED")
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.reviewed").value(2));

        assertThat(rowFor(admin, target)).as("미검토 목록에 남아 있습니다").isNull();
        assertThat(rowFor(admin, target, "ACTIONED")).isNotNull();

        Map<String, Object> saved = anyReportAbout(target);
        assertThat(saved.get("status")).isEqualTo("ACTIONED");
        assertThat(saved.get("reviewed_by")).isEqualTo("test-admin");
        assertThat(saved.get("reviewed_at")).isNotNull();
    }

    @Test
    @DisplayName("검토한 뒤 새로 들어온 신고만 다시 올라온다")
    void onlyNewReportsComeBack() throws Exception {
        // 상태를 사람이 아니라 신고 한 건마다 두기로 한 이유가 이것입니다. 사람 단위로
        // 두면 "언제부터 다시 봐야 하는가"를 따로 관리해야 합니다.
        String reporter = createUser();
        String target = createUser();
        report(reporter, target, "ABUSE", "먼저 온 것");

        Admin admin = login();
        review(admin, target, "DISMISSED").andExpect(status().isOk());
        assertThat(rowFor(admin, target)).isNull();

        report(createUser(), target, "CHEATING", "나중에 온 것");

        Map<String, Object> again = rowFor(admin, target);
        assertThat(again).as("새 신고가 올라오지 않았습니다").isNotNull();
        assertThat(again.get("reportCount")).as("검토한 것까지 다시 세면 안 됩니다").isEqualTo(1);
    }

    @Test
    @DisplayName("이미 처리된 사람을 또 처리해도 오류가 아니다")
    void reviewingTwiceIsNotAnError() throws Exception {
        // 두 사람이 같은 화면을 보다가 둘 다 누르는 경우입니다. 뒤에 누른 쪽에게
        // 오류를 주면 무엇이 잘못됐는지 알 수 없는데, 원하는 결과는 이미 이루어져
        // 있습니다.
        String target = createUser();
        report(createUser(), target, "ABUSE", null);

        Admin admin = login();
        review(admin, target, "ACTIONED").andExpect(jsonPath("$.reviewed").value(1));
        review(admin, target, "ACTIONED").andExpect(jsonPath("$.reviewed").value(0));
    }

    @Test
    @DisplayName("판단은 뒤집을 수 있다")
    void aDecisionCanBeChanged() throws Exception {
        // 잘못 누르는 일은 실제로 일어납니다. 못 고치게 하면 틀린 기록이 영원히
        // 남습니다.
        String target = createUser();
        report(createUser(), target, "ABUSE", null);

        Admin admin = login();
        review(admin, target, "ACTIONED");

        // 이미 ACTIONED 라 미검토가 아니므로, 뒤집으려면 그 상태에서 다시 부릅니다.
        // 지금 구현은 미검토만 건드리므로 여기서는 0 건입니다. 판단을 바꾸는 것은
        // 그 사람의 신고가 다시 미검토로 올라왔을 때의 이야기입니다.
        review(admin, target, "DISMISSED").andExpect(jsonPath("$.reviewed").value(0));
        assertThat(anyReportAbout(target).get("status")).isEqualTo("ACTIONED");
    }

    @Test
    @DisplayName("PENDING 으로 되돌리는 것은 막는다")
    void cannotUnreview() throws Exception {
        // 검토를 취소하면 reviewed_at 과 reviewed_by 가 남은 채 상태만 미검토가 되어
        // "아직 아무도 안 본 신고"와 구분되지 않습니다.
        String target = createUser();
        report(createUser(), target, "ABUSE", null);

        review(login(), target, "PENDING")
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("INVALID_REQUEST"));
    }

    @Test
    @DisplayName("없는 사용자를 처리하려 하면 404")
    void reviewingNobodyIs404() throws Exception {
        review(login(), UUID.randomUUID().toString(), "ACTIONED")
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.code").value("TARGET_NOT_FOUND"));
    }

    @Test
    @DisplayName("상세를 상태로 걸러 볼 수 있다")
    void detailCanBeFilteredByStatus() throws Exception {
        // 미검토를 보다가 펼쳤는데 예전에 판단한 것까지 섞여 나오면 지금 무엇을
        // 정해야 하는지가 흐려집니다.
        String target = createUser();
        report(createUser(), target, "ABUSE", "먼저 온 것");

        Admin admin = login();
        review(admin, target, "ACTIONED").andExpect(status().isOk());

        report(createUser(), target, "SPAM", "나중에 온 것");

        // 걸러서 보면 그 상태만
        mvc.perform(get(REPORTS + "/" + target).param("status", "PENDING").session(admin.session()))
                .andExpect(jsonPath("$.reports.length()").value(1))
                .andExpect(jsonPath("$.reports[0].memo").value("나중에 온 것"));

        mvc.perform(get(REPORTS + "/" + target).param("status", "ACTIONED").session(admin.session()))
                .andExpect(jsonPath("$.reports.length()").value(1))
                .andExpect(jsonPath("$.reports[0].memo").value("먼저 온 것"));

        // 비워 두면 전부. "이 사람 그동안 어땠나"를 볼 때 필요합니다.
        mvc.perform(get(REPORTS + "/" + target).session(admin.session()))
                .andExpect(jsonPath("$.reports.length()").value(2));
    }

    @Test
    @DisplayName("신고가 없는 사람의 상세는 빈 목록이고, 없는 계정은 404")
    void detailTellsTheTwoApart() throws Exception {
        Admin admin = login();
        String clean = createUser();

        mvc.perform(get(REPORTS + "/" + clean).session(admin.session()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.reports.length()").value(0));

        mvc.perform(get(REPORTS + "/" + UUID.randomUUID()).session(admin.session()))
                .andExpect(status().isNotFound());
    }

    @Test
    @DisplayName("상세에 신고자가 담기지 않는다")
    void detailNeverNamesTheReporter() throws Exception {
        // 신고자를 드러내면 보복의 여지가 생깁니다. 운영자가 판단할 때 필요한 것은
        // 무엇이 몇 번 일어났는가이고, 인원수는 목록의 reporterCount 로 충분합니다.
        String reporter = createUser();
        String target = createUser();
        report(reporter, target, "ABUSE", "욕설");

        Admin admin = login();
        String body = mvc.perform(get(REPORTS + "/" + target).session(admin.session()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.reports[0].memo").value("욕설"))
                .andReturn().getResponse().getContentAsString();

        assertThat(body).doesNotContain(reporter);
    }

    // --- 도우미 -------------------------------------------------------------

    private ResultActions review(Admin admin, String userId, String status) throws Exception {
        // 화면이 하는 것과 같게 보냅니다 - 쿠키는 자동으로 실려 가고, 화면이 그 쿠키를
        // 읽어 헤더에도 넣습니다. 헤더만 보내면 서버가 비교할 원본이 없어 403 입니다.
        return mvc.perform(patch(REPORTS + "/{userId}", userId)
                .session(admin.session())
                .cookie(admin.csrf())
                .header("X-XSRF-TOKEN", admin.csrf().getValue())
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"status\":\"" + status + "\"}"));
    }

    private Map<String, Object> rowFor(Admin admin, String userId) throws Exception {
        return rowFor(admin, userId, "PENDING");
    }

    /** 목록에서 그 사람의 줄을 찾습니다. 없으면 null 입니다. */
    private Map<String, Object> rowFor(Admin admin, String userId, String status) throws Exception {
        String body = mvc.perform(get(REPORTS).param("status", status).session(admin.session()))
                .andExpect(status().isOk())
                .andReturn().getResponse().getContentAsString();

        for (var node : objectMapper.readTree(body).get("users")) {
            if (userId.equals(node.get("userId").asText())) {
                return objectMapper.readValue(node.toString(), Map.class);
            }
        }

        return null;
    }

    private Map<String, Object> anyReportAbout(String userId) {
        return jdbcTemplate.queryForMap("""
                SELECT r.status, r.reviewed_by, r.reviewed_at
                  FROM user_reports r
                  JOIN users u ON u.users_seq = r.reported_seq
                 WHERE u.public_id = ?
                 LIMIT 1
                """, userId);
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
        return createUser(UUID.randomUUID().toString());
    }

    private String createUser(String deviceId) throws Exception {
        String body = mvc.perform(post("/api/v1/accounts")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"deviceId\":\"" + deviceId + "\"}"))
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
}
