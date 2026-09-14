package com.ssafy.d205.domain.user.repository;

import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.util.List;
import java.util.Optional;

import com.ssafy.d205.domain.user.entity.User;

public interface UserRepository extends JpaRepository<User, Integer> {

    Optional<User> findByPublicId(String publicId);

    /**
     * nickname 컬럼 콜레이션이 utf8mb4_0900_as_cs라(V4) 대소문자를 구분합니다.
     * Player가 있어도 player는 없다고 답하고, uk_users_nickname도 같게 동작합니다.
     */
    boolean existsByNickname(String nickname);

    /**
     * 닉네임이 정확히 일치하는 사용자를 찾습니다.
     *
     * <p><b>앞글자로는 찾히지 않습니다.</b> 예전에는 접두사로 찾을 수 있었지만, 몇 글자만
     * 쳐도 남이 걸려 나오는 것을 막기 위해 정확히 일치로 바꿨습니다. 검색을 끄는
     * 스위치(searchable)와 같은 방향입니다.
     *
     * <p><b>대소문자를 구분합니다.</b> nickname 컬럼이 as_cs 콜레이션이라 player 를
     * 검색하면 Player 는 나오지 않습니다. 둘은 서로 다른 닉네임이고 동시에 존재할 수
     * 있으므로, 구분하지 않으면 한 번의 검색이 서로 다른 두 사람을 함께 내놓습니다.
     *
     * <p><b>결과는 많아야 한 건입니다.</b> uk_users_nickname 이 유니크이기 때문입니다.
     * 그래서 limit 과 정렬이 결과에 영향을 주지 않습니다. 파라미터는 계약을 깨지 않으려고
     * 남겨두었을 뿐입니다.
     *
     * <p>인덱스는 uk_users_nickname 을 그대로 씁니다. 유니크 인덱스라 접두사 스캔보다
     * 낫습니다. nickname_lower 는 이제 이 쿼리가 쓰지 않지만, 친구 목록 정렬이 아직
     * 쓰고 있으므로 남겨 둡니다.
     *
     * <p>JPQL 이 아니라 네이티브 쿼리인 이유는 searchable 과 users_seq 를 함께 걸러야
     * 하는데 이 조합을 파생 쿼리 이름으로 표현하면 읽기 어려워지기 때문입니다.
     *
     * <p>거르는 것은 나 자신과 <b>검색을 꺼 둔 사람</b>입니다. 화면에서 가리는 것이
     * 아니라 여기서 빠지므로, 응답 자체에 그 사람이 담기지 않습니다.
     */
    @Query(value = """
            SELECT u.public_id AS userId,
                   u.nickname  AS nickname
              FROM users u
             WHERE u.nickname = :nickname
               AND u.users_seq <> :meSeq
               AND u.searchable = TRUE
            """, nativeQuery = true)
    List<UserSummaryRow> findByExactNickname(@Param("nickname") String nickname,
                                             @Param("meSeq") Integer meSeq);

    /**
     * 운영자용 사용자 검색 (S15P21D205-972). 게임 클라이언트가 쓰는 위 검색과 규칙이 다릅니다.
     *
     * <p><b>부분 일치이고 searchable 을 무시합니다.</b> 운영자는 검색을 꺼 둔 사람과 신고가
     * 없는 사람도 찾아야 합니다. 그래서 이 조회는 관리자 세션 뒤에서만 불려야 하고,
     * 컨트롤러가 /api/v1/admin 아래에 있는 것이 그 보장입니다.
     *
     * <p>대소문자는 구분합니다. nickname 이 as_cs 콜레이션이라 LIKE 도 그 규칙을 따릅니다.
     *
     * <p>파라미터 셋으로 세 가지 조회를 다 합니다. 같은 SELECT 를 세 번 적지 않으려는 것입니다.
     * <ul>
     *   <li>최근 가입순 전체: pattern "%", exact ""</li>
     *   <li>검색: pattern "%q%" (q 의 % _ ! 는 ! 로 이스케이프), exact q</li>
     *   <li>한 사람: pattern "" (아무것도 안 걸림), exact userId, limit 1</li>
     * </ul>
     * NULL 파라미터를 쓰지 않는 이유는 네이티브 쿼리에서 타입 없는 NULL 바인딩이 드라이버에
     * 따라 실패하기 때문입니다.
     *
     * <p>신고 수와 친구 수는 상관 서브쿼리입니다. 결과가 최대 50행이라 행마다 두 번 세어도
     * 인덱스(ix_user_reports_reported, uk_friendships_pair)를 타면 문제없습니다. 사용자가
     * 수만 명이 되면 그때 집계 테이블로 옮깁니다.
     *
     * <p>기기 식별자는 SELECT 에 없습니다. 더하지 마세요.
     */
    @Query(value = """
            SELECT u.public_id        AS userId,
                   u.nickname         AS nickname,
                   u.created_at       AS createdAt,
                   u.suspended_at     AS suspendedAt,
                   u.suspended_reason AS suspendedReason,
                   p.status           AS presence,
                   p.heartbeat_at     AS lastSeenAt,
                   (SELECT COUNT(*)
                      FROM user_reports r
                     WHERE r.reported_seq = u.users_seq
                       AND r.deleted_at IS NULL)                        AS reportCount,
                   (SELECT COUNT(*)
                      FROM friendships f
                     WHERE f.status = 'ACCEPTED'
                       AND (f.user_low_seq = u.users_seq
                            OR f.user_high_seq = u.users_seq))         AS friendCount
              FROM users u
              LEFT JOIN user_presence p ON p.user_seq = u.users_seq
             WHERE u.nickname LIKE :pattern ESCAPE '!'
                OR u.public_id = :exact
             ORDER BY u.created_at DESC, u.users_seq DESC
             LIMIT :limit
            """, nativeQuery = true)
    List<AdminUserRow> searchForAdmin(@Param("pattern") String pattern,
                                      @Param("exact") String exact,
                                      @Param("limit") int limit);
}
