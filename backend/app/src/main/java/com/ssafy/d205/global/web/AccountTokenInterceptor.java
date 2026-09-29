package com.ssafy.d205.global.web;

import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Component;
import org.springframework.web.servlet.HandlerInterceptor;

import com.ssafy.d205.global.exception.UnauthorizedException;
import com.ssafy.d205.global.security.AccountTokens;

/**
 * X-User-Id 가 정말 그 사람의 것인지 확인합니다.
 *
 * <p>X-User-Id 만으로는 식별이지 인증이 아닙니다. 같은 방에 있었던 사람은 서로의 userId 를
 * 알고, 그 값을 헤더에 넣으면 그 사람 행세를 할 수 있습니다. 그래서 X-User-Id 가 붙은 요청은
 * 계정 응답에서 받은 토큰을 {@code X-Account-Token} 으로 함께 보내야 하고, 여기서 그 짝이
 * 맞는지 봅니다. 토큰은 서버 비밀로 userId 를 서명한 값이라 비밀을 모르면 남의 것을 만들 수
 * 없습니다({@link AccountTokens}).
 *
 * <p><b>헤더가 없는 요청은 그냥 통과시킵니다.</b> 계정 발급은 토큰을 받기 전이고 플레이 로그는
 * 원래 X-User-Id 를 보내지 않습니다. 어느 요청이 신원을 요구하는지는 컨트롤러가
 * {@code @RequestHeader} 로 정하고, 여기는 "신원을 댔으면 증명하라"만 봅니다.
 *
 * <p><b>비밀이 없는 서버는 검사하지 않습니다.</b> 그 서버는 토큰을 발급한 적도 없으므로
 * 요구하면 아무도 못 들어옵니다. 로컬 개발이 이 상태입니다.
 *
 * <p>정지 검사({@link SuspensionInterceptor})보다 앞에 걸립니다. 증명된 userId 만 정지 여부를
 * 보므로 남의 id 를 빌려 정지 검사를 피하는 길이 닫히고, 토큰 없이 남의 정지 여부를 밖에서
 * 알아내는 길도 닫힙니다.
 *
 * <p>DB 를 보지 않습니다. 계산 한 번과 비교 한 번입니다. "그런 계정이 없다"는 여전히
 * 컨트롤러가 404 ACCOUNT_NOT_FOUND 로 답할 일이고, 없는 계정의 올바른 토큰은 서버 비밀
 * 없이는 만들 수 없으므로 그 경로로 뚫리는 것은 없습니다.
 */
@Component
@RequiredArgsConstructor
public class AccountTokenInterceptor implements HandlerInterceptor {

    static final String USER_ID_HEADER = "X-User-Id";
    static final String TOKEN_HEADER = "X-Account-Token";

    private final AccountTokens tokens;

    @Override
    public boolean preHandle(HttpServletRequest request, HttpServletResponse response, Object handler) {
        if (!tokens.isEnabled()) {
            return true;
        }

        String userId = request.getHeader(USER_ID_HEADER);
        if (userId == null || userId.isBlank()) {
            return true;
        }

        // 예외를 던집니다. 응답을 직접 쓰면 오류 본문의 모양이 GlobalExceptionHandler 가
        // 만드는 것과 갈라집니다. 인터셉터에서 던진 예외도 그 핸들러가 잡습니다.
        if (!tokens.matches(userId, request.getHeader(TOKEN_HEADER))) {
            throw new UnauthorizedException();
        }

        return true;
    }
}
