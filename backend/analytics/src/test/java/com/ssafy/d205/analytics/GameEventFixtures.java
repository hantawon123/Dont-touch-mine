package com.ssafy.d205.analytics;

import org.springframework.test.web.servlet.request.RequestPostProcessor;
import tools.jackson.databind.JsonNode;
import tools.jackson.databind.ObjectMapper;

import java.time.Instant;
import java.util.UUID;

import com.ssafy.d205.domain.analytics.dto.GameEventRequest;

/**
 * 분석 테스트가 함께 쓰는 이벤트 생성기.
 *
 * <p>IP 를 테스트마다 다르게 주는 것이 중요합니다. MockMvc 의 기본 remoteAddr 은 늘 127.0.0.1 이라
 * 여러 테스트가 한 IP 로 보이고, 레이트 리밋이 스위트 중간에 걸려 무관한 테스트가 429 로
 * 깨집니다. 각 테스트가 {@link #fromIp(String)} 로 자기 IP 를 씁니다.
 */
final class GameEventFixtures {

    private static final ObjectMapper JSON = new ObjectMapper();

    private GameEventFixtures() {
    }

    /** 지금 시각의 client_quit 이벤트. 세션과 순번은 호출자가 정합니다. */
    static GameEventRequest event(String sessionId, long seq) {
        return event(sessionId, seq, "client_quit", Instant.now().toEpochMilli(), params("{\"scene\":\"Lobby\",\"reason\":\"NORMAL\"}"));
    }

    static GameEventRequest event(String sessionId, long seq, String eventName, long occurredAt, JsonNode params) {
        return new GameEventRequest(
                occurredAt,
                sessionId,
                seq,
                "ABC234",
                null,
                null,
                UUID.randomUUID().toString(),
                eventName,
                "Searching",
                "basement",
                1.5f, 0f, -2.25f,
                false,
                (short) 1,
                params);
    }

    static JsonNode params(String json) {
        return JSON.readTree(json);
    }

    static String newSessionId() {
        return UUID.randomUUID().toString();
    }

    /** 요청의 출처 IP 를 지정합니다. 레이트 리밋이 IP 단위라 테스트마다 달라야 합니다. */
    static RequestPostProcessor fromIp(String ip) {
        return request -> {
            request.setRemoteAddr(ip);
            return request;
        };
    }
}
