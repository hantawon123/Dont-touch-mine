package com.ssafy.d205.analytics;

import tools.jackson.databind.ObjectMapper;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.test.context.TestPropertySource;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.RequestBuilder;

import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import static com.ssafy.d205.analytics.GameEventFixtures.event;
import static com.ssafy.d205.analytics.GameEventFixtures.fromIp;
import static com.ssafy.d205.analytics.GameEventFixtures.newSessionId;

import org.springframework.jdbc.core.JdbcTemplate;
import com.ssafy.d205.domain.analytics.config.AnalyticsProperties;
import com.ssafy.d205.domain.analytics.service.GameEventBuffer;
import com.ssafy.d205.domain.analytics.service.GameEventFlusher;
import com.ssafy.d205.support.AnalyticsIntegrationTest;

/**
 * 한도가 약속대로 동작하는지 봅니다. 상한 큐는 별도 서비스로 나누지 않은 전제이고, 레이트 리밋은
 * 인증 없는 엔드포인트의 유일한 남용 방어입니다.
 *
 * <p>큐 용량 3, 레이트 리밋 분당 5, flush 주기 한 시간인 별도 컨텍스트입니다. 운영값(10만, 600)을
 * 실제로 채우는 테스트는 느리고, 플러셔가 도는 중에는 "가득 찬 큐"를 재현할 수 없습니다.
 * flush 는 직접 부릅니다.
 */
@TestPropertySource(properties = {
        "analytics.queue-capacity=3",
        "analytics.rate-limit-per-minute=5",
        "analytics.flush-interval-ms=3600000"
})
class GameEventQueueTest extends AnalyticsIntegrationTest {

    @Autowired
    MockMvc mvc;

    @Autowired
    ObjectMapper objectMapper;

    @Autowired
    GameEventBuffer buffer;

    @Autowired
    GameEventFlusher flusher;

    @Autowired
    JdbcTemplate analytics;

    @Autowired
    AnalyticsProperties properties;

    @Test
    @DisplayName("큐가 가득 차도 API 는 블로킹 없이 202 를 주고, 넘친 만큼만 버린다")
    void fullQueueDropsWithoutBlocking() throws Exception {
        flusher.flushOnce();
        String session = newSessionId();
        long droppedBefore = buffer.droppedCount();

        // 용량 3에 5건. 앞의 3건은 들어가고 2건은 버려져야 합니다.
        List<?> five = List.of(event(session, 0), event(session, 1), event(session, 2),
                event(session, 3), event(session, 4));

        long started = System.currentTimeMillis();
        mvc.perform(send("10.1.0.1", five)).andExpect(status().isAccepted());
        long elapsed = System.currentTimeMillis() - started;

        assertThat(elapsed).as("가득 찬 큐에서 요청이 기다리면 안 됩니다").isLessThan(2_000);
        assertThat(buffer.droppedCount() - droppedBefore).isEqualTo(2);
        assertThat(buffer.size()).isEqualTo(3);

        // 직접 비우면 들어간 3건만 저장됩니다.
        int flushed = flusher.flushOnce();
        assertThat(flushed).isEqualTo(3);
        assertThat(flusher.failedCount()).isZero();

        Integer stored = analytics.queryForObject(
                "SELECT COUNT(*) FROM game_event WHERE client_session_id = ?", Integer.class, session);
        assertThat(stored).isEqualTo(3);
        assertThat(buffer.isEmpty()).isTrue();
    }

    @Test
    @DisplayName("비운 뒤에는 다시 받는다")
    void acceptsAgainAfterFlush() throws Exception {
        flusher.flushOnce();
        String session = newSessionId();
        long droppedBefore = buffer.droppedCount();

        mvc.perform(send("10.1.0.2", List.of(event(session, 0)))).andExpect(status().isAccepted());

        assertThat(buffer.droppedCount()).isEqualTo(droppedBefore);
        assertThat(flusher.flushOnce()).isEqualTo(1);
    }

    @Test
    @DisplayName("한 IP 가 분당 허용량을 넘기면 429 RATE_LIMITED, 다른 IP 는 영향이 없다")
    void rateLimitsPerIp() throws Exception {
        String ip = "10.1.99.1";
        int allowed = properties.rateLimitPerMinute();

        for (int i = 0; i < allowed; i++) {
            flusher.flushOnce(); // 큐 용량이 3이라 매번 비워 줍니다. 여기서 보는 것은 레이트 리밋입니다.
            mvc.perform(send(ip, List.of(event(newSessionId(), 0)))).andExpect(status().isAccepted());
        }

        mvc.perform(send(ip, List.of(event(newSessionId(), 0))))
                .andExpect(status().isTooManyRequests())
                .andExpect(jsonPath("$.code").value("RATE_LIMITED"));

        flusher.flushOnce();
        mvc.perform(send("10.1.99.2", List.of(event(newSessionId(), 0)))).andExpect(status().isAccepted());
    }

    private RequestBuilder send(String ip, List<?> events) throws Exception {
        return post("/api/v1/events")
                .with(fromIp(ip))
                .contentType(MediaType.APPLICATION_JSON)
                .content(objectMapper.writeValueAsString(events));
    }
}
