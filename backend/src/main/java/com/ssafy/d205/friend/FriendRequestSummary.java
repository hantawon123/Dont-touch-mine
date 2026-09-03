package com.ssafy.d205.friend;

/**
 * @param userId      상대의 공개 식별자. 수락이나 거절을 부를 때 이 값을 씁니다.
 * @param nickname    상대의 닉네임. 목록에 보여줄 값입니다.
 * @param requestedAt 요청 시각. yyyyMMddHHmmss, UTC 입니다.
 */
public record FriendRequestSummary(
        String userId,
        String nickname,
        String requestedAt
) {
}
