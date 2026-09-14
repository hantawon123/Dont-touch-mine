package com.ssafy.d205.global.exception;

public class SelfFriendRequestException extends RuntimeException {

    public SelfFriendRequestException() {
        super("자기 자신에게 친구 요청을 보낼 수 없습니다.");
    }
}
