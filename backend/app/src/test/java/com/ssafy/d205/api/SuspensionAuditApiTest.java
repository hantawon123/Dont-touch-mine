package com.ssafy.d205.api;

import jakarta.servlet.http.Cookie;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.mock.web.MockHttpSession;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.MvcResult;
import tools.jackson.databind.JsonNode;
import tools.jackson.databind.ObjectMapper;

import java.util.ArrayList;
import java.util.List;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.ssafy.d205.support.IntegrationTest;

/**
 * 정지 감사 로그와 정지 리스트 (S15P21D205-974).
 *
 * <p>그 전에는 users 의 지금 상태만 있어서, 해제하면 정지했던 사실이 사라지고 누가 눌렀는지도
 * 남지 않았습니다. 이 테스트는 누를 때마다 기록이 쌓이고, 해제해도 남고, 대상이 탈퇴해도
 * 남는 것을 고정합니다.
 */
class SuspensionAuditApiTest extends IntegrationTest {

    private static final String USER_ID_HEADER = "X-User-Id";
    private static final String DEVICE_ID_HEADER = "X-Device-Id";
    private static final String LOGIN = "/api/v1/admin/session";
    private static final String USERS = "/api/v1/admin/users";
    private static final String SUSPENSIONS = "/api/v1/admin/suspensions";

    /** SecurityConfig 가 테스트 프로필에서 쓰는 계정 이름입니다. 감사 행의 처리자가 됩니다. */
    private static final String ADMIN = "test-admin";

    @Autowired
    MockMvc mvc;

    @Autowired
    ObjectMapper objectMapper;

    @Test
    @DisplayName("로그인 없이 부르면 401")
    void needsAnAdminSession() throws Exception {
        mvc.perform(get(SUSPENSIONS)).andExpect(status().isUnauthorized());
    }

    @Test
    @DisplayName("정지와 해제를 반복하면 이력이 최신순으로 쌓인다")
    void stacksEveryActionNewestFirst() throws Exception {
        Admin admin = login();
        String user = createUser();

        suspend(admin, user, "첫 번째");
        lift(admin, user);
        suspend(admin, user, "두 번째");

        JsonNode history = historyOf(admin, user);

        assertThat(actionsOf(history)).containsExactly("SUSPEND", "LIFT", "SUSPEND");
        assertThat(history.get(0).get("reason").asText()).isEqualTo("두 번째");

        // 해제에는 사유가 없습니다. 운영자에게 받지 않는 값입니다.
        assertThat(history.get(1).get("reason").isNull()).isTrue();
        assertThat(history.get(2).get("reason").asText()).isEqualTo("첫 번째");
    }

    @Test
    @DisplayName("처리자로 로그인한 관리자 이름이 남는다")
    void recordsWhoPressedIt() throws Exception {
        Admin admin = login();
        String user = createUser();

        suspend(admin, user, "처리자 확인");

        JsonNode entry = historyOf(admin, user).get(0);
        assertThat(entry.get("adminUsername").asText()).isEqualTo(ADMIN);
        assertThat(entry.get("actedAt").asText()).hasSize(14);
        assertThat(entry.get("accountDeleted").asBoolean()).isFalse();
    }

    @Test
    @DisplayName("이미 정지된 계정에 다시 눌러도 기록이 남는다")
    void recordsRepeatedSuspensionsSoTheReasonChangeIsTraceable() throws Exception {
        Admin admin = login();
        String user = createUser();

        suspend(admin, user, "처음 적은 사유");
        // 상태는 그대로지만 사유가 바뀝니다. 남기지 않으면 언제 왜 바뀌었는지 알 수 없습니다.
        suspend(admin, user, "고쳐 적은 사유");

        JsonNode history = historyOf(admin, user);
        assertThat(actionsOf(history)).containsExactly("SUSPEND", "SUSPEND");
        assertThat(history.get(0).get("reason").asText()).isEqualTo("고쳐 적은 사유");
    }

    @Test
    @DisplayName("정지 목록에 나오고 해제하면 빠진다")
    void listsOnlyAccountsThatAreSuspendedRightNow() throws Exception {
        Admin admin = login();
        String user = createUser();

        suspend(admin, user, "목록 확인");
        assertThat(suspendedIdsOf(admin)).contains(user);

        lift(admin, user);
        assertThat(suspendedIdsOf(admin)).doesNotContain(user);
    }

    @Test
    @DisplayName("정지 목록의 한 줄에 사유와 처리자가 함께 있다")
    void showsReasonAndAdminOnEachSuspendedRow() throws Exception {
        Admin admin = login();
        String user = createUser();

        suspend(admin, user, "사유가 보여야 한다");

        JsonNode row = suspendedRowOf(admin, user);
        assertThat(row.get("reason").asText()).isEqualTo("사유가 보여야 한다");
        assertThat(row.get("adminUsername").asText()).isEqualTo(ADMIN);
        assertThat(row.get("suspendedAt").asText()).hasSize(14);
    }

