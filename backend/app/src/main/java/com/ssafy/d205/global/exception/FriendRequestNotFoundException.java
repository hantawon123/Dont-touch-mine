package com.ssafy.d205.global.exception;

public class FriendRequestNotFoundException extends RuntimeException {

    public FriendRequestNotFoundException() {
        super("친구 요청을 찾을 수 없습니다.");
    }
}
