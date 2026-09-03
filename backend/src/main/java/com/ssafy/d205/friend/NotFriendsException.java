package com.ssafy.d205.friend;

public class NotFriendsException extends RuntimeException {

    public NotFriendsException() {
        super("친구가 아닙니다.");
    }
}
