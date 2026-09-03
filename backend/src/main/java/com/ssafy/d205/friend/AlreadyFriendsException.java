package com.ssafy.d205.friend;

public class AlreadyFriendsException extends RuntimeException {

    public AlreadyFriendsException() {
        super("이미 친구입니다.");
    }
}
