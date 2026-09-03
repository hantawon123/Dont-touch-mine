package com.ssafy.d205.global.exception;

public class NicknameTakenException extends RuntimeException {

    public NicknameTakenException(String nickname) {
        super("이미 사용 중인 닉네임입니다: " + nickname);
    }
}
