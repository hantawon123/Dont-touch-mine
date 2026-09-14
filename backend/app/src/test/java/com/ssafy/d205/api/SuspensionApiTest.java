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

import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.ssafy.d205.support.IntegrationTest;

/**
 * 계정 정지 (S15P21D205-923).
 *
 * <p>이 프로젝트에는 로그인이 없습니다. 앱 시작 때 기기 식별자로 계정을 발급받고 그 뒤로는
 * X-User-Id 를 붙입니다. 그래서 "로그인을 막는다"가 두 곳이고, 두 곳 모두를 봅니다.
 * 발급만 막으면 이미 켜져 있는 클라이언트가 그대로 플레이하기 때문입니다.
 */
class SuspensionApiTest extends IntegrationTest {

    private static final String USER_ID_HEADER = "X-User-Id";
    private static final String LOGIN = "/api/v1/admin/session";
    private static final String ADMIN_USERS = "/api/v1/admin/users";

    @Autowired
    MockMvc mvc;

    @Autowired
    ObjectMapper objectMapper;

    @Autowired
    JdbcTemplate jdbcTemplate;

    @Test
    @DisplayName("정지하면 그 기기로는 계정을 다시 발급받지 못한다")
    void issuingIsRefusedForASuspendedAccount() throws Exception {
        String deviceId = UUID.randomUUID().toString();
        String userId = createUser(deviceId);

        suspend(login(), userId, "욕설로 신고가 여러 건 들어옴")
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.changed").value(true));

        // 앱을 껐다 켜는 자리입니다. 같은 기기가 부르면 원래는 200 으로 기존 계정이 나옵니다.
        mvc.perform(post("/api/v1/accounts")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"deviceId\":\"" + deviceId + "\"}"))
                .andExpect(status().isForbidden())
                .andExpect(jsonPath("$.code").value("SUSPENDED"));
    }

    @Test
    @DisplayName("이미 켜져 있는 클라이언트도 막힌다")
    void anAlreadyRunningClientIsBlockedToo() throws Exception {
        String userId = createUser();

        // 정지 전에는 된다는 것을 먼저 보입니다. 이게 없으면 아래 403 이 정지 때문인지
        // 이 엔드포인트가 원래 안 되는 것인지 구분되지 않습니다.
        mvc.perform(get("/api/v1/accounts/me").header(USER_ID_HEADER, userId))
                .andExpect(status().isOk());

        suspend(login(), userId, "부정행위");

        // 클라이언트는 계정 발급을 다시 부르지 않았습니다. 헤더만 들고 계속 요청합니다.
        mvc.perform(get("/api/v1/accounts/me").header(USER_ID_HEADER, userId))
                .andExpect(status().isForbidden())
                .andExpect(jsonPath("$.code").value("SUSPENDED"));
    }

    @Test
    @DisplayName("정지는 계정 발급 말고 다른 경로도 막는다")
    void suspensionBlocksOtherEndpointsAsWell() throws Exception {
        String userId = createUser();
        String other = createUser();

        suspend(login(), userId, "도배");

        // 신고는 인증이 없는 경로입니다. 그래도 헤더가 붙으므로 막혀야 합니다 - 정지된
        // 사람이 남을 신고하는 것까지 열어두면 정지의 뜻이 반쪽이 됩니다.
        mvc.perform(post("/api/v1/reports")
                        .header(USER_ID_HEADER, userId)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"userId\":\"" + other + "\",\"reason\":\"ABUSE\"}"))
                .andExpect(status().isForbidden())
                .andExpect(jsonPath("$.code").value("SUSPENDED"));
    }

    @Test
    @DisplayName("해제하면 발급도 요청도 다시 된다")
    void liftingRestoresBoth() throws Exception {
        String deviceId = UUID.randomUUID().toString();
        String userId = createUser(deviceId);
        Admin admin = login();

        suspend(admin, userId, "오인");
        lift(admin, userId)
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.changed").value(true));

        mvc.perform(post("/api/v1/accounts")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"deviceId\":\"" + deviceId + "\"}"))
                .andExpect(status().isOk());

        mvc.perform(get("/api/v1/accounts/me").header(USER_ID_HEADER, userId))
                .andExpect(status().isOk());
    }

    @Test
    @DisplayName("정지와 해제는 멱등하고, 바뀐 게 없으면 changed 가 false 다")
    void bothOperationsAreIdempotent() throws Exception {
        String userId = createUser();
        Admin admin = login();

        suspend(admin, userId, "처음 사유").andExpect(jsonPath("$.changed").value(true));
        suspend(admin, userId, "고쳐 적은 사유").andExpect(jsonPath("$.changed").value(false));

        // 두 번째 요청이 사유를 덮어씁니다. 거절하면 사유를 고치려고 해제했다가 다시
        // 정지해야 하고, 그 사이에 그 사람이 들어옵니다.
        assertThat(reasonOf(userId)).isEqualTo("고쳐 적은 사유");

        lift(admin, userId).andExpect(jsonPath("$.changed").value(true));
        lift(admin, userId).andExpect(jsonPath("$.changed").value(false));
    }

    @Test
    @DisplayName("정지하면 시각과 사유가 함께 남는다")
    void suspensionWritesBothTimeAndReason() throws Exception {
        String userId = createUser();

        suspend(login(), userId, "괴롭힘");

        assertThat(suspendedAtOf(userId)).hasSize(14);
        assertThat(reasonOf(userId)).isEqualTo("괴롭힘");
    }

    @Test
    @DisplayName("해제하면 시각과 사유가 함께 지워진다")
    void liftingClearsBoth() throws Exception {
        String userId = createUser();
        Admin admin = login();

        suspend(admin, userId, "괴롭힘");
        lift(admin, userId);

        assertThat(suspendedAtOf(userId)).isNull();
        assertThat(reasonOf(userId)).isNull();
    }

    @Test
    @DisplayName("사유가 없으면 400 INVALID_REQUEST")
    void reasonIsRequired() throws Exception {
        String userId = createUser();
        Admin admin = login();

        putSuspension(admin, userId, "{}")
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("INVALID_REQUEST"));

        putSuspension(admin, userId, "{\"reason\":\"   \"}")
                .andExpect(status().isBadRequest());

        // 거절당했으면 정지되지 않았어야 합니다.
        assertThat(suspendedAtOf(userId)).isNull();
    }

    @Test
    @DisplayName("사유가 200자를 넘으면 400 INVALID_REQUEST")
    void anOverlongReasonIsRejected() throws Exception {
        String userId = createUser();
        Admin admin = login();

        putSuspension(admin, userId, "{\"reason\":\"" + "가".repeat(200) + "\"}")
                .andExpect(status().isOk());
        lift(admin, userId);

        putSuspension(admin, userId, "{\"reason\":\"" + "가".repeat(201) + "\"}")
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("INVALID_REQUEST"));
    }

    @Test
    @DisplayName("없는 사용자를 정지하면 404 TARGET_NOT_FOUND")
    void suspendingAnUnknownUserIsNotFound() throws Exception {
        suspend(login(), UUID.randomUUID().toString(), "아무거나")
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.code").value("TARGET_NOT_FOUND"));
    }

    @Test
    @DisplayName("로그인하지 않으면 정지도 해제도 할 수 없다")
    void suspendingRequiresAnAdminSession() throws Exception {
        String userId = createUser();

        mvc.perform(put(ADMIN_USERS + "/" + userId + "/suspension")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"reason\":\"몰래\"}"))
                .andExpect(status().isForbidden());

        mvc.perform(delete(ADMIN_USERS + "/" + userId + "/suspension"))
                .andExpect(status().isForbidden());

        // 무엇이 막았든 계정은 그대로여야 합니다.
        assertThat(suspendedAtOf(userId)).isNull();
    }

    @Test
    @DisplayName("운영자 화면은 정지의 영향을 받지 않는다")
    void theAdminConsoleKeepsWorkingForSuspendedUsers() throws Exception {
        // 운영자는 X-User-Id 를 쓰지 않지만, 인터셉터를 /** 에 걸었으므로 관리자 경로가
        // 제외되는지 확인합니다. 여기가 막히면 정지를 푸는 길 자체가 사라집니다.
        String userId = createUser();
        Admin admin = login();

        suspend(admin, userId, "정지");

        mvc.perform(get("/api/v1/admin/reports")
                        .header(USER_ID_HEADER, userId)
                        .session(admin.session()))
                .andExpect(status().isOk());

        lift(admin, userId).andExpect(status().isOk());
    }

    @Test
    @DisplayName("정지된 계정은 스스로 탈퇴할 수 없다")
    void aSuspendedAccountCannotDeleteItself() throws Exception {
        // 이게 막히지 않으면 정지가 한 번의 탈퇴로 풀립니다. 계정을 지우면 user_identities
        // 도 CASCADE 로 사라지므로, 같은 기기로 다시 발급받으면 정지가 없는 새 계정이
        // 나옵니다. 기기 식별자를 바꿀 필요조차 없습니다.
        //
        // 지금은 인터셉터가 X-User-Id 를 보고 막습니다. 우연히 닫힌 구멍이라 이 테스트가
        // 없으면 나중에 탈퇴만 예외로 열어 주는 변경이 조용히 들어올 수 있습니다.
        String deviceId = UUID.randomUUID().toString();
        String userId = createUser(deviceId);

        suspend(login(), userId, "정지");

        mvc.perform(delete("/api/v1/accounts/me")
                        .header(USER_ID_HEADER, userId)
                        .header("X-Device-Id", deviceId))
                .andExpect(status().isForbidden())
                .andExpect(jsonPath("$.code").value("SUSPENDED"));

        // 계정이 그대로 있고 정지도 그대로여야 합니다.
        assertThat(suspendedAtOf(userId)).isNotNull();
    }

    @Test
    @DisplayName("신고 목록이 정지 여부를 함께 돌려준다")
    void theReportListCarriesTheSuspensionState() throws Exception {
        // 운영 화면이 버튼을 어느 쪽으로 그릴지 정하는 값입니다(S15P21D205-927).
        // 목록을 그린 뒤 사람 수만큼 다시 묻지 않으려고 여기 싣습니다.
        String reporter = createUser();
        String target = createUser();
        Admin admin = login();

        mvc.perform(post("/api/v1/reports")
                        .header(USER_ID_HEADER, reporter)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"userId\":\"" + target + "\",\"reason\":\"ABUSE\"}"))
                .andExpect(status().isCreated());

        assertThat(suspendedInList(admin, target)).isFalse();

        suspend(admin, target, "신고 누적");
        assertThat(suspendedInList(admin, target)).isTrue();

        lift(admin, target);
        assertThat(suspendedInList(admin, target)).isFalse();
    }

    @Test
    @DisplayName("기기 식별자를 바꾸면 새 계정이 발급된다")
    void aNewDeviceIdGetsAFreshAccount() throws Exception {
        // 우회가 가능하다는 사실을 박아 둡니다. 정지는 계정에 걸린 것이고, 계정은 기기
        // 식별자에 묶여 있습니다. 이 구조에서는 막을 수 없고 진짜 인증이 붙어야 닫힙니다.
        // 이 테스트가 깨지는 날은 그 전제가 바뀐 날이므로, 그때 정지도 같이 봐야 합니다.
        String userId = createUser();
        suspend(login(), userId, "정지");

        String fresh = createUser();

        assertThat(fresh).isNotEqualTo(userId);
        mvc.perform(get("/api/v1/accounts/me").header(USER_ID_HEADER, fresh))
                .andExpect(status().isOk());
    }

    @Test
    @DisplayName("헤더가 없는 요청과 모르는 헤더는 정지 검사가 건드리지 않는다")
    void theCheckDoesNotInventFailures() throws Exception {
        // 인터셉터는 인증이 아닙니다. 헤더가 없으면 통과시키고(계정 발급이 그 자리),
        // 모르는 값이면 컨트롤러가 404 로 답하게 둡니다. 여기서 403 으로 바꾸면 없는
        // 계정과 정지된 계정이 구분되지 않습니다.
        mvc.perform(get("/api/v1/accounts/me"))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("MISSING_HEADER"));

        mvc.perform(get("/api/v1/accounts/me").header(USER_ID_HEADER, UUID.randomUUID().toString()))
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.code").value("ACCOUNT_NOT_FOUND"));
    }

    private ResultActions suspend(Admin admin, String userId, String reason) throws Exception {
        return putSuspension(admin, userId, "{\"reason\":\"" + reason + "\"}");
    }

    private ResultActions putSuspension(Admin admin, String userId, String body) throws Exception {
        return mvc.perform(put(ADMIN_USERS + "/{userId}/suspension", userId)
                .session(admin.session())
                .cookie(admin.csrf())
                .header("X-XSRF-TOKEN", admin.csrf().getValue())
                .contentType(MediaType.APPLICATION_JSON)
                .content(body));
    }

    private ResultActions lift(Admin admin, String userId) throws Exception {
        return mvc.perform(delete(ADMIN_USERS + "/{userId}/suspension", userId)
                .session(admin.session())
                .cookie(admin.csrf())
                .header("X-XSRF-TOKEN", admin.csrf().getValue()));
    }

    /** 신고당한 사람 목록에서 그 사람의 정지 여부. 목록에 없으면 실패시킵니다. */
    private boolean suspendedInList(Admin admin, String userId) throws Exception {
        String body = mvc.perform(get("/api/v1/admin/reports").session(admin.session()))
                .andExpect(status().isOk())
                .andReturn().getResponse().getContentAsString();

        for (var node : objectMapper.readTree(body).get("users")) {
            if (userId.equals(node.get("userId").asText())) {
                return node.get("suspended").asBoolean();
            }
        }

        throw new AssertionError("신고 목록에 " + userId + " 가 없습니다: " + body);
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

    private String suspendedAtOf(String userId) {
        return jdbcTemplate.queryForObject(
                "SELECT suspended_at FROM users WHERE public_id = ?", String.class, userId);
    }

    private String reasonOf(String userId) {
        return jdbcTemplate.queryForObject(
                "SELECT suspended_reason FROM users WHERE public_id = ?", String.class, userId);
    }
}
