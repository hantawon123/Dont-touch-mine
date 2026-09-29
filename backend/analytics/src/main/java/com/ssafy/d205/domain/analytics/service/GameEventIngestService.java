package com.ssafy.d205.domain.analytics.service;

import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import tools.jackson.databind.JsonNode;

import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.util.ArrayList;
import java.util.List;

import com.ssafy.d205.domain.analytics.config.AnalyticsProperties;
import com.ssafy.d205.domain.analytics.dto.GameEventRequest;
import com.ssafy.d205.domain.analytics.entity.GameEventName;
import com.ssafy.d205.domain.analytics.entity.GameEventRow;
import com.ssafy.d205.domain.analytics.exception.EventBatchRejectedException;
import com.ssafy.d205.domain.analytics.exception.RateLimitedException;

/**
 * 배치를 받아 검증하고 큐에 넣습니다. DB 를 기다리지 않습니다.
 *
 * <p>여기서 하는 검증은 형식 검증(DTO 애너테이션)이 못 하는 것들입니다.
 * <ul>
 *   <li>이름이 명세 목록에 있는가</li>
 *   <li>occurred_at 이 지금-7일 ~ 지금+5분 안인가. 파티션 키가 클라이언트 값이라 1970년 시각
 *       하나가 들어오면 파티션이 없어 그 배치의 insert 가 통째로 실패합니다</li>
 *   <li>params 가 객체이고 크기 상한 안인가</li>
 * </ul>
 *
 * <p><b>하나라도 틀리면 배치 전체를 거부합니다.</b> 일부만 받으면 클라이언트가 무엇이 들어갔는지
 * 알 수 없어 재전송을 판단하지 못합니다. 클라이언트는 400 을 재전송하지 않습니다(명세 7절).
 *
 * <p>시각은 배치 안에서 한 번만 읽습니다(TimeProvider 와 같은 이유). received_at 도 그 값이라
 * 한 배치의 행들은 같은 received_at 을 갖습니다. 그게 맞습니다. 같은 요청으로 왔으니까요.
 */
@Service
@RequiredArgsConstructor
public class GameEventIngestService {

    /** params JSON 문자열의 상한. 한 이벤트의 추가 값이 이보다 크면 설계가 잘못된 것입니다. */
    static final int MAX_PARAMS_BYTES = 2048;

    private final AnalyticsProperties properties;
    private final GameEventBuffer buffer;
    private final IpRateLimiter rateLimiter;
    private final Clock clock;

    /**
     * @return 큐가 차서 버린 개수. 컨트롤러는 쓰지 않고 202 를 냅니다. 테스트가 봅니다
     * @throws RateLimitedException 이 IP 가 분당 허용량을 넘김
     * @throws EventBatchRejectedException 배치 안의 이벤트가 규칙을 어김
     */
    public int ingest(String ip, List<GameEventRequest> events) {
        if (!rateLimiter.tryAcquire(ip)) {
            throw new RateLimitedException(ip);
        }

        Instant now = clock.instant();
        Instant oldest = now.minus(Duration.ofDays(properties.occurredAtPastDays()));
        Instant newest = now.plus(Duration.ofMinutes(properties.occurredAtFutureMinutes()));

        List<GameEventRow> rows = new ArrayList<>(events.size());
        for (int i = 0; i < events.size(); i++) {
            rows.add(toRow(i, events.get(i), now, oldest, newest));
        }

        return buffer.offerAll(rows);
    }

    private static GameEventRow toRow(int index, GameEventRequest e,
                                      Instant receivedAt, Instant oldest, Instant newest) {
        if (!GameEventName.isKnown(e.eventName())) {
            throw new EventBatchRejectedException(index, "알 수 없는 eventName '" + e.eventName() + "'");
        }

        Instant occurredAt = Instant.ofEpochMilli(e.occurredAt());
        if (occurredAt.isBefore(oldest) || occurredAt.isAfter(newest)) {
            throw new EventBatchRejectedException(index,
                    "occurredAt 이 허용 범위(" + oldest + " ~ " + newest + ") 밖입니다: " + occurredAt);
        }

        String params = serializeParams(index, e.params());

        return new GameEventRow(
                occurredAt,
                receivedAt,
                e.clientSessionId(),
                e.clientSeq(),
                e.roomCode(),
                e.matchId(),
                e.matchTimeMs(),
                e.userPublicId(),
                e.eventName(),
                e.phase(),
                e.mapId(),
                e.posX(),
                e.posY(),
                e.posZ(),
                e.fromHost(),
                e.schemaVer(),
                params);
    }

    /**
     * params 를 문자열로 바꿉니다. 여기서 한 번 직렬화해 두면 플러셔는 문자열만 넘깁니다.
     *
     * <p>객체만 받습니다. 배열이나 스칼라를 params 로 보내는 것은 명세에 없고, 받아 두면 나중에
     * JSON_EXTRACT('$.key') 가 조용히 NULL 을 돌려줘 집계가 틀린 채 통과합니다.
     */
    private static String serializeParams(int index, JsonNode params) {
        if (params == null || params.isNull()) {
            return null;
        }
        if (!params.isObject()) {
            throw new EventBatchRejectedException(index, "params 는 JSON 객체여야 합니다.");
        }
        String json = params.toString();
        if (json.getBytes(java.nio.charset.StandardCharsets.UTF_8).length > MAX_PARAMS_BYTES) {
            throw new EventBatchRejectedException(index, "params 가 " + MAX_PARAMS_BYTES + " 바이트를 넘습니다.");
        }
        return json;
    }
}
