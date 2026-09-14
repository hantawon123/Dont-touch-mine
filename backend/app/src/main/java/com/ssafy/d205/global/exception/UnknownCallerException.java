package com.ssafy.d205.global.exception;

/**
 * X-User-Id 헤더가 가리키는 계정이 없습니다.
 *
 * <p>여러 도메인이 함께 쓰기 때문에 global 에 있습니다. 계정·유저 검색·친구 요청이
 * 모두 호출자를 확인하는데, 어느 한 도메인에 두면 다른 도메인이 그 패키지를 가져다
 * 써야 합니다.
 *
 * <p>오류 코드는 <b>ACCOUNT_NOT_FOUND</b> 로 유지합니다. 클래스 이름과 다른 이유는,
 * 이름은 "누가 부르는지 모르겠다"는 서버 쪽 사정이고 코드는 "계정을 다시 발급받아라"는
 * 클라이언트 쪽 조치이기 때문입니다. 코드를 바꾸면 이미 붙은 클라이언트가 깨집니다.
 *
 * <p>상대를 찾을 수 없는 경우는 TargetUserNotFoundException 입니다. 둘을 구분하는
 * 이유는 클라이언트의 대응이 다르기 때문입니다.
 */
public class UnknownCallerException extends RuntimeException {

    public UnknownCallerException(String userId) {
        super("계정을 찾을 수 없습니다: " + userId);
    }
}