    @Test
    @DisplayName("최근 해제 이력에 해제가 남는다")
    void keepsRecentLifts() throws Exception {
        Admin admin = login();
        String user = createUser();

        suspend(admin, user, "해제 전");
        lift(admin, user);

        assertThat(liftedIdsOf(admin)).contains(user);
    }

    @Test
    @DisplayName("대상이 탈퇴해도 이력은 남고 탈퇴한 것으로 표시된다")
    void survivesTheAccountItIsAbout() throws Exception {
        Admin admin = login();
        String deviceId = UUID.randomUUID().toString();
        String user = createUser(deviceId);

        suspend(admin, user, "탈퇴 전 정지");

        // 정지된 계정은 스스로 탈퇴하지 못하므로 먼저 풉니다. 이 테스트가 고정하려는 것은
        // 탈퇴 경로가 아니라 그 뒤에 이력이 남는지입니다.
        lift(admin, user);
        mvc.perform(delete("/api/v1/accounts/me")
                        .header(USER_ID_HEADER, user)
                        .header(DEVICE_ID_HEADER, deviceId))
                .andExpect(status().isNoContent());

        // 사용자 상세는 이제 404 입니다. 계정이 없으니 당연합니다. 이력은 따로 남습니다.
        mvc.perform(get(USERS + "/{userId}", user).session(admin.session()))
                .andExpect(status().isNotFound());

        JsonNode mine = liftRowOf(admin, user);
        assertThat(mine.get("accountDeleted").asBoolean()).isTrue();
        // 닉네임은 그 시점 값을 복사해 둔 것이라 계정이 사라져도 남습니다.
        assertThat(mine.get("nickname").asText()).isNotBlank();
    }

    private List<String> actionsOf(JsonNode history) {
        List<String> actions = new ArrayList<>();
        history.forEach(entry -> actions.add(entry.get("action").asText()));
        return actions;
    }

    private JsonNode historyOf(Admin admin, String userId) throws Exception {
        String body = mvc.perform(get(USERS + "/{userId}", userId).session(admin.session()))
                .andExpect(status().isOk())
                .andReturn().getResponse().getContentAsString();
        return objectMapper.readTree(body).get("suspensions");
    }

    private JsonNode suspensions(Admin admin) throws Exception {
        String body = mvc.perform(get(SUSPENSIONS).session(admin.session()))
                .andExpect(status().isOk())
                .andReturn().getResponse().getContentAsString();
        return objectMapper.readTree(body);
    }

    private List<String> suspendedIdsOf(Admin admin) throws Exception {
        List<String> ids = new ArrayList<>();
        suspensions(admin).get("suspended").forEach(row -> ids.add(row.get("userId").asText()));
        return ids;
    }

    private List<String> liftedIdsOf(Admin admin) throws Exception {
        List<String> ids = new ArrayList<>();
        suspensions(admin).get("recentLifts").forEach(row -> ids.add(row.get("userId").asText()));
        return ids;
    }

    private JsonNode suspendedRowOf(Admin admin, String userId) throws Exception {
        for (JsonNode row : suspensions(admin).get("suspended")) {
            if (row.get("userId").asText().equals(userId)) {
                return row;
            }
        }
        throw new AssertionError("정지 목록에 " + userId + " 가 없습니다.");
    }

    private JsonNode liftRowOf(Admin admin, String userId) throws Exception {
        for (JsonNode row : suspensions(admin).get("recentLifts")) {
            if (row.get("userId").asText().equals(userId)) {
                return row;
            }
        }
        throw new AssertionError("해제 이력에 " + userId + " 가 없습니다.");
    }

    private void suspend(Admin admin, String userId, String reason) throws Exception {
        mvc.perform(put(USERS + "/{userId}/suspension", userId)
                        .session(admin.session())
                        .cookie(admin.csrf())
                        .header("X-XSRF-TOKEN", admin.csrf().getValue())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"reason\":\"" + reason + "\"}"))
                .andExpect(status().isOk());
    }

    private void lift(Admin admin, String userId) throws Exception {
        mvc.perform(delete(USERS + "/{userId}/suspension", userId)
                        .session(admin.session())
                        .cookie(admin.csrf())
                        .header("X-XSRF-TOKEN", admin.csrf().getValue()))
                .andExpect(status().isOk());
    }

    private Admin login() throws Exception {
        MockHttpSession session = new MockHttpSession();

        MvcResult result = mvc.perform(post(LOGIN)
                        .session(session)
                        .param("username", ADMIN)
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
}
