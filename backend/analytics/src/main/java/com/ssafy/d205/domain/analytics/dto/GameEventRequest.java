package com.ssafy.d205.domain.analytics.dto;

import jakarta.validation.constraints.Max;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Pattern;
import jakarta.validation.constraints.Size;
import tools.jackson.databind.JsonNode;

/**
 * 이벤트 하나. docs/analytics-events.md 2절의 공통 봉투입니다.
 *
 * <p>형식 검증만 여기서 합니다. 이름이 목록에 있는지, 시각이 범위 안인지, params 가 너무 크지
 * 않은지는 GameEventIngestService 가 봅니다. 애너테이션으로 표현할 수 없거나 설정값에 기대는
 * 검사이기 때문입니다.
 *
 * <p>NotNull 을 감싼 형(Long, Boolean, Short)에 붙이는 이유는 UpdateSearchableRequest 와 같습니다.
 * 기본형이면 빠진 값이 0 이나 false 로 조용히 채워집니다.
 *
 * @param occurredAt   클라이언트 시각, UTC epoch ms
 * @param clientSessionId 보낸 앱 실행의 UUID
 * @param clientSeq    세션 안에서 0부터 단조 증가. 재전송 중복 제거의 키
 * @param roomCode     Photon 방 코드. 방에 없으면 null
 * @param matchId      경기 UUID. 인게임 밖이면 null
 * @param matchTimeMs  호스트의 Fusion ServerTime 기준 경기 시각. 호스트 이벤트만
 * @param userPublicId 행동 주체의 users.public_id
 * @param eventName    GameEventName.wire 중 하나
 * @param phase        MatchPhase 이름. 인게임 밖이면 null
 * @param params       이벤트별 추가 값. 객체여야 하고 크기 상한이 있습니다
 */
public record GameEventRequest(
        @NotNull(message = "occurredAt은 필수입니다.")
        Long occurredAt,

        @NotBlank(message = "clientSessionId는 필수입니다.")
        @Pattern(regexp = UUID_REGEX, message = "clientSessionId는 UUID 형식이어야 합니다.")
        String clientSessionId,

        @NotNull(message = "clientSeq는 필수입니다.")
        @Min(value = 0, message = "clientSeq는 0 이상이어야 합니다.")
        @Max(value = 4_294_967_295L, message = "clientSeq가 너무 큽니다.")
        Long clientSeq,

        @Size(min = 6, max = 6, message = "roomCode는 6자여야 합니다.")
        String roomCode,

        @Pattern(regexp = UUID_REGEX, message = "matchId는 UUID 형식이어야 합니다.")
        String matchId,

        @Min(value = 0, message = "matchTimeMs는 0 이상이어야 합니다.")
        @Max(value = 4_294_967_295L, message = "matchTimeMs가 너무 큽니다.")
        Long matchTimeMs,

        @Pattern(regexp = UUID_REGEX, message = "userPublicId는 UUID 형식이어야 합니다.")
        String userPublicId,

        @NotBlank(message = "eventName은 필수입니다.")
        @Size(max = 64, message = "eventName은 64자 이내여야 합니다.")
        String eventName,

        @Size(max = 16, message = "phase는 16자 이내여야 합니다.")
        String phase,

        @Size(max = 32, message = "mapId는 32자 이내여야 합니다.")
        String mapId,

        Float posX,
        Float posY,
        Float posZ,

        @NotNull(message = "fromHost는 필수입니다.")
        Boolean fromHost,

        @NotNull(message = "schemaVer는 필수입니다.")
        @Min(value = 1, message = "schemaVer는 1 이상이어야 합니다.")
        Short schemaVer,

        JsonNode params
) {
    /** 하이픈 포함 36자 소문자·대문자 16진수. users.public_id 와 같은 형식입니다. */
    public static final String UUID_REGEX =
            "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$";
}
