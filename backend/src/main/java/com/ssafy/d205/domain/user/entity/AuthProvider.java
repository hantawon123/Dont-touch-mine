package com.ssafy.d205.domain.user.entity;

/**
 * 계정에 연결할 수 있는 외부 신원의 종류입니다.
 *
 * <p>DB에는 VARCHAR(20) 문자열로 저장합니다. MySQL ENUM을 쓰지 않는 이유는 값을
 * 추가할 때마다 ALTER TABLE이 필요하고 JPA의 @Enumerated(STRING)과도 맞지 않기
 * 때문입니다. 여기에 상수를 추가하면 마이그레이션 없이 바로 쓸 수 있습니다.
 */
public enum AuthProvider {
    DEVICE,
    STEAM,
    EPIC
}
