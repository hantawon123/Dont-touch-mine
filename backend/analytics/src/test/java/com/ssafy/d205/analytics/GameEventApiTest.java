package com.ssafy.d205.analytics;

import tools.jackson.databind.ObjectMapper;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.RequestBuilder;

import java.time.Duration;
import java.time.Instant;
import java.util.ArrayList;
import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import static com.ssafy.d205.analytics.GameEventFixtures.event;
import static com.ssafy.d205.analytics.GameEventFixtures.fromIp;
import static com.ssafy.d205.analytics.GameEventFixtures.newSessionId;
import static com.ssafy.d205.analytics.GameEventFixtures.params;

import org.springframework.jdbc.core.JdbcTemplate;
import com.ssafy.d205.domain.analytics.controller.GameEventController;
import com.ssafy.d205.domain.analytics.dto.GameEventRequest;
import com.ssafy.d205.support.AnalyticsIntegrationTest;

/**
 * 수집 API 의 계약. 명세는 docs/analytics-events.md 7절입니다.
 *
 * <p>저장은 비동기라 202 뒤에 잠시 기다려야 행이 보입니다. application-test.yml 이 flush 주기를
 * 200ms 로 줄여 두었고, {@link #awaitRows} 가 최대 5초까지 폴링합니다.
 */
class GameEventApiTest extends AnalyticsIntegrationTest {

    private static final String EVENTS = "/api/v1/events";

    @Autowired
    MockMvc mvc;

    @Autowired
    ObjectMapper objectMapper;

    @Autowired
    JdbcTemplate analytics;

    @Test
    @DisplayName("정상 배열을 보내면 202 가 오고 잠시 뒤 game_event 에 행이 생긴다")
    void acceptsAndStores() throws Exception {
        String session = newSessionId();

        mvc.perform(send("10.0.0.1", event(session, 0), event(session, 1), event(session, 2)))
                .andExpect(status().isAccepted());

        assertThat(awaitRows(session, 3)).isEqualTo(3);
    }

    @Test
    @DisplayName("X-User-Id 헤더가 없어도 받는다. 주체는 이벤트마다 들어 있다")
    void doesNotRequireUserHeader() throws Exception {
        // send() 는 헤더를 붙이지 않습니다. 이 테스트는 그 사실을 고정합니다.
        mvc.perform(send("10.0.0.2", event(newSessionId(), 0)))
                .andExpect(status().isAccepted());
    }

    @Test
    @DisplayName("같은 이벤트를 다시 보내도(스풀 재전송) 행은 하나다")
    void retransmissionIsDeduplicated() throws Exception {
        String session = newSessionId();
        GameEventRequest same = event(session, 7);

        mvc.perform(send("10.0.0.3", same)).andExpect(status().isAccepted());
        assertThat(awaitRows(session, 1)).isEqualTo(1);

        mvc.perform(send("10.0.0.3", same)).andExpect(status().isAccepted());
        // 두 번째도 flush 될 시간을 준 뒤 그대로 1인지 봅니다.
        Thread.sleep(600);
        assertThat(countRows(session)).isEqualTo(1);
    }

    @Test
    @DisplayName("시각은 UTC 로 저장된다")
    void storesTimestampsAsUtc() throws Exception {
        // 한 시간 전(범위 검증 안)의 시각을 밀리초까지 보내고, DB 가 돌려주는 문자열이 UTC 표기와
        // 같은지 봅니다. 세션 타임존(KST)으로 저장됐다면 9시간 어긋나 여기서 걸립니다.
        Instant sent = Instant.now().minus(Duration.ofHours(1)).truncatedTo(java.time.temporal.ChronoUnit.MILLIS);
        String session = newSessionId();

        mvc.perform(send("10.0.0.4", event(session, 0, "scene_enter", sent.toEpochMilli(), params("{\"scene\":\"Home\"}"))))
                .andExpect(status().isAccepted());
        awaitRows(session, 1);

        String stored = analytics.queryForObject(
                "SELECT DATE_FORMAT(occurred_at, '%Y-%m-%dT%H:%i:%s.%f') FROM game_event WHERE client_session_id = ?",
                String.class, session);

        // MySQL 의 %f 는 마이크로초 6자리라 밀리초 뒤에 000 이 붙습니다. 앞 23자(밀리초까지)를 비교합니다.
        String expected = java.time.LocalDateTime.ofInstant(sent, java.time.ZoneOffset.UTC)
                .format(java.time.format.DateTimeFormatter.ofPattern("yyyy-MM-dd'T'HH:mm:ss.SSS"));
        assertThat(stored).startsWith(expected);
    }

