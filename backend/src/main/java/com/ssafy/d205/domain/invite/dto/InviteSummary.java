package com.ssafy.d205.domain.invite.dto;

/**
 * @param userId    부른 사람의 공개 식별자. 거절할 때 이 값을 씁니다.
 * @param nickname  부른 사람의 현재 닉네임.
 * @param roomCode  입장할 방의 코드. 클라이언트가 EnterByCodeAsync 로 넘깁니다.
 *                  <p>잠긴 방이면 코드만으로는 못 들어가고 비밀번호를 따로 묻습니다.
 *                  코드는 어느 방인지만 가리키고 권한을 주지 않습니다.
 * @param invitedAt 부른 시각. yyyyMMddHHmmss, UTC.
 */
public record InviteSummary(
        String userId,
        String nickname,
        String roomCode,
        String invitedAt
) {
}
