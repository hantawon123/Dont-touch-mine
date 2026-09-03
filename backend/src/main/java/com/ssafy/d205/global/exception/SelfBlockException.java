package com.ssafy.d205.global.exception;

/**
 * 자기 자신을 차단하려 했습니다.
 *
 * <p>DB의 ck_user_blocks_not_self 가 막지만, 제약 위반은 클라이언트에게 "요청이 다른
 * 데이터와 충돌했다"로만 전달됩니다. 이유를 알려주기 위해 먼저 걸러냅니다.
 */
public class SelfBlockException extends RuntimeException {

    public SelfBlockException() {
        super("자기 자신을 차단할 수 없습니다.");
    }
}
