package com.ssafy.d205.api;

import jakarta.servlet.http.Cookie;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.mock.web.MockHttpSession;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.MvcResult;

import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.ssafy.d205.support.IntegrationTest;

/**
 * 관리자 인증.
 *
 * <p><b>여기서 제일 중요한 것은 막히는지가 아니라 열려 있는지입니다.</b>
 * spring-boot-starter-security 는 기본값이 "전부 막기"라, 설정을 빠뜨리면 게임 API 가
 * 통째로 401 이 됩니다. 그 회귀는 이 프로젝트의 나머지 테스트들이 잡습니다 - 전부 실제로
 * 엔드포인트를 부르기 때문입니다.
 *
 * <p>다만 그물에 걸리지 않는 구멍이 하나 있어서 여기서 메웁니다. /actuator/health 는
 * 어느 백엔드 테스트도 부르지 않는데, 막히면 PlayMode 스모크가 "백엔드가 없다"고 판단해
 * <b>실패가 아니라 건너뜀</b>이 됩니다. 초록으로 보이는 고장입니다.
 */
class AdminAuthTest extends IntegrationTest {

    /** application-test.yml 이 등록하는 계정입니다. */
    private static final String USERNAME = "test-admin";
    private static final String PASSWORD = "test-password";

    private static final String SESSION = "/api/v1/admin/session";
    private static final String LOGOUT = "/api/v1/admin/logout";

    @Autowired
    MockMvc mvc;

    @Test
    @DisplayName("헬스체크는 인증 없이 열려 있다")
    void healthStaysOpen() throws Exception {
        // 막히면 PlayMode 스모크 13개가 조용히 건너뛴다. 그쪽에서는 고장으로 보이지
        // 않으므로 여기서 본다.
        mvc.perform(get("/actuator/health"))
                .andExpect(status().isOk());
    }