    @Test
    @DisplayName("빈 배열은 400")
    void rejectsEmptyBatch() throws Exception {
        mvc.perform(post(EVENTS).with(fromIp("10.0.0.5"))
                        .contentType(MediaType.APPLICATION_JSON).content("[]"))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("INVALID_REQUEST"));
    }

    @Test
    @DisplayName("배열이 상한을 넘으면 400")
    void rejectsOversizedBatch() throws Exception {
        String session = newSessionId();
        List<GameEventRequest> tooMany = new ArrayList<>();
        for (int i = 0; i <= GameEventController.MAX_BATCH_SIZE; i++) {
            tooMany.add(event(session, i));
        }

        mvc.perform(send("10.0.0.6", tooMany))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("INVALID_REQUEST"));

        Thread.sleep(400);
        assertThat(countRows(session)).as("거부된 배치는 한 건도 들어가면 안 됩니다").isZero();
    }

    @Test
    @DisplayName("명세에 없는 eventName 이 하나라도 있으면 배치 전체가 400")
    void rejectsUnknownEventName() throws Exception {
        String session = newSessionId();
        GameEventRequest ok = event(session, 0);
        GameEventRequest bad = event(session, 1, "teleported", Instant.now().toEpochMilli(), null);

        mvc.perform(send("10.0.0.7", ok, bad))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("INVALID_REQUEST"))
                .andExpect(jsonPath("$.message").value(org.hamcrest.Matchers.containsString("events[1]")));

        Thread.sleep(400);
        assertThat(countRows(session)).isZero();
    }

    @Test
    @DisplayName("occurredAt 이 7일보다 과거면 400")
    void rejectsTooOldTimestamp() throws Exception {
        long eightDaysAgo = Instant.now().minus(Duration.ofDays(8)).toEpochMilli();

        mvc.perform(send("10.0.0.8", event(newSessionId(), 0, "client_quit", eightDaysAgo, null)))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("INVALID_REQUEST"));
    }

    @Test
    @DisplayName("occurredAt 이 5분보다 미래면 400")
    void rejectsFutureTimestamp() throws Exception {
        long tenMinutesLater = Instant.now().plus(Duration.ofMinutes(10)).toEpochMilli();

        mvc.perform(send("10.0.0.9", event(newSessionId(), 0, "client_quit", tenMinutesLater, null)))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("INVALID_REQUEST"));
    }

    @Test
    @DisplayName("params 가 객체가 아니면 400")
    void rejectsNonObjectParams() throws Exception {
        mvc.perform(send("10.0.0.10", event(newSessionId(), 0, "client_quit", Instant.now().toEpochMilli(), params("[1,2,3]"))))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("INVALID_REQUEST"));
    }

    @Test
    @DisplayName("params 가 너무 크면 400")
    void rejectsHugeParams() throws Exception {
        String big = "{\"blob\":\"" + "x".repeat(3000) + "\"}";

        mvc.perform(send("10.0.0.11", event(newSessionId(), 0, "client_quit", Instant.now().toEpochMilli(), params(big))))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("INVALID_REQUEST"));
    }

    @Test
    @DisplayName("필수 필드가 빠지면 400")
    void rejectsMissingRequiredField() throws Exception {
        // fromHost 가 없습니다. 기본형이었다면 false 로 채워져 조용히 통과했을 것입니다.
        String body = """
                [{"occurredAt": %d, "clientSessionId": "%s", "clientSeq": 0,
                  "eventName": "client_quit", "schemaVer": 1}]
                """.formatted(Instant.now().toEpochMilli(), newSessionId());

        mvc.perform(post(EVENTS).with(fromIp("10.0.0.12"))
                        .contentType(MediaType.APPLICATION_JSON).content(body))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("INVALID_REQUEST"));
    }

    @Test
    @DisplayName("params 없는 이벤트도 받는다")
    void acceptsEventWithoutParams() throws Exception {
        String session = newSessionId();

        mvc.perform(send("10.0.0.13", event(session, 0, "perf_sample", Instant.now().toEpochMilli(), null)))
                .andExpect(status().isAccepted());

        assertThat(awaitRows(session, 1)).isEqualTo(1);
        String params = analytics.queryForObject(
                "SELECT params FROM game_event WHERE client_session_id = ?", String.class, session);
        assertThat(params).isNull();
    }

    private RequestBuilder send(String ip, GameEventRequest... events) throws Exception {
        return send(ip, List.of(events));
    }

    private RequestBuilder send(String ip, List<GameEventRequest> events) throws Exception {
        return post(EVENTS)
                .with(fromIp(ip))
                .contentType(MediaType.APPLICATION_JSON)
                .content(objectMapper.writeValueAsString(events));
    }

    private int countRows(String sessionId) {
        Integer count = analytics.queryForObject(
                "SELECT COUNT(*) FROM game_event WHERE client_session_id = ?", Integer.class, sessionId);
        return count == null ? 0 : count;
    }

    /** 플러셔가 비동기라 최대 5초까지 기다립니다. 기대 개수에 도달하면 바로 돌아옵니다. */
    private int awaitRows(String sessionId, int expected) throws InterruptedException {
        long deadline = System.currentTimeMillis() + 5_000;
        int count = countRows(sessionId);
        while (count < expected && System.currentTimeMillis() < deadline) {
            Thread.sleep(100);
            count = countRows(sessionId);
        }
        return count;
    }
}
