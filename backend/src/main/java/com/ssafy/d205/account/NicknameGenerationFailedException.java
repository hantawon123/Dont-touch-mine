package com.ssafy.d205.account;

public class NicknameGenerationFailedException extends RuntimeException {

    public NicknameGenerationFailedException(int attempts) {
        super("닉네임 자동 생성이 " + attempts + "회 연속 충돌했습니다.");
    }
}
