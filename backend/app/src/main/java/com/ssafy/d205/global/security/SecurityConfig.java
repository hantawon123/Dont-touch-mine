package com.ssafy.d205.global.security;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.core.annotation.Order;
import org.springframework.http.HttpStatus;
import org.springframework.http.MediaType;
import org.springframework.security.config.annotation.web.builders.HttpSecurity;
import org.springframework.security.config.http.SessionCreationPolicy;
import org.springframework.security.core.userdetails.User;
import org.springframework.security.core.userdetails.UserDetailsService;
import org.springframework.security.crypto.bcrypt.BCryptPasswordEncoder;
import org.springframework.security.crypto.password.PasswordEncoder;
import org.springframework.security.provisioning.InMemoryUserDetailsManager;
import org.springframework.security.web.SecurityFilterChain;
import org.springframework.security.web.savedrequest.NullRequestCache;
import org.springframework.security.web.csrf.CookieCsrfTokenRepository;
import org.springframework.security.web.csrf.CsrfTokenRequestAttributeHandler;
import tools.jackson.databind.ObjectMapper;

import jakarta.servlet.http.HttpServletResponse;

import java.io.IOException;

import com.ssafy.d205.global.exception.ErrorResponse;

/**
 * 관리자만 인증을 요구하고 나머지는 지금까지처럼 열어 둡니다.
 *
 * <p><b>이 파일이 하는 가장 중요한 일은 막는 것이 아니라 열어 두는 것입니다.</b>
 * spring-boot-starter-security 를 의존성에 넣는 순간 기본값이 "전부 막기"라, 설정이
 * 없으면 계정 발급과 친구와 초대와 신고가 전부 401 이 되고 /actuator/health 까지
 * 닫힙니다. 그러면 PlayMode 스모크가 "백엔드가 없다"고 판단해 스스로를 건너뛰므로
 * <b>실패가 아니라 초록으로 보입니다.</b>
 *
 * <p>범위를 분명히 해 둡니다. 이것은 <b>운영자 인증이지 게임 유저 인증이 아닙니다.</b>
 * X-User-Id 는 여전히 인증이 아니라 식별이고, 남의 id 를 아는 사람이 그 사람 행세를 할
 * 수 있습니다. 그 문제를 고치려면 Unity 클라이언트와 토큰 발급, 갱신, 기존 일곱
 * 엔드포인트가 전부 바뀝니다. 여기서 다루지 않습니다.
 */
@Configuration
public class SecurityConfig {

    private static final Logger log = LoggerFactory.getLogger(SecurityConfig.class);

    private static final String ADMIN_PATHS = "/api/v1/admin/**";
    private static final String LOGIN_PATH = "/api/v1/admin/session";
    private static final String LOGOUT_PATH = "/api/v1/admin/logout";

