package com.ssafy.d205.domain.presence.dto;

import jakarta.validation.constraints.Size;

/**
 * @param sessionId Photon 룸 식별자. 세션 밖이면 null 로 두거나 필드를 빼고 보냅니다.
 *                  <p>값이 있으면 IN_GAME, 없으면 ONLINE 입니다.
 *                  <p>상태를 직접 받지 않는 이유가 있습니다. status 와 sessionId 를 함께
 *                  받으면 "IN_GAME 인데 sessionId 가 없다" 같은 <b>잘못된 조합이 표현
 *                  가능</b>해지고, 그걸 막는 검증을 따로 만들어야 합니다. 이 필드 하나로
 *                  상태가 결정되므로 어긋날 방법이 없습니다.
 */
public record UpdatePresenceRequest(
        @Size(max = 64, message = "sessionId는 64자를 넘을 수 없습니다.")
        String sessionId
) {
}
