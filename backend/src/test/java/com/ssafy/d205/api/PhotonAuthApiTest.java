package com.ssafy.d205.api;

import jakarta.servlet.http.Cookie;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.mock.web.MockHttpSession;
import org.springframework.test.context.TestPropertySource;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.MvcResult;
import org.springframework.test.web.servlet.ResultActions;
import tools.jackson.databind.ObjectMapper;

import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.ssafy.d205.support.IntegrationTest;

/**
 * Photon 커스텀 인증 (S15P21D205-925).
 *
 * <p>정지가 게임을 막는 유일한 자리입니다. 백엔드에는 방 API 가 없고 방 만들기와 참가와
 * 플레이가 전부 Photon 이라, {@code SuspensionInterceptor} 는 친구와 초대와 알림만 막습니다.
 *
 * <p>비밀을 테스트 프로퍼티로 넣습니다. 비어 있으면 기능이 꺼진 것으로 보고 전부
 * 통과시키는 동작이라, 설정 없이 돌리면 이 테스트가 통째로 무의미해집니다.
 */
@TestPropertySource(properties = {
        "photon.auth.secret=test-photon-signing-secret",
        "photon.auth.key=test-dashboard-key"
})
class PhotonAuthApiTest extends IntegrationTest {

    private static final String AUTH = "/api/v1/photon/auth";
    private static final String LOGIN = "/api/v1/admin/session";
    private static final String ADMIN_USERS = "/api/v1/admin/users";
    private static final String KEY = "test-dashboard-key";

    @Autowired
    MockMvc mvc;

    @Autowired
    ObjectMapper objectMapper;

    @Autowired
    JdbcTemplate jdbcTemplate;

    @Test
    @DisplayName("정상 계정은 통과하고 userId 를 그대로 돌려받는다")
    void aNormalAccountPasses() throws Exception {
        Account account = createAccount();

        auth(account.userId(), account.photonToken(), KEY)
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.ResultCode").value(1))
                .andExpect(jsonPath("$.UserId").value(account.userId()));
    }

    @Test
    @DisplayName("정지된 계정은 거절된다")
    void aSuspendedAccountIsRejected() throws Exception {
        Account account = createAccount();
        suspend(account.userId());

        auth(account.userId(), account.photonToken(), KEY)
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.ResultCode").value(2));
    }

    @Test
    @DisplayName("거절도 HTTP 200 이다")
    void rejectionIsStillHttp200() throws Exception {
        // Photon 은 HTTP 오류를 "인증 서버 고장"으로 읽고 대시보드 설정에 따라 통과시킵니다.
        // 403 이나 401 로 답하면 막으려던 사람이 오히려 들어옵니다.
        Account account = createAccount();
        suspend(account.userId());

        auth(account.userId(), account.photonToken(), KEY).andExpect(status().isOk());
        auth(account.userId(), "엉터리토큰", KEY).andExpect(status().isOk());
        auth(account.userId(), account.photonToken(), "틀린키").andExpect(status().isOk());
        auth(null, null, null).andExpect(status().isOk());
    }

    @Test
    @DisplayName("남의 userId 를 넣어도 토큰이 없으면 통과하지 못한다")
    void aStolenUserIdWithoutItsTokenIsRefused() throws Exception {
        // 이 테스트가 이 기능의 요점입니다. 토큰을 받지 않으면 정지된 사람이 남의 userId 나
        // 아무 값이나 넣어 Photon 접속을 통과합니다.
        Account suspended = createAccount();
        Account healthy = createAccount();
        suspend(suspended.userId());

        auth(healthy.userId(), null, KEY)
                .andExpect(jsonPath("$.ResultCode").value(3));

        auth(healthy.userId(), suspended.photonToken(), KEY)
                .andExpect(jsonPath("$.ResultCode").value(3));
    }

    @Test
    @DisplayName("공유 비밀이 틀리면 거절한다")
    void aWrongKeyIsRefused() throws Exception {
        // 주소가 공개라 누구나 부를 수 있습니다. 통과시키면 남의 정지 여부를 밖에서
        // 물어볼 수 있는 창구가 됩니다.
        Account account = createAccount();

        auth(account.userId(), account.photonToken(), "틀린키")
                .andExpect(jsonPath("$.ResultCode").value(3));

        auth(account.userId(), account.photonToken(), null)
                .andExpect(jsonPath("$.ResultCode").value(3));
    }

    @Test
    @DisplayName("모르는 계정은 통과시킨다")
    void anUnknownAccountPasses() throws Exception {
        // 백엔드가 앱 시작 때 잠깐 죽어 있어 계정을 아직 못 받은 경우가 있습니다. 막으면
        // fail-open 결정과 어긋납니다. 토큰 검증이 먼저라 아무 값이나 넣어 통과하는 길은
        // 닫혀 있습니다 - 모르는 id 의 올바른 토큰을 만들려면 서버 비밀이 있어야 합니다.
        String unknown = UUID.randomUUID().toString();

        auth(unknown, tokenFor(unknown), KEY)
                .andExpect(jsonPath("$.ResultCode").value(1));
    }

