package com.ssafy.d205.global.exception;

/**
 * 정지된 계정이 요청했습니다. 403 SUSPENDED 로 나갑니다.
 *
 * <p>404 가 아니라 403 인 이유는 계정이 있기 때문입니다. 없는 것처럼 꾸미면 클라이언트가
 * 계정 발급을 다시 부르고, 발급도 막혀 있으므로 같은 자리를 맴돕니다. 사용자에게도
 * "내 계정이 사라졌다"로 보이는데 사실이 아닙니다.
 *
 * <p><b>사유를 응답에 담지 않습니다.</b> 정지 사유는 신고 내용에서 나오므로, 그대로
 * 돌려주면 누가 무엇을 신고했는지가 드러납니다. 사유는 운영자 화면에서만 봅니다.
 */
public class SuspendedAccountException extends RuntimeException {

    public SuspendedAccountException() {
        super("정지된 계정입니다.");
    }
}
