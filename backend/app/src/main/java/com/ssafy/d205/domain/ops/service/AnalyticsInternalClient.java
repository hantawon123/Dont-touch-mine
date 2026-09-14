package com.ssafy.d205.domain.ops.service;

import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.http.client.SimpleClientHttpRequestFactory;
import org.springframework.stereotype.Component;
import org.springframework.web.client.RestClient;

import java.time.Duration;
import java.util.Optional;

import com.ssafy.d205.domain.admin.dto.AdminOverview;

/**
 * 분석 서비스의 내부 API 를 부르는 유일한 자리 (S15P21D205-1002).
 *
 * <p>두 서비스는 DB 를 공유하지 않으므로 계정 서비스가 분석에 시킬 일은 전부 이 클래스를 지납니다.
 * 지금은 둘입니다. 탈퇴한 사람의 로그 익명화, 관리 화면 개요 탭의 경기 통계.
 *
 * <p><b>실패해도 예외를 밖으로 내지 않습니다.</b> 이 클래스가 부르는 쪽은 탈퇴 처리와 관리 화면인데,
 * 분석 서비스가 죽었다고 탈퇴가 실패하거나 관리 화면이 안 열리면 서비스를 나눈 이유가 없습니다.
 * 결과는 boolean 과 Optional 로 돌려주고, 부르는 쪽이 재시도(익명화)나 빈 카드(통계)로 대응합니다.
 *
 * <p>타임아웃이 짧습니다. 관리 화면 요청 안에서 부르므로 분석이 느려도 화면이 몇 초씩 멈추면 안 됩니다.
 *
 * <p>주소는 compose 네트워크의 서비스 이름(http://analytics:8080)이고 로컬 기본값은 8081 입니다.
 * 키는 분석 서비스의 internal.key 와 같은 값이어야 하며 .env 의 INTERNAL_KEY 하나를 둘이 읽습니다.
 */
@Component
@Slf4j
public class AnalyticsInternalClient {

    static final String KEY_HEADER = "X-Internal-Key";
    private static final Duration TIMEOUT = Duration.ofSeconds(3);

    private final RestClient client;
    private final String key;

    public AnalyticsInternalClient(@Value("${analytics.internal-url:http://localhost:8081}") String baseUrl,
                                   @Value("${analytics.internal-key:}") String key) {
        SimpleClientHttpRequestFactory factory = new SimpleClientHttpRequestFactory();
        factory.setConnectTimeout(TIMEOUT);
        factory.setReadTimeout(TIMEOUT);
        this.client = RestClient.builder().baseUrl(baseUrl).requestFactory(factory).build();
        this.key = key == null ? "" : key;
        if (this.key.isBlank()) {
            log.warn("analytics.internal-key 가 비어 있습니다. 분석 서비스가 내부 요청을 전부 404 로 거절하므로 "
                    + "탈퇴 로그 익명화와 개요 탭 경기 통계가 동작하지 않습니다. 운영에서는 INTERNAL_KEY 를 설정하세요.");
        }
    }

    /**
     * 그 사람의 플레이 로그에서 userId 를 지우라고 요청합니다.
     *
     * @return 분석 서비스가 200 으로 답했으면 true. 연결 실패·타임아웃·4xx·5xx 는 전부 false 이고 부르는
     *         쪽이 재시도 큐에 넣습니다. 지운 행 수는 여기서 관심사가 아닙니다 - 0 행도 성공입니다(멱등)
     */
    public boolean eraseUserEvents(String userId) {
        try {
            client.delete()
                    .uri("/internal/users/{userId}/events", userId)
                    .header(KEY_HEADER, key)
                    .retrieve()
                    .toBodilessEntity();
            return true;
        } catch (RuntimeException e) {
            log.warn("분석 서비스에 탈퇴 익명화를 요청하지 못했습니다. 재시도합니다. 원인: {}", e.getMessage());
            return false;
        }
    }

    /**
     * 개요 탭 경기 통계. 분석 서비스가 없거나 거절하면 비어 있고, 화면은 그 카드만 비웁니다.
     */
    public Optional<AdminOverview.MatchStats> fetchSummary() {
        try {
            AdminOverview.MatchStats stats = client.get()
                    .uri("/internal/admin/summary")
                    .header(KEY_HEADER, key)
                    .retrieve()
                    .body(AdminOverview.MatchStats.class);
            return Optional.ofNullable(stats);
        } catch (RuntimeException e) {
            log.debug("분석 서비스에서 경기 통계를 받지 못했습니다: {}", e.getMessage());
            return Optional.empty();
        }
    }
}