    /**
     * 관리자 경로. 세션 쿠키로 인증하고 CSRF 를 켭니다.
     *
     * <p>체인을 하나로 두고 예외를 다는 대신 나눈 이유는 두 영역의 성질이 정반대이기
     * 때문입니다. 한쪽은 쿠키를 쓰는 상태 있는 브라우저 세션이고, 다른 쪽은 쿠키가 없는
     * 무상태 API 입니다. 한 체인에 섞으면 "이 설정이 어느 쪽에 걸리는가"를 매번
     * 따져야 합니다.
     *
     * <p><b>CSRF 는 여기서만 켭니다.</b> CSRF 는 브라우저가 자격증명을 자동으로 붙일 때만
     * 성립합니다. 게임 API 는 쿠키를 쓰지 않으므로 위조할 것이 없고, 관리자 쪽은 쿠키를
     * 쓰므로 반드시 필요합니다. 끄면 운영자가 아무 사이트나 열었을 때 그 페이지가
     * 신고를 기각 처리시킬 수 있습니다.
     *
     * <p>로그인 요청만 CSRF 에서 뺍니다. 아직 세션도 토큰도 없는 상태에서 토큰을
     * 요구하면 첫 로그인이 불가능합니다. 로그인 CSRF 는 공격자의 계정으로 로그인시키는
     * 것이라 영향이 작고, 대신 그 응답에서 토큰 쿠키가 나가므로 이후 요청은 보호됩니다.
     */
    @Bean
    @Order(1)
    SecurityFilterChain adminChain(HttpSecurity http, ObjectMapper mapper) throws Exception {
        http
                .securityMatcher(ADMIN_PATHS)
                .authorizeHttpRequests(paths -> paths.anyRequest().authenticated())
                .csrf(csrf -> csrf
                        .csrfTokenRepository(CookieCsrfTokenRepository.withHttpOnlyFalse())
                        .csrfTokenRequestHandler(rawCookieValue())
                        .ignoringRequestMatchers(LOGIN_PATH))
                .sessionManagement(session -> session
                        .sessionCreationPolicy(SessionCreationPolicy.IF_REQUIRED))
                .formLogin(form -> form
                        .loginProcessingUrl(LOGIN_PATH)
                        // 로그인 화면으로 리다이렉트하지 않습니다. 이 서버는 화면을
                        // 주지 않고 상태 코드만 답합니다.
                        .successHandler((request, response, authentication) ->
                                response.setStatus(HttpStatus.NO_CONTENT.value()))
                        .failureHandler((request, response, exception) -> {
                            // 속도 제한이 없습니다. 계정이 하나뿐이라 비밀번호를 계속
                            // 찔러 볼 수 있는데, 최소한 흔적은 남아야 나중에 알아챌 수
                            // 있습니다. 비밀번호는 남기지 않습니다.
                            log.warn("관리자 로그인 실패: username={}, from={}",
                                    LoginAttempt.forLog(request.getParameter("username")),
                                    request.getRemoteAddr());

                            write(mapper, response, HttpStatus.UNAUTHORIZED,
                                    "BAD_CREDENTIALS", "아이디 또는 비밀번호가 맞지 않습니다.");
                        }))
                .logout(logout -> logout
                        .logoutUrl(LOGOUT_PATH)
                        .logoutSuccessHandler((request, response, authentication) ->
                                response.setStatus(HttpStatus.NO_CONTENT.value())))
                // 401 을 낼 때 요청을 저장하지 않습니다. 기본 동작은 "로그인 뒤에 원래
                // 가려던 곳으로 보내주려고" 요청을 세션에 담아 두는 것인데, 그러려면
                // 세션을 만들어야 합니다. 로그인하지 않은 요청 하나가 세션 하나를
                // 만드는 셈이라, 관리자 경로를 훑는 스캐너에 메모리가 쌓입니다.
                //
                // 우리는 리다이렉트를 하지 않으므로 저장해도 쓸 곳이 없습니다.
                .requestCache(cache -> cache.requestCache(new NullRequestCache()))
                // 기본값은 로그인 페이지로 302 입니다. API 를 부르는 쪽에는 리다이렉트가
                // 성공처럼 보이므로 401 로 답합니다.
                .exceptionHandling(handling -> handling
                        .authenticationEntryPoint((request, response, exception) -> write(
                                mapper, response, HttpStatus.UNAUTHORIZED,
                                "UNAUTHORIZED", "로그인이 필요합니다.")));

        return http.build();
    }

    /**
     * 나머지 전부. 지금까지와 똑같이 누구나 부를 수 있습니다.
     *
     * <p>securityMatcher 를 두지 않아 위 체인이 잡지 않은 모든 요청이 여기로 옵니다.
     * /actuator/health 와 Swagger 도 포함됩니다.
     *
     * <p>무상태로 두는 것은 이쪽이 세션을 만들 이유가 없기 때문입니다. 만들게 두면
     * 게임 클라이언트의 요청마다 세션이 하나씩 생겨 메모리에 쌓입니다.
     */
    @Bean
    @Order(2)
    SecurityFilterChain openChain(HttpSecurity http) throws Exception {
        http
                .authorizeHttpRequests(paths -> paths.anyRequest().permitAll())
                .csrf(csrf -> csrf.disable())
                .sessionManagement(session -> session
                        .sessionCreationPolicy(SessionCreationPolicy.STATELESS));

        return http.build();
    }