    @Test
    @DisplayName("게임 API 는 인증 없이 그대로 동작한다")
    void theGameApiStaysOpen() throws Exception {
        // 나머지 테스트들이 이미 확인하지만, 이 파일을 읽는 사람이 "관리자 인증을 넣으면
        // 게임도 로그인해야 하나"를 묻지 않도록 한 줄 남겨 둔다.
        mvc.perform(post("/api/v1/accounts")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"deviceId\":\"" + UUID.randomUUID() + "\"}"))
                .andExpect(status().isCreated());
    }

    @Test
    @DisplayName("관리자 경로는 로그인 없이 401")
    void adminPathsRequireLogin() throws Exception {
        // 이 테스트가 통과한다는 것 자체가 보안 필터가 실제로 걸려 있다는 증거다.
        // 필터가 안 붙어 있으면 여기서 401 이 아니라 200 이나 500 이 난다.
        mvc.perform(get(SESSION))
                .andExpect(status().isUnauthorized())
                .andExpect(jsonPath("$.code").value("UNAUTHORIZED"))
                // 문구까지 보는 이유는 인코딩 때문이다. 응답을 손으로 쓰고 있어서
                // 문자셋을 빠뜨리면 한글이 깨지는데, code 만 확인하면 그것을 못 잡는다.
                .andExpect(jsonPath("$.message").value("로그인이 필요합니다."));
    }

    @Test
    @DisplayName("거부된 요청이 세션을 만들지 않는다")
    void arefusedRequestCreatesNoSession() throws Exception {
        // 기본 동작은 "로그인 뒤 원래 가려던 곳으로 보내주려고" 요청을 세션에 담는
        // 것이다. 우리는 리다이렉트를 하지 않으므로 쓸 곳이 없는데, 관리자 경로를
        // 훑는 요청마다 세션이 하나씩 생긴다.
        MvcResult result = mvc.perform(get(SESSION)).andReturn();

        assertThat(result.getRequest().getSession(false)).isNull();
    }

    @Test
    @DisplayName("로그인 화면으로 넘기지 않고 401 로 답한다")
    void unauthenticatedIsNotARedirect() throws Exception {
        // Spring Security 기본값은 로그인 페이지로 302 다. API 를 부르는 쪽에는
        // 리다이렉트가 성공처럼 보여서, 화면이 로그인 폼 HTML 을 데이터로 받는다.
        MvcResult result = mvc.perform(get(SESSION)).andReturn();

        assertThat(result.getResponse().getStatus()).isEqualTo(401);
        assertThat(result.getResponse().getRedirectedUrl()).isNull();
    }

    @Test
    @DisplayName("비밀번호가 틀리면 401 BAD_CREDENTIALS")
    void wrongPasswordIsRejected() throws Exception {
        mvc.perform(post(SESSION)
                        .param("username", USERNAME)
                        .param("password", "not-the-password"))
                .andExpect(status().isUnauthorized())
                .andExpect(jsonPath("$.code").value("BAD_CREDENTIALS"))
                .andExpect(jsonPath("$.message").value("아이디 또는 비밀번호가 맞지 않습니다."));
    }

    @Test
    @DisplayName("없는 계정도 같은 401 이다")
    void unknownAccountLooksTheSame() throws Exception {
        // 있는 아이디와 없는 아이디를 구분해 답하면 아이디를 하나씩 찔러 볼 수 있다.
        mvc.perform(post(SESSION)
                        .param("username", "nobody")
                        .param("password", PASSWORD))
                .andExpect(status().isUnauthorized())
                .andExpect(jsonPath("$.code").value("BAD_CREDENTIALS"));
    }

    @Test
    @DisplayName("로그인하면 그 세션으로 다음 요청이 통과한다")
    void loginPersistsAcrossRequests() throws Exception {
        // 이 테스트가 이 파일의 핵심이다. 로그인 응답만 보면 204 라서 성공처럼 보이는데,
        // SecurityContext 가 세션에 저장되지 않으면 다음 요청이 401 이 된다. 두 번째
        // 요청을 하지 않으면 그 버그를 못 잡는다.
        LoggedIn admin = login();

        mvc.perform(get(SESSION).session(admin.session()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.username").value(USERNAME));
    }

    @Test
    @DisplayName("로그아웃하면 그 세션이 더는 통하지 않는다")
    void logoutEndsTheSession() throws Exception {
        LoggedIn admin = login();
        mvc.perform(get(SESSION).session(admin.session())).andExpect(status().isOk());

        // 브라우저가 하는 것과 같게 보냅니다 - 쿠키는 자동으로 실려 가고, 화면이 그
        // 쿠키를 읽어 헤더에도 넣습니다. 서버는 둘이 같은지 봅니다. 이것이 이중 제출이고,
        // 헤더만 보내면 서버가 비교할 원본이 없어 403 입니다.
        mvc.perform(post(LOGOUT)
                        .session(admin.session())
                        .cookie(admin.csrfCookie())
                        .header("X-XSRF-TOKEN", admin.csrfCookie().getValue()))
                .andExpect(status().isNoContent());

        mvc.perform(get(SESSION).session(admin.session()))
                .andExpect(status().isUnauthorized());
    }

    @Test
    @DisplayName("로그인 응답이 CSRF 토큰 쿠키를 준다")
    void loginHandsOutTheCsrfToken() throws Exception {
        // 토큰은 필요할 때만 발급되므로, 로그인 응답에서 한 번 읽어 주지 않으면 쿠키가
        // 나가지 않는다. 그러면 화면은 토큰을 구할 방법이 없고 첫 PATCH 에서 403 을
        // 받는다. 테스트에서는 보이지 않고 브라우저에서만 드러나는 종류의 고장이다.
        MvcResult result = mvc.perform(post(SESSION)
                        .param("username", USERNAME)
                        .param("password", PASSWORD))
                .andExpect(status().isNoContent())
                .andReturn();

        Cookie token = result.getResponse().getCookie("XSRF-TOKEN");
        assertThat(token).isNotNull();
        assertThat(token.getValue()).isNotBlank();
    }

    @Test
    @DisplayName("CSRF 토큰 없이 로그아웃하면 거부된다")
    void mutatingWithoutTheCsrfTokenIsRefused() throws Exception {
        // 실제 공격이 어떤 모양인지 그대로 만든다. 운영자가 다른 사이트를 열면 그
        // 페이지가 우리 서버로 요청을 보낼 수 있고, 브라우저는 쿠키를 자동으로 붙인다.
        // 하지만 그 페이지는 쿠키 값을 읽을 수 없어 헤더에는 넣지 못한다.
        //
        // 그래서 쿠키는 있고 헤더만 없는 요청을 보낸다. 여기서 통과하면 CSRF 방어가
        // 사실상 없는 것이다.
        LoggedIn admin = login();

        mvc.perform(post(LOGOUT).session(admin.session()).cookie(admin.csrfCookie()))
                .andExpect(status().isForbidden());

        // 거부됐을 뿐 세션은 살아 있어야 한다.
        mvc.perform(get(SESSION).session(admin.session())).andExpect(status().isOk());
    }

    /**
     * 로그인하고, 세션과 CSRF 토큰을 함께 돌려줍니다.
     *
     * <p>토큰을 세션이 아니라 응답 쿠키에서 꺼내는 것이 중요합니다. 화면이 가진 것도
     * 그 쿠키뿐이라, 세션 속 값을 몰래 꺼내 쓰면 테스트만 통과하고 브라우저에서는
     * 403 이 나는 상태를 못 잡습니다.
     */
    private LoggedIn login() throws Exception {
        MockHttpSession session = new MockHttpSession();

        MvcResult result = mvc.perform(post(SESSION)
                        .session(session)
                        .param("username", USERNAME)
                        .param("password", PASSWORD))
                .andExpect(status().isNoContent())
                .andReturn();

        Cookie token = result.getResponse().getCookie("XSRF-TOKEN");
        assertThat(token).as("로그인 응답에 CSRF 토큰 쿠키가 없습니다").isNotNull();

        return new LoggedIn(session, token);
    }

    private record LoggedIn(MockHttpSession session, Cookie csrfCookie) {
    }
}
