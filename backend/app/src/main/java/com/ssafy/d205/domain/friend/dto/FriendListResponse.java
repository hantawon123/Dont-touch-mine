package com.ssafy.d205.domain.friend.dto;

import java.util.List;

/**
 * @param friends 친구 목록. 없으면 빈 배열입니다.
 *                <p>온라인과 오프라인을 나누지 않고 한 배열로 줍니다. 클라이언트의
 *                FriendListSystem.ReplaceFriends 가 IsOnline 으로 직접 나누므로
 *                서버가 미리 나눌 이유가 없습니다.
 */
public record FriendListResponse(
        List<FriendSummary> friends
) {
}
