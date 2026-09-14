package com.ssafy.d205.global.config;

import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.boot.context.properties.bind.DefaultValue;

import java.util.List;

/**
 * 실시간 알림 채널의 설정. {@code notifications.*}
 *
 * @param allowedOrigins 핸드셰이크에서 받아 줄 Origin. 브라우저(WebGL)만 이 헤더를 보내고,
 *                       Unity 스탠드얼론은 보내지 않아 검사 대상이 아닙니다. 패턴을 쓸 수
 *                       있어 로컬 빌드의 임의 포트는 {@code http://localhost:[*]} 로 덮습니다
 * @param helloTimeoutMs 연결 뒤 HELLO 를 기다리는 시간. 넘으면 끊습니다. 테스트는 짧게 둡니다
 * @param pingIntervalMs 서버가 ping 을 보내는 주기. nginx proxy_read_timeout 보다 짧아야 합니다
 */
@ConfigurationProperties(prefix = "notifications")
public record NotificationProperties(
        @DefaultValue({ "https://j15d205.p.ssafy.io", "http://localhost:[*]" }) List<String> allowedOrigins,
        @DefaultValue("5000") long helloTimeoutMs,
        @DefaultValue("30000") long pingIntervalMs
) {
}
