package com.ssafy.d205.domain.user.dto;

/**
 * 남에게 보여줄 수 있는 계정 정보.
 *
 * <p>내부 seq 와 provider_user_id 는 담지 않습니다. 전자는 가입자 수와 다른 계정을
 * 노출하고, 후자는 자격증명입니다.
 *
 * <p>접속 상태는 넣지 않았습니다. 클라이언트의 검색 화면이 렌더링하는 FriendSearchHit
 * 에 접속 상태가 없어서 쓰이지 않습니다. 접속 상태는 친구 목록 API(S15P21D205-432)가
 * 담당하고, 하트비트 타임아웃 판정을 두 곳에 두지 않기 위해 여기서는 빼둡니다.
 */
public record UserSummary(
        String userId,
        String nickname
) {
}
