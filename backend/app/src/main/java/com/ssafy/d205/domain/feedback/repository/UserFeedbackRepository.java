package com.ssafy.d205.domain.feedback.repository;

import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.util.List;

import com.ssafy.d205.domain.feedback.entity.UserFeedback;

/**
 * 피드백을 저장하고, 운영자가 읽을 수 있게 최근 순으로 돌려줍니다.
 *
 * <p>조회가 하나뿐입니다. 사람별·기간별로 거르는 조회를 미리 만들지 않은 이유는, 부를
 * 곳이 없는 조회는 필요한 모양이 정해지기 전에 계약처럼 굳기 때문입니다. 신고 조회가
 * 화면이 정해진 뒤에 생긴 것과 같은 이유입니다.
 */
public interface UserFeedbackRepository extends JpaRepository<UserFeedback, Integer> {

    /** since 이후(포함) 들어온 보이는 피드백 수. 개요 탭의 "오늘 피드백" 카드용입니다. */
    long countByCreatedAtGreaterThanEqualAndDeletedAtIsNull(String since);

    /**
     * 최근에 들어온 피드백. 운영자 목록이 쓰는 조회입니다.
     *
     * <p><b>왜 전체를 주지 않는가.</b> 피드백은 계속 쌓이기만 하고 지워지지 않습니다.
     * 상한 없이 내보내면 응답 크기가 시간에 비례해 자라고, 그 사실을 알게 되는 시점은
     * 응답이 이미 느려진 뒤입니다. 그래서 조회에 개수를 반드시 받습니다.
     *
     * <p>탈퇴한 사람의 피드백도 나옵니다. LEFT JOIN 인 이유가 그것입니다. INNER JOIN
     * 으로 두면 작성자가 NULL 인 행이 목록에서 조용히 사라지는데, 그 행들은 지우지
     * 않기로 하고 남긴 것입니다(V14 주석).
     *
     * <p>정렬 뒤에 seq 를 하나 더 두는 이유는 created_at 이 초 단위라서입니다. 같은
     * 초에 두 건이 들어오면 순서가 실행마다 달라지고, 그러면 페이지를 나눠 볼 때 같은
     * 행이 두 번 나오거나 빠집니다.
     *
     * <p>운영자가 숨긴 것은 빠집니다(S15P21D205-900). 이 조회 하나가 화면 전체를
     * 먹이므로, 여기 조건이 빠지면 숨김 기능이 통째로 동작하지 않습니다.
     */
    @Query(value = """
            SELECT f.user_feedback_seq AS id,
                   u.public_id         AS authorUserId,
                   u.nickname          AS authorNickname,
                   f.message           AS message,
                   f.build_ver         AS buildVer,
                   f.platform          AS platform,
                   f.created_at        AS createdAt
              FROM user_feedback f
              LEFT JOIN users u ON u.users_seq = f.author_seq
             WHERE f.deleted_at IS NULL
             ORDER BY f.created_at DESC, f.user_feedback_seq DESC
             LIMIT :limit
            """, nativeQuery = true)
    List<FeedbackRow> findRecent(@Param("limit") int limit);

    /**
     * 한 사람이 보낸 피드백. 관리 화면 사용자 상세용(S15P21D205-973).
     *
     * <p>위 조회와 달리 INNER JOIN 입니다. 사람을 지정해 묻는 자리라 작성자가 NULL 인 행은
     * 애초에 대상이 아닙니다.
     *
     * <p>상한을 박아 둡니다. 한 사람이 100건 넘게 보냈다면 그 자체가 신호이고, 화면에서 다
     * 읽을 양도 아닙니다.
     */
    @Query(value = """
            SELECT f.user_feedback_seq AS id,
                   u.public_id         AS authorUserId,
                   u.nickname          AS authorNickname,
                   f.message           AS message,
                   f.build_ver         AS buildVer,
                   f.platform          AS platform,
                   f.created_at        AS createdAt
              FROM user_feedback f
              JOIN users u ON u.users_seq = f.author_seq
             WHERE u.public_id = :userId
               AND f.deleted_at IS NULL
             ORDER BY f.created_at DESC, f.user_feedback_seq DESC
             LIMIT 100
            """, nativeQuery = true)
    List<FeedbackRow> findByAuthorForAdmin(@Param("userId") String userId);
}
