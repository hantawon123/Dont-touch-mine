package com.ssafy.d205.domain.presence.dto;

import jakarta.validation.constraints.Size;

import com.ssafy.d205.domain.presence.entity.SessionKind;

/**
 * @param sessionId   Photon 룸 식별자. 룸 밖이면 null 로 두거나 필드를 빼고 보냅니다.
 *                    <p>값이 없으면 ONLINE 입니다. 있으면 룸 안이고, 로비인지 경기인지는
 *                    sessionKind 가 정합니다.
 *                    <p>상태를 직접 받지 않는 이유가 있습니다. status 를 그대로 받으면
 *                    "IN_GAME 인데 sessionId 가 없다" 같은 <b>잘못된 조합이 표현 가능</b>
 *                    해지고, 그걸 막는 검증을 따로 만들어야 합니다. 유도하면 애초에
 *                    표현할 수 없습니다.
 * @param sessionKind 그 룸이 로비인지 경기인지. LOBBY 면 IN_LOBBY, MATCH 면 IN_GAME.
 *                    <p><b>값이 없으면 MATCH 로 봅니다.</b> 이 필드가 생기기 전의
 *                    클라이언트는 sessionId 만 보내고, 그때 룸 안은 곧 IN_GAME 이었습니다.
 *                    없는 것을 MATCH 로 읽으면 그 클라이언트가 지금과 똑같이 동작합니다.
 *                    LOBBY 로 읽으면 경기 중인 사람이 로비로 보이고, 그것이 더 나쁩니다.
 *                    <p>sessionId 가 없으면 이 값은 아무 뜻이 없어 무시합니다. 룸 밖인데
 *                    로비라고 주장하는 요청을 400 으로 되돌릴 수도 있지만, 상태는 이미
 *                    ONLINE 으로 정해져 있어 되돌려서 나아지는 것이 없습니다.
 */
public record UpdatePresenceRequest(
        @Size(max = MAX_SESSION_ID_LENGTH, message = "sessionId는 64자를 넘을 수 없습니다.")
        String sessionId,

        SessionKind sessionKind
) {

    /**
     * user_presence.session_id 컬럼의 길이입니다.
     *
     * <p>상수로 뺀 이유는 이 값을 검사하는 곳이 둘이 됐기 때문입니다. REST 본문은 위의
     * &#64;Size 가 400 으로 되돌리고, WebSocket {@code PRESENCE} 프레임은 되돌릴 응답이
     * 없어 핸들러가 직접 재고 버립니다. 두 곳이 각자 64 를 적으면 컬럼을 늘리는 날
     * 한쪽만 고쳐집니다.
     */
    public static final int MAX_SESSION_ID_LENGTH = 64;

    /**
     * 없는 sessionKind 를 MATCH 로 메꾼 값. <b>기본값을 여기 한 곳에만 둡니다.</b>
     * 서비스와 엔티티가 각자 null 을 확인하면 한쪽만 고쳐지는 날이 옵니다.
     */
    public SessionKind sessionKindOrMatch() {
        return sessionKind == null ? SessionKind.MATCH : sessionKind;
    }
}
