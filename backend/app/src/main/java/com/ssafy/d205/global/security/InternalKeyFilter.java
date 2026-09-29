package com.ssafy.d205.global.security;

import jakarta.servlet.FilterChain;
import jakarta.servlet.ServletException;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;
import org.springframework.web.filter.OncePerRequestFilter;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;

/**
 * {@code /internal/**} 을 공유 키로 잠급니다. 게임 서버만 부르는 경로입니다 (S15P21D205-1027).
 *
 * <p>분석 서비스에도 같은 이름의 필터가 있지만 <b>키는 다릅니다.</b> 그쪽 키(INTERNAL_KEY)는 이
 * 서비스가 분석 서비스를 부를 때 내미는 것이라, 같은 값을 게임 서버에 쥐여 주면 게임 서버가
 * 분석 쪽 내부 경로까지 열 수 있게 됩니다. 탈퇴 익명화가 거기 있습니다. 필요한 권한만 주려고
 * CHAT_INTERNAL_KEY 를 따로 둡니다.
 *
 * <p>키가 없거나 틀리면 <b>404</b> 입니다. 401 이나 403 을 주면 "여기 무언가 있다"를 알려주는
 * 셈이고, 이 경로는 바깥에 존재 자체를 드러낼 이유가 없습니다.
 *
 * <p><b>키가 설정되지 않으면 전부 404 입니다.</b> 열어 두는 기본값은 없습니다. 열려 있는 채로
 * 떠 있는 것보다 채팅 필터와 기록이 동작하지 않는 편이 낫습니다 - 앞은 아무나 목록을 받아 가고
 * 가짜 기록을 심을 수 있는 상태이고, 뒤는 로그에 경고가 남는 상태입니다.
 *
 * <p>SecurityConfig 가 관리자 경로만 잠그고 나머지는 열어 두므로, 이 경로에서는 이 필터가
 * 유일한 벽입니다. nginx 에 /internal 을 열지 않는 것이 첫 번째 벽이고 이것이 두 번째입니다.
 *
 * <p>비교는 상수 시간입니다. 키를 한 글자씩 맞혀 나가는 타이밍 공격을 막습니다.
 */
@Component
@Slf4j
public class InternalKeyFilter extends OncePerRequestFilter {

    public static final String HEADER = "X-Internal-Key";
    static final String PREFIX = "/internal/";

    private final byte[] key;

    public InternalKeyFilter(@Value("${chat.internal-key:}") String key) {
        this.key = key == null || key.isBlank() ? null : key.getBytes(StandardCharsets.UTF_8);
        if (this.key == null) {
            log.warn("chat.internal-key 가 비어 있습니다. /internal 경로는 전부 404 로 답합니다. "
                    + "게임 서버가 금칙어 목록을 받지 못해 채팅 필터가 꺼진 채로 돌고, 신고 조사용 "
                    + "채팅 기록도 쌓이지 않습니다. 운영에서는 CHAT_INTERNAL_KEY 를 설정하세요.");
        }
    }

    @Override
    protected boolean shouldNotFilter(HttpServletRequest request) {
        return !request.getRequestURI().startsWith(PREFIX);
    }

    @Override
    protected void doFilterInternal(HttpServletRequest request, HttpServletResponse response,
                                    FilterChain chain) throws ServletException, IOException {
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
