package com.ssafy.d205.domain.friend.dto;

/**
 * @param userId    차단한 상대의 공개 식별자. 해제할 때 이 값을 씁니다.
 * @param nickname  차단한 시점의 닉네임이 아니라 현재 닉네임입니다. 상대가 닉네임을
 *                  바꾸면 목록에도 바뀐 이름이 보입니다.
 * @param blockedAt 차단한 시각. yyyyMMddHHmmss, UTC 입니다.
 */
public record BlockedUserSummary(
        String userId,
        String nickname,
        String blockedAt
) {
}
