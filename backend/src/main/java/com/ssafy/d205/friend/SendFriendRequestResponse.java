package com.ssafy.d205.friend;

/**
 * @param status 요청을 보낸 결과. PENDING 이면 상대의 수락을 기다리는 상태이고,
 *               ACCEPTED 면 <b>바로 친구가 된 것</b>입니다.
 *               <p>후자는 상대가 이미 나에게 요청을 보내둔 경우입니다. 서로 원한다는
 *               것이 명확하므로 "받은 요청을 수락하세요"로 되돌리지 않고 그 자리에서
 *               성립시킵니다. 클라이언트는 이 값으로 "요청을 보냈습니다"와 "친구가
 *               되었습니다"를 갈라 보여주면 됩니다.
 */
public record SendFriendRequestResponse(
        FriendshipStatus status
) {
}
