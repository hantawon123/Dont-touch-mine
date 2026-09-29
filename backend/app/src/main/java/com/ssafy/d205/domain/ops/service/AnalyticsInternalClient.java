package com.ssafy.d205.domain.ops.service;

import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.http.client.SimpleClientHttpRequestFactory;
import org.springframework.stereotype.Component;
import org.springframework.util.MultiValueMap;
import org.springframework.web.client.HttpClientErrorException;
import org.springframework.web.client.RestClient;

import java.time.Duration;
import java.util.List;
import java.util.Optional;

import com.ssafy.d205.domain.admin.dto.AdminAnalyticsTable;
import com.ssafy.d205.domain.admin.dto.AdminOverview;

/**
 * 분석 서비스의 내부 API 를 부르는 유일한 자리 (S15P21D205-1002).
 *
 * <p>두 서비스는 DB 를 공유하지 않으므로 계정 서비스가 분석에 시킬 일은 전부 이 클래스를 지납니다.
 * 지금은 셋입니다. 탈퇴한 사람의 로그 익명화, 관리 화면 개요 탭의 경기 통계, 분석 탭의 표(976).
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

    /**
     * 분석 탭의 표는 따로 더 기다립니다. 개요 카드의 3초는 "화면이 멈추면 안 된다"에서 나온 값인데, 체류 구역
     * 같은 집계는 경기가 수백 판 쌓이면 그보다 오래 걸릴 수 있고 좌표 응답은 1MB 입니다. 3초로 끊으면 살아 있는
     * 분석 서비스를 "연결할 수 없음"으로 보이게 됩니다. 분석 쪽 SQL 타임아웃(AnalyticsQueryService)보다 길게 두어
     * 그쪽이 먼저 400/500 으로 답하게 합니다.
     */
    private static final Duration TABLE_TIMEOUT = Duration.ofSeconds(20);

    private final RestClient client;
    private final RestClient tableClient;
    private final String key;

    public AnalyticsInternalClient(@Value("${analytics.internal-url:http://localhost:8081}") String baseUrl,
                                   @Value("${analytics.internal-key:}") String key) {
        this.client = RestClient.builder().baseUrl(baseUrl).requestFactory(factory(TIMEOUT)).build();
        this.tableClient = RestClient.builder().baseUrl(baseUrl).requestFactory(factory(TABLE_TIMEOUT)).build();
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
     * 분석 탭의 표 하나 (S15P21D205-976). 질문 이름과 필터를 그대로 넘기고 분석 서비스가 만든 표를 그대로
     * 받습니다. 이 클래스는 질문 이름을 알지 못합니다 - 그 목록은 분석 서비스가 문서에서 읽고, 화면이
     * 고정으로 갖습니다.
     *
     * @param path  {@code /internal/admin/analytics/} 아래 경로. 질문 이름 또는 {@code positions}
     * @param query 그대로 넘길 쿼리. null 값은 부르는 쪽이 이미 뺐습니다
     * @return 분석 서비스가 없거나 거절하면 비어 있고, 화면은 "연결할 수 없음"을 보입니다. 404 도 여기 들어가는데
     *         그건 질문 이름이 문서에 없거나 INTERNAL_KEY 가 두 서비스에서 다른 것이라 경고를 남깁니다
     */
    private static SimpleClientHttpRequestFactory factory(Duration readTimeout) {
        SimpleClientHttpRequestFactory factory = new SimpleClientHttpRequestFactory();
        factory.setConnectTimeout(TIMEOUT);
        factory.setReadTimeout(readTimeout);
        return factory;
    }

    public Optional<AdminAnalyticsTable> fetchTable(String path, MultiValueMap<String, String> query) {
        try {
            Table table = tableClient.get()
                    .uri(builder -> builder.path("/internal/admin/analytics/" + path).queryParams(query).build())
                    .header(KEY_HEADER, key)
                    .retrieve()
                    .body(Table.class);
            return Optional.ofNullable(table).map(t -> new AdminAnalyticsTable(t.columns(), t.rows(), false));
        } catch (HttpClientErrorException.NotFound e) {
            log.warn("분석 서비스가 /internal/admin/analytics/{} 를 404 로 거절했습니다. 질문 이름이 문서에 없거나 "
                    + "INTERNAL_KEY 가 두 서비스에서 다릅니다.", path);
            return Optional.empty();
        } catch (RuntimeException e) {
            log.debug("분석 서비스에서 표 {} 를 받지 못했습니다: {}", path, e.getMessage());
            return Optional.empty();
        }
    }

    /**
     * 분석 서비스의 응답 모양 그대로. {@link AdminAnalyticsTable} 로 바로 받지 않는 이유는 그쪽의 {@code unavailable}
     * 이 응답에 없어서입니다 - 잭슨은 없는 값을 boolean 에 넣지 않고 거절합니다(FAIL_ON_NULL_FOR_PRIMITIVES).
     */
    private record Table(List<String> columns, List<List<Object>> rows) {
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
