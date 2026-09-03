package com.ssafy.d205;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import com.ssafy.d205.support.IntegrationTest;

class D205ApplicationTests extends IntegrationTest {

    /**
     * 컨텍스트가 뜨는 것만 확인하는 테스트가 아닙니다. Flyway가 마이그레이션을 전부
     * 적용하고, 그다음 Hibernate의 ddl-auto=validate가 엔티티와 실제 컬럼이 맞는지
     * 검사합니다. 컬럼명이나 타입이 어긋나면 여기서 기동이 실패합니다.
     */
    @Test
    @DisplayName("애플리케이션 컨텍스트가 뜬다")
    void contextLoads() {
    }
}
