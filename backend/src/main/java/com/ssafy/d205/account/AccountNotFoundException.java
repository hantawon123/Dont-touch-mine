package com.ssafy.d205.account;

public class AccountNotFoundException extends RuntimeException {

    public AccountNotFoundException(String userId) {
        super("계정을 찾을 수 없습니다: " + userId);
    }
}
