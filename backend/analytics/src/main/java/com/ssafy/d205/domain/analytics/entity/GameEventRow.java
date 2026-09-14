package com.ssafy.d205.domain.analytics.entity;

import java.time.Instant;

/**
 * 큐에 들어가고 DB 에 쓰이는 행. 검증을 통과한 뒤의 값이라 여기서는 아무것도 확인하지 않습니다.
 *
 * <p>요청 DTO 를 그대로 큐에 넣지 않는 이유는 둘입니다. received_at 은 서버가 찍는 값이라
 * 요청에 없고, params 는 JsonNode 가 아니라 이미 문자열로 바뀐 것을 들고 있어야 플러셔가
 * 직렬화를 다시 하지 않습니다. 플러셔는 한 번에 수천 행을 다루므로 거기서 하는 일은 적을수록
 * 좋습니다.
 *
 * @param params JSON 문자열. 없으면 null. MySQL 의 JSON 컬럼은 문자열을 그대로 받습니다
 */
public record GameEventRow(
        Instant occurredAt,
        Instant receivedAt,
        String clientSessionId,
        long clientSeq,
        String roomCode,
        String matchId,
        Long matchTimeMs,
        String userPublicId,
        String eventName,
        String phase,
        String mapId,
        Float posX,
        Float posY,
        Float posZ,
        boolean fromHost,
        short schemaVer,
        String params
) {
}
