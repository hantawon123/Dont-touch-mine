package com.ssafy.d205.global.exception;

/**
 * 상대를 찾을 수 없습니다.
 *
 * <p>부르는 사람이 없는 경우(UnknownCallerException)와 코드를 구분합니다. 전자는
 * 클라이언트가 계정 발급을 다시 불러야 하고, 후자는 "그 사용자가 없습니다"를 보여주면
 * 됩니다. 같은 코드로 내보내면 클라이언트가 갈라서 처리할 수 없습니다.
 *
 * <p><b>차단 관계일 때도 이 예외입니다.</b> "당신은 차단당했습니다"를 알려주면 차단한
 * 사람이 드러납니다. 없는 사용자와 구분되지 않게 두는 것이 의도입니다.
 */
public class TargetUserNotFoundException extends RuntimeException {

    public TargetUserNotFoundException(String userId) {
        super("상대를 찾을 수 없습니다: " + userId);
    }
}
