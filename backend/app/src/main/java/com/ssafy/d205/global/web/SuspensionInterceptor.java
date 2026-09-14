package com.ssafy.d205.global.web;

import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Component;
import org.springframework.web.servlet.HandlerInterceptor;

import com.ssafy.d205.domain.user.repository.UserRepository;
import com.ssafy.d205.global.exception.SuspendedAccountException;

/**
 * 정지된 계정의 요청을 막습니다.
 *
 * <p>이 프로젝트에는 로그인이 없습니다. 앱 시작 때 기기 식별자로 계정을 발급받고
 * (`POST /api/v1/accounts`), 그 뒤로는 모든 요청에 {@code X-User-Id} 를 붙입니다.
 * 그래서 "로그인을 막는다"는 두 곳을 막는다는 뜻입니다 - 발급은
 * {@code AccountService.issue} 가, 나머지 전부를 여기가 막습니다.
 *
 * <p>발급만 막으면 반쪽입니다. 발급은 앱을 켤 때 한 번만 불리므로 이미 실행 중인
 * 클라이언트는 헤더를 이미 들고 있습니다. 정지를 눌러도 그 사람은 앱을 끄기 전까지
 * 그대로 플레이합니다.
 *
 * <p><b>인증이 아니라 정지 검사입니다.</b> 헤더가 없는 요청은 그냥 통과시킵니다.
 * 여기서 헤더를 강제하면 헤더 없이 부르도록 만든 엔드포인트(계정 발급, 헬스 체크)가
 * 전부 막히고, 그건 이 인터셉터가 할 판단이 아닙니다. 헤더가 필요한 곳은 컨트롤러가
 * {@code @RequestHeader} 로 요구하고 없으면 400 MISSING_HEADER 가 나갑니다.
 *
 * <p>모르는 값이 와도 통과시킵니다. "그런 계정 없음"은 컨트롤러가 404
 * ACCOUNT_NOT_FOUND 로 답할 일이고, 여기서 403 으로 바꾸면 없는 계정과 정지된 계정이
 * 구분되지 않습니다.
 *
 * <p><b>요청마다 조회가 한 번 늘어납니다.</b> public_id 유니크 인덱스를 타는 단건
 * 조회라 지금 규모에서는 문제없지만 공짜는 아닙니다. 무거워지면 여기가 캐시를 얹는
 * 자리이고, 그때는 정지가 반영되기까지의 지연을 받아들이는 셈이 됩니다.
 */
@Component
@RequiredArgsConstructor
public class SuspensionInterceptor implements HandlerInterceptor {

    static final String USER_ID_HEADER = "X-User-Id";

    private final UserRepository userRepository;

    @Override
    public boolean preHandle(HttpServletRequest request, HttpServletResponse response, Object handler) {
        String userId = request.getHeader(USER_ID_HEADER);
        if (userId == null || userId.isBlank()) {
            return true;
        }

        // 예외를 던집니다. 여기서 응답을 직접 쓰면 오류 본문의 모양이
        // GlobalExceptionHandler 가 만드는 것과 갈라지고, 클라이언트는 같은 실패를 두 가지
        // 형식으로 받게 됩니다. 인터셉터에서 던진 예외도 그 핸들러가 잡습니다.
        userRepository.findByPublicId(userId)
                .filter(user -> user.isSuspended())
                .ifPresent(user -> {
                    throw new SuspendedAccountException();
                });

        return true;
    }
}
