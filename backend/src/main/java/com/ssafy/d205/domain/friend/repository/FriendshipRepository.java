package com.ssafy.d205.domain.friend.repository;

import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.util.List;
import java.util.Optional;

import com.ssafy.d205.domain.friend.entity.Friendship;

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
     * 두 사람 사이에 차단이 있는지. 방향은 보지 않습니다.
     *
     * <p>차단은 양방향으로 적용합니다. A가 B를 차단하면 B도 A에게 요청을 보낼 수
     * 없어야 합니다. 차단당한 쪽이 계속 요청을 보낼 수 있으면 차단의 의미가 없습니다.
     *
     * <p>이 쿼리가 user_blocks 를 읽는데도 여기 있는 이유는, 그 테이블이 복합 기본키라
     * 엔티티로 만들면 @IdClass 보일러플레이트가 따라오고 쓰는 곳은 친구 도메인뿐이기
     * 때문입니다. user_blocks 의 두 인덱스가 각 방향을 담당합니다.
     */
    @Query(value = """
            SELECT EXISTS (
                       SELECT 1
                         FROM user_blocks
                        WHERE (blocker_seq = :userSeqA AND blocked_seq = :userSeqB)
                           OR (blocker_seq = :userSeqB AND blocked_seq = :userSeqA))
            """, nativeQuery = true)
    long countBlockBetween(@Param("userSeqA") Integer userSeqA,
                           @Param("userSeqB") Integer userSeqB);

    /**
     * 위 쿼리를 boolean 으로 감쌉니다.
     *
     * <p>반환형을 boolean 으로 두면 터집니다. MySQL 의 SELECT EXISTS 는 BIGINT 로
     * 0 또는 1 을 주므로 Long 을 Boolean 으로 캐스팅하려다 ClassCastException 이
     * 납니다. 컴파일은 통과하고 실행에서만 드러납니다.
     */
    default boolean existsBlockBetween(int userSeqA, int userSeqB) {
        return countBlockBetween(userSeqA, userSeqB) > 0;
    }
}
