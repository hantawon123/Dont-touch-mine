package com.ssafy.d205.domain.analytics.config;

import org.springframework.boot.context.properties.ConfigurationProperties;

/**
 * 수집 동작 한도. 값은 application.yml 의 analytics.* 에 있고 테스트가 작게 덮어씁니다.
 *
 * <p>접속 정보는 여기 없습니다. 서비스가 나뉜 뒤로 분석 DB 는 이 서비스의 표준
 * spring.datasource 이고, Flyway 도 자동 설정이 돌립니다(S15P21D205-980).
 *
 * @param queueCapacity          메모리 큐 상한. 넘치면 새 이벤트를 버리고 센다
 * @param flushIntervalMs        큐를 DB 로 비우는 주기
 * @param flushBatchSize         한 번에 넣는 최대 행 수
 * @param flushMaxBatchesPerTick 한 주기에 배치를 최대 몇 번 연달아 넣는가
 * @param rateLimitPerMinute     IP 하나의 분당 요청 상한
 * @param occurredAtPastDays     이벤트 시각이 이보다 과거면 거절
 * @param occurredAtFutureMinutes 이벤트 시각이 이보다 미래면 거절
 */
@ConfigurationProperties(prefix = "analytics")
public record AnalyticsProperties(
        int queueCapacity,
        long flushIntervalMs,
        int flushBatchSize,
        int flushMaxBatchesPerTick,
        int rateLimitPerMinute,
        int occurredAtPastDays,
        int occurredAtFutureMinutes
) {
}
