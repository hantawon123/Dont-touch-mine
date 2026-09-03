package com.ssafy.d205.friend;

/**
 * 친구 관계의 상태.
 *
 * <p>거절과 친구 삭제는 상태가 아니라 <b>행 삭제</b>로 처리합니다. REJECTED 를 남기면
 * uk_friendships_pair 때문에 같은 상대에게 다시 요청할 수 없게 됩니다.
 * V2__create_friends.sql 주석에 같은 내용이 적혀 있습니다.
 */
public enum FriendshipStatus {
    PENDING,
    ACCEPTED
}
