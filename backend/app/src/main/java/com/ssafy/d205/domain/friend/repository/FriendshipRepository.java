package com.ssafy.d205.domain.friend.repository;

import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.util.List;
import java.util.Optional;

import com.ssafy.d205.domain.friend.entity.Friendship;

/**
 * 친구 요청과 친구 관계를 담은 friendships 를 다룹니다.
 *
 * <p>차단이 있던 동안에는 이 인터페이스가 user_blocks 도 함께 다뤘습니다. 차단할 때
 * friendships 를 지워야 하고 검색과 요청과 수락이 차단을 검사해야 해서, 도메인을
 * 나누면 양방향 의존이 되기 때문이었습니다. 차단이 사라지면서 그 얽힘도 사라졌습니다.
 */
public interface FriendshipRepository extends JpaRepository<Friendship, Integer> {

    Optional<Friendship> findByUserLowSeqAndUserHighSeq(Integer lowSeq, Integer highSeq);

    /**
     * 순서를 신경 쓰지 않고 두 사람 사이의 관계를 찾습니다.
     *
     * <p>정렬 방식은 {@link Friendship#request} 와 같아야 합니다. 한쪽만 바꾸면 관계가
     * 있는데 없다고 답하기 시작합니다.
     */
    default Optional<Friendship> findByPair(int userSeqA, int userSeqB) {
        return findByUserLowSeqAndUserHighSeq(Math.min(userSeqA, userSeqB),
                                              Math.max(userSeqA, userSeqB));
    }

    /**
     * 내가 받은 요청. 상대는 요청을 보낸 사람이므로 requested_by_seq 로 조인합니다.
     */
    @Query(value = """
            SELECT u.public_id  AS userId,
                   u.nickname   AS nickname,
                   f.created_at AS requestedAt
              FROM friendships f
              JOIN users u ON u.users_seq = f.requested_by_seq
             WHERE f.status = 'PENDING'
               AND (f.user_low_seq = :meSeq OR f.user_high_seq = :meSeq)
               AND f.requested_by_seq <> :meSeq
             ORDER BY f.created_at DESC
            """, nativeQuery = true)
    List<FriendRequestRow> findIncoming(@Param("meSeq") Integer meSeq);

    /**
     * 내가 보낸 요청. 상대는 쌍에서 내가 아닌 쪽이므로 CASE 로 골라 조인합니다.
     */
    @Query(value = """
            SELECT u.public_id  AS userId,
                   u.nickname   AS nickname,
                   f.created_at AS requestedAt
              FROM friendships f
              JOIN users u
                ON u.users_seq = CASE WHEN f.user_low_seq = :meSeq
                                      THEN f.user_high_seq
                                      ELSE f.user_low_seq END
             WHERE f.status = 'PENDING'
               AND f.requested_by_seq = :meSeq
             ORDER BY f.created_at DESC
            """, nativeQuery = true)
    List<FriendRequestRow> findOutgoing(@Param("meSeq") Integer meSeq);

    /**
     * 성립된 친구 목록. 상대의 접속 상태를 함께 가져옵니다.
     *
     * <p>user_presence 를 LEFT JOIN 하는 이유는 한 번도 접속하지 않은 친구는 그 테이블에
     * 행이 없기 때문입니다. INNER JOIN 이면 그 사람이 목록에서 사라집니다.
     *
     * <p>저장된 status 를 그대로 쓰지 않고 heartbeat_at 도 함께 가져옵니다. 크래시로 죽은
     * 클라이언트의 status 는 ONLINE 으로 남아 있어서, 실제 상태는 마지막 하트비트로
     * 계산해야 합니다. 그 판정은 PresenceTimeout 이 합니다.
     *
     * <p>상대는 쌍에서 내가 아닌 쪽이므로 CASE 로 골라 조인합니다. 정렬을 nickname_lower
     * 로 하는 것은 V6 의 인덱스를 쓰기 위해서이고, 온라인 여부로 나누는 것은 클라이언트가
     * 합니다.
     */
    @Query(value = """
            SELECT u.public_id     AS userId,
                   u.nickname      AS nickname,
                   p.status        AS status,
                   p.heartbeat_at  AS heartbeatAt
              FROM friendships f
              JOIN users u
                ON u.users_seq = CASE WHEN f.user_low_seq = :meSeq
                                      THEN f.user_high_seq
                                      ELSE f.user_low_seq END
              LEFT JOIN user_presence p ON p.user_seq = u.users_seq
             WHERE f.status = 'ACCEPTED'
               AND (f.user_low_seq = :meSeq OR f.user_high_seq = :meSeq)
             ORDER BY u.nickname_lower
            """, nativeQuery = true)
    List<FriendSummaryRow> findFriends(@Param("meSeq") Integer meSeq);

    /**
     * 두 사람 사이의 방 초대를 양방향으로 지웁니다. 친구를 끊을 때 부릅니다.
     *
     * <p>초대를 남겨두면 끊은 것이 끊은 것이 아닙니다. 방금 친구를 끊은 사람이 이미
     * 받아둔 초대로 내 방에 그대로 들어올 수 있습니다. 초대의 수명이 3분이라 창이
     * 좁을 뿐 열려 있는 것은 마찬가지입니다.
     *
     * <p>원래 차단이 하던 일입니다. 차단이 사라지면서 관계를 끊는 유일한 수단이
     * 친구 끊기가 되었으므로 이 정리도 그쪽으로 옮겼습니다.
     *
     * <p>양방향인 이유는 초대가 어느 쪽에서 갔든 관계가 끊기면 함께 없어져야 하기
     * 때문입니다. 내가 보낸 것도 상대가 보낸 것도 지웁니다.
     *
     * <p>이 쿼리가 room_invites 를 읽는데도 친구 쪽에 있는 이유는, 초대 도메인에 두면
     * 친구가 초대를 부르고 초대가 친구를 부르는 양방향 의존이 되기 때문입니다.
     */
    @Modifying
    @Query(value = """
            DELETE FROM room_invites
             WHERE (inviter_seq = :userSeqA AND invitee_seq = :userSeqB)
                OR (inviter_seq = :userSeqB AND invitee_seq = :userSeqA)
            """, nativeQuery = true)
    int deleteInvitesBetween(@Param("userSeqA") Integer userSeqA,
                             @Param("userSeqB") Integer userSeqB);

}