    @Test
    @DisplayName("해제하면 다시 통과한다")
    void liftingLetsThemBackIn() throws Exception {
        Account account = createAccount();
        Admin admin = login();

        suspendAs(admin, account.userId());
        auth(account.userId(), account.photonToken(), KEY)
                .andExpect(jsonPath("$.ResultCode").value(2));

        lift(admin, account.userId());
        auth(account.userId(), account.photonToken(), KEY)
                .andExpect(jsonPath("$.ResultCode").value(1));
    }

    @Test
    @DisplayName("정지된 계정의 토큰은 계정을 다시 읽어서 얻을 수 없다")
    void aSuspendedAccountCannotRefreshItsToken() throws Exception {
        // 토큰은 계정 응답에 실려 나갑니다. 정지된 뒤에도 그 응답을 받을 수 있으면
        // 토큰을 잃어버린 사람이 다시 받아갈 수 있는데, 정지 검사가 그 경로를 막습니다.
        Account account = createAccount();
        suspend(account.userId());

        // 토큰은 실어 보냅니다(975). 안 실으면 정지 검사에 닿기 전에 401 로 끝나 이 테스트가
        // 보려는 것(정지가 토큰 재발급 경로를 막는가)을 못 봅니다.
        mvc.perform(get("/api/v1/accounts/me")
                        .header("X-User-Id", account.userId())
                        .header("X-Account-Token", account.photonToken()))
                .andExpect(status().isForbidden())
                .andExpect(jsonPath("$.code").value("SUSPENDED"));
    }

    @Test
    @DisplayName("정지 검사가 인증 엔드포인트를 막지 않는다")
    void theAuthEndpointIsNotBlockedByTheSuspensionCheck() throws Exception {
        // 헤더가 실려 오는 일은 없지만, 막히면 막으려던 사람이 오히려 들어옵니다
        // (Photon 이 오류를 고장으로 읽고 통과시킵니다).
        Account account = createAccount();
        suspend(account.userId());

        mvc.perform(get(AUTH)
                        .param("userId", account.userId())
                        .param("token", account.photonToken())
                        .param("key", KEY)
                        .header("X-User-Id", account.userId()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.ResultCode").value(2));
    }

    @Test
    @DisplayName("계정 응답에 토큰이 함께 온다")
    void theTokenRidesWithEveryAccountResponse() throws Exception {
        // 발급에서만 주면 앱을 껐다 켠 클라이언트가 /me 로 계정을 읽은 뒤 토큰 없이
        // Photon 에 붙습니다.
        Account issued = createAccount();
        assertThat(issued.photonToken()).isNotBlank();

        String body = mvc.perform(get("/api/v1/accounts/me")
                        .header("X-User-Id", issued.userId())
                        .header("X-Account-Token", issued.photonToken()))
                .andExpect(status().isOk())
                .andReturn().getResponse().getContentAsString();

        assertThat(objectMapper.readTree(body).get("photonToken").asText())
                .isEqualTo(issued.photonToken());
    }

    private String tokenFor(String userId) throws Exception {
        // 서버와 같은 방법으로 만듭니다. 테스트가 서버 코드를 그대로 부르면 서명이
        // 틀려도 양쪽이 같이 틀려서 통과합니다.
        javax.crypto.Mac mac = javax.crypto.Mac.getInstance("HmacSHA256");
        mac.init(new javax.crypto.spec.SecretKeySpec(
                "test-photon-signing-secret".getBytes(java.nio.charset.StandardCharsets.UTF_8),
                "HmacSHA256"));
        return java.util.Base64.getUrlEncoder().withoutPadding().encodeToString(
                mac.doFinal(userId.getBytes(java.nio.charset.StandardCharsets.UTF_8)));
    }

    private ResultActions auth(String userId, String token, String key) throws Exception {
        var request = get(AUTH);
        if (userId != null) {
            request = request.param("userId", userId);
        }
        if (token != null) {
            request = request.param("token", token);
        }
        if (key != null) {
            request = request.param("key", key);
        }
        return mvc.perform(request);
    }

    private void suspend(String userId) throws Exception {
        suspendAs(login(), userId);
    }

    private void suspendAs(Admin admin, String userId) throws Exception {
        mvc.perform(put(ADMIN_USERS + "/{userId}/suspension", userId)
                        .session(admin.session())
                        .cookie(admin.csrf())
                        .header("X-XSRF-TOKEN", admin.csrf().getValue())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"reason\":\"테스트\"}"))
                .andExpect(status().isOk());
    }

    private void lift(Admin admin, String userId) throws Exception {
        mvc.perform(org.springframework.test.web.servlet.request.MockMvcRequestBuilders
                        .delete(ADMIN_USERS + "/{userId}/suspension", userId)
                        .session(admin.session())
                        .cookie(admin.csrf())
                        .header("X-XSRF-TOKEN", admin.csrf().getValue()))
                .andExpect(status().isOk());
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

    private record Account(String userId, String photonToken) {
    }

    private Account createAccount() throws Exception {
        String body = mvc.perform(post("/api/v1/accounts")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"deviceId\":\"" + UUID.randomUUID() + "\"}"))
                .andExpect(status().isCreated())
                .andReturn().getResponse().getContentAsString();

        var json = objectMapper.readTree(body);
        return new Account(json.get("userId").asText(), json.get("photonToken").asText());
    }
}
