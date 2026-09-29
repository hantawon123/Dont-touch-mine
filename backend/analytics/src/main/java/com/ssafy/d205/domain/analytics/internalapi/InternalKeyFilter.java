package com.ssafy.d205.domain.analytics.internalapi;

import jakarta.servlet.FilterChain;
import jakarta.servlet.ServletException;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;
import org.springframework.web.filter.OncePerRequestFilter;

import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;

/**
 * {@code /internal/**} 을 공유 키로 잠급니다. 계정 서비스만 부르는 경로입니다.
 *
 * <p>키가 없거나 틀리면 <b>404</b> 입니다. 401 이나 403 을 주면 "여기 무언가 있다"를 알려주는 셈이고,
 * 이 경로는 바깥에 존재 자체를 드러낼 이유가 없습니다. nginx 에도 이 경로가 없으므로 정상적으로는
 * compose 네트워크 안의 계정 서비스만 닿습니다. 이 필터는 nginx 설정이 실수로 열렸을 때의 두 번째 벽입니다.
 *
 * <p><b>키가 설정되지 않으면 전부 404 입니다.</b> 열어 두는 기본값은 없습니다. 계정 토큰 서명이 비밀
 * 없이 통과(fail-open)하는 것과 반대인데, 그쪽은 없으면 게임이 막히는 문제이고 이쪽은 없으면 탈퇴
 * 익명화와 관리 화면 카드 하나가 안 되는 문제라 무게가 다릅니다. 기동 로그에 경고를 남깁니다.
 *
 * <p>비교는 상수 시간입니다. 키를 한 글자씩 맞혀 나가는 타이밍 공격을 막습니다.
 */
@Component
@Slf4j
public class InternalKeyFilter extends OncePerRequestFilter {

    public static final String HEADER = "X-Internal-Key";
    static final String PREFIX = "/internal/";

    private final byte[] key;

    public InternalKeyFilter(@Value("${internal.key:}") String key) {
        this.key = key == null || key.isBlank() ? null : key.getBytes(StandardCharsets.UTF_8);
        if (this.key == null) {
            log.warn("internal.key 가 비어 있습니다. /internal 경로는 전부 404 로 답합니다. "
                    + "탈퇴 로그 익명화와 관리 화면 경기 통계가 동작하지 않습니다. 운영에서는 INTERNAL_KEY 를 설정하세요.");
        }
    }

    @Override
    protected boolean shouldNotFilter(HttpServletRequest request) {
        return !request.getRequestURI().startsWith(PREFIX);
    }

    @Override
    protected void doFilterInternal(HttpServletRequest request, HttpServletResponse response,
                                    FilterChain chain) throws ServletException, java.io.IOException {
        if (!matches(request.getHeader(HEADER))) {
            response.setStatus(HttpServletResponse.SC_NOT_FOUND);
            return;
        }
        chain.doFilter(request, response);
    }

    boolean matches(String presented) {
        if (key == null || presented == null) {
            return false;
        }
        return MessageDigest.isEqual(key, presented.getBytes(StandardCharsets.UTF_8));
    }
}
