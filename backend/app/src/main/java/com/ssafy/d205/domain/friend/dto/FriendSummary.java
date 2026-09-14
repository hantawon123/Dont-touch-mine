package com.ssafy.d205.domain.friend.dto;

import com.ssafy.d205.domain.presence.entity.PresenceStatus;

/**
 * @param userId   상대의 공개 식별자
 * @param nickname 상대의 닉네임
 * @param presence 실제 접속 상태. 저장된 값이 아니라 마지막 하트비트로 계산한 값입니다.
 *                 <p>sessionId 는 담지 않습니다. 클라이언트의 FriendSummary 에 그 필드가
 *                 없고, 친구 방 참여는 초대 흐름(S15P21D205-438)이 담당합니다. 지금
 *                 노출하면 아무도 쓰지 않는 필드가 계약에 남습니다.
 */
public record FriendSummary(
        String userId,
        String nickname,
        PresenceStatus presence
) {
}