    /**
     * 화면이 쿠키에서 읽은 값을 그대로 헤더에 넣을 수 있게 합니다.
     *
     * <p>기본 핸들러는 XorCsrfTokenRequestAttributeHandler 입니다. 그것은 요청마다 값을
     * 다르게 보이도록 XOR 로 섞어 두고, 들어온 헤더 값을 다시 풀어서 비교합니다. 그런데
     * <b>쿠키에 저장되는 것은 섞이지 않은 원본</b>이라, 화면이 쿠키를 읽어 그대로 보내면
     * 서버가 그것을 풀다가 엉뚱한 값을 얻고 403 을 냅니다.
     *
     * <p>화면이 가진 것은 쿠키뿐입니다. 그래서 섞지 않는 핸들러를 씁니다. 대가는 BREACH
     * 압축 공격에 대한 방어를 잃는 것인데, 그 공격은 응답이 압축되고 공격자가 같은
     * 응답에 자기 입력을 섞어 넣을 수 있어야 성립합니다. 관리 화면은 로그인한 운영자에게
     * 신고 목록만 보여줍니다.
     *
     * <p>속성 이름을 비우면 지연 발급도 함께 꺼집니다. 요청마다 토큰이 실제로 만들어지고,
     * 그래서 로그인 응답에 쿠키가 실려 나갑니다. 그러지 않으면 아무도 읽지 않은 토큰은
     * 만들어지지 않아 쿠키가 나가지 않습니다.
     *
     * <p>이 선택은 테스트가 지킵니다. AdminAuthTest 가 로그인 응답의 쿠키를 꺼내
     * 헤더에 넣는 방식으로, 즉 화면이 하는 그대로 확인합니다.
     */
    private static CsrfTokenRequestAttributeHandler rawCookieValue() {
        CsrfTokenRequestAttributeHandler handler = new CsrfTokenRequestAttributeHandler();
        handler.setCsrfRequestAttributeName(null);
        return handler;
    }

    @Bean
    PasswordEncoder passwordEncoder() {
        return new BCryptPasswordEncoder();
    }

    /**
     * 관리자 계정. 설정에서 읽어 메모리에 둡니다.
     *
     * <p>테이블을 만들지 않은 이유는 계정이 하나이고 팀이 공유하기 때문입니다.
     * 마이그레이션과 엔티티와 리포지토리를 두어도 담을 행이 하나뿐이라, 늘어나는 것은
     * 파일 수뿐입니다. 사람마다 계정을 나눌 일이 실제로 생기면 그때 테이블을 만드는
     * 편이 낫습니다.
     *
     * <p><b>설정이 없으면 아무도 등록하지 않습니다.</b> 관리자 로그인만 불가능해지고
     * 게임은 그대로 돕니다. 반대로 기본 비밀번호를 심어두면 설정을 잊은 서버가 아는
     * 비밀번호로 열려 있게 됩니다. 실패는 이 방향이어야 합니다.
     */
    @Bean
    UserDetailsService adminAccount(
            @Value("${admin.username:}") String username,
            @Value("${admin.password:}") String password,
            PasswordEncoder encoder) {

        if (username.isBlank() || password.isBlank()) {
            log.warn("관리자 계정이 설정되지 않아 등록하지 않습니다. "
                    + "ADMIN_USERNAME 과 ADMIN_PASSWORD 를 넣으면 관리 API 를 쓸 수 있습니다.");
            return new InMemoryUserDetailsManager();
        }

        return new InMemoryUserDetailsManager(User.withUsername(username)
                .password(encoder.encode(password))
                .roles("ADMIN")
                .build());
    }

    /** 오류 본문을 다른 API 와 같은 모양으로 씁니다. 클라이언트가 code 로 분기합니다. */
    private static void write(ObjectMapper mapper, HttpServletResponse response,
                              HttpStatus status, String code, String message) throws IOException {
        response.setStatus(status.value());
        response.setContentType(MediaType.APPLICATION_JSON_VALUE);
        response.setCharacterEncoding("UTF-8");
        response.getWriter().write(mapper.writeValueAsString(new ErrorResponse(code, message)));
    }
}
