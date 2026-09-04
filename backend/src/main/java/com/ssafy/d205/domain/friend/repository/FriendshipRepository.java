package com.ssafy.d205.domain.friend.repository;

import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.util.List;
import java.util.Optional;

import com.ssafy.d205.domain.friend.entity.Friendship;

/**
 * 친구 도메인의 두 테이블을 다룹니다 — friendships 와 user_blocks.
 *
 * <p>user_blocks 를 엔티티로 만들지 않은 이유는 복합 기본키라 @IdClass 보일러플레이트가
 * 따라오는데, 필요한 세 연산(멱등 삽입, 삭제, 조인 조회)이 모두 네이티브 쿼리로
 * 끝나기 때문입니다. 엔티티가 할 일이 없습니다.
 *
 * <p>차단을 친구와 같은 도메인에 둔 이유는 순환 의존을 피하기 위해서입니다. 차단할 때
 * friendships 를 지워야 하고, 검색·요청·수락은 차단을 검사해야 합니다. 도메인을 나누면
 * 양방향 의존이 됩니다.
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
     * 차단합니다. <b>멱등합니다.</b>
     *
     * <p>ON DUPLICATE KEY UPDATE 로 중복 키를 오류가 아니라 무시로 만듭니다.
     * created_at = created_at 은 아무것도 바꾸지 않는 갱신이고, 목적은 그것뿐입니다.
     *
     * <p>조회 후 없으면 삽입하는 방식은 동시 요청에 뚫려 제약 위반이 납니다. 그러면
     * 트랜잭션이 롤백 전용이 되어 계정 발급처럼 삽입을 별도 빈으로 빼야 하는데,
     * 이 한 문장은 애초에 위반이 나지 않으므로 그럴 필요가 없습니다.
     */
    @Modifying
    @Query(value = """
            INSERT INTO user_blocks (blocker_seq, blocked_seq, created_at)
            VALUES (:blockerSeq, :blockedSeq, :now)
            ON DUPLICATE KEY UPDATE created_at = created_at
            """, nativeQuery = true)
    int insertBlock(@Param("blockerSeq") Integer blockerSeq,
                    @Param("blockedSeq") Integer blockedSeq,
                    @Param("now") String now);

    /**
     * 두 사람 사이의 방 초대를 양방향으로 지웁니다. 차단할 때 부릅니다.
     *
     * <p>초대를 남겨두면 차단이 뚫립니다. 친구였다가 차단당한 사람은 관계가 끊긴 뒤에도
     * 이미 받아둔 초대로 상대의 방에 들어갈 수 있습니다. 차단은 상대가 나를 찾지도
     * 닿지도 못하게 하는 것인데, 방 코드는 이미 건네진 뒤입니다. 초대의 수명이 3분이라
     * 창이 좁을 뿐 열려 있는 것은 마찬가지입니다.
     *
     * <p>양방향인 이유는 차단이 양방향으로 적용되기 때문입니다. 내가 보낸 초대도 상대가
     * 받은 초대도 함께 없어져야 합니다.
     *
     * <p>이 쿼리가 room_invites 를 읽는데도 여기 있는 이유는 user_blocks 와 같습니다.
     * 초대 도메인에 두면 친구 도메인이 초대를 부르고 초대 도메인이 친구를 부르는
     * 양방향 의존이 됩니다.
     */
    @Modifying
    @Query(value = """
            DELETE FROM room_invites
             WHERE (inviter_seq = :userSeqA AND invitee_seq = :userSeqB)
                OR (inviter_seq = :userSeqB AND invitee_seq = :userSeqA)
            """, nativeQuery = true)
    int deleteInvitesBetween(@Param("userSeqA") Integer userSeqA,
                             @Param("userSeqB") Integer userSeqB);

    /**
     * 차단을 해제합니다. 없어도 오류가 아닙니다. 영향받은 행이 0이면 이미 해제 상태입니다.
     */
    @Modifying
    @Query(value = """
            DELETE FROM user_blocks
             WHERE blocker_seq = :blockerSeq AND blocked_seq = :blockedSeq
            """, nativeQuery = true)
    int deleteBlock(@Param("blockerSeq") Integer blockerSeq,
                    @Param("blockedSeq") Integer blockedSeq);

    /**
     * 내가 차단한 사람들. 내가 차단당한 것은 담지 않습니다.
     *
     * <p>PK 선두 컬럼이 blocker_seq 라 이 조회는 기본키 인덱스를 그대로 씁니다.
     */
    @Query(value = """
            SELECT u.public_id  AS userId,
                   u.nickname   AS nickname,
                   b.created_at AS blockedAt
              FROM user_blocks b
              JOIN users u ON u.users_seq = b.blocked_seq
             WHERE b.blocker_seq = :meSeq
             ORDER BY b.created_at DESC
            """, nativeQuery = true)
    List<BlockedUserRow> findBlocked(@Param("meSeq") Integer meSeq);

    /**
     * 차단한 두 사람 사이의 친구 관계와 대기 중인 요청을 지웁니다.
     *
     * <p><b>차단할 때 반드시 함께 해야 합니다.</b> 이걸 빼면 이미 친구인 상태가 유지되고
     * 받은 요청 목록에 차단한 사람의 요청이 계속 보입니다. 목록 쿼리에 차단 필터를 붙여
     * 가리는 방법도 있지만, 관계를 남겨두면 언젠가 다른 경로로 새어 나옵니다.
     *
     * <p>상태를 가리지 않고 지웁니다. ACCEPTED 든 PENDING 든 차단하면 없어야 합니다.
     *
     * <p>clearAutomatically 를 켠 이유가 있습니다. 네이티브 삭제는 DB만 지우고 영속성
     * 컨텍스트는 건드리지 않습니다. 지금은 block() 이 Friendship 을 로드하지 않아
     * 무해하지만, 누가 "차단 전 관계를 확인해 로그를 남기자" 같은 코드를 넣어
     * findByPair() 를 부르면 그 엔티티가 관리 상태로 남습니다. 그 뒤 같은 트랜잭션에서
     * 읽으면 <b>지워진 행이 캐시에서 나옵니다.</b> 컨텍스트를 비워 그 함정을 없앱니다.
     */
    @Modifying(clearAutomatically = true)
    @Query(value = """
            DELETE FROM friendships
             WHERE user_low_seq = :lowSeq AND user_high_seq = :highSeq
            """, nativeQuery = true)
    int deleteFriendshipByPair(@Param("lowSeq") Integer lowSeq,
                               @Param("highSeq") Integer highSeq);

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
