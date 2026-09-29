package com.ssafy.d205.domain.feedback.repository;

import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.util.List;

import com.ssafy.d205.domain.feedback.entity.UserFeedback;

/**
 * 피드백을 저장하고, 운영자가 읽을 수 있게 최근 순으로 돌려줍니다.
 *
 * <p>처음에는 조회가 하나뿐이었습니다. 사람별·기간별로 거르는 조회를 미리 만들지 않은 이유는,
 * 부를 곳이 없는 조회는 필요한 모양이 정해지기 전에 계약처럼 굳기 때문입니다. 검색과 분포
 * 조회는 화면(S15P21D205-1004)이 정해진 뒤에 그 화면 모양대로 생겼습니다.
 *
 * <p><b>읽는 조회는 전부 {@code deleted_at IS NULL} 을 답니다</b>(S15P21D205-900). 신고 저장소와 같은
 * 규칙입니다.
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

    /**
     * 본문에 keyword 가 들어 있는 최근 피드백. 피드백 탭의 검색칸이 씁니다 (S15P21D205-1004).
     *
     * <p>{@link #findRecent} 와 같은 모양에 조건 하나가 더 붙었습니다. 나눈 이유는 pattern 이 null 일 때
     * {@code (:pattern IS NULL OR ...)} 로 합치면 MySQL 이 인덱스 판단을 매번 새로 하기 때문이고, 부르는
     * 쪽이 갈라 주는 편이 읽기에도 낫습니다.
     *
     * <p>pattern 은 서비스가 퍼센트·밑줄·백슬래시를 이스케이프해 앞뒤에 퍼센트를 붙인 값입니다.
     * 여기서 붙이지 않는 이유는 이스케이프와 감싸기가 한 곳에 있어야 하기 때문입니다. 대소문자는 컬럼
     * 콜레이션(utf8mb4_0900_ai_ci)이 구분하지 않습니다.
     *
     * <p>본문 인덱스는 없습니다. 피드백은 많아야 수천 건이라 훑어도 됩니다.
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
               AND f.message LIKE :pattern
             ORDER BY f.created_at DESC, f.user_feedback_seq DESC
             LIMIT :limit
            """, nativeQuery = true)
    List<FeedbackRow> searchRecent(@Param("pattern") String pattern, @Param("limit") int limit);

    /**
     * since(포함) 이후 들어온 보이는 피드백의 날짜별 건수. <b>날짜는 한국 시간</b>입니다 (S15P21D205-1004).
     *
     * <p>UTC 문자열의 앞 8자를 그대로 자르면 한국의 저녁 9시 이후 것이 다음 날로 넘어갑니다.
     * 신고 저장소의 같은 조회와 같은 이유로 9시간을 더해 자릅니다. 없는 날은 행이 없고 0 으로
     * 채우는 것은 서비스가 합니다.
     */
    @Query(value = """
            SELECT DATE_FORMAT(DATE_ADD(STR_TO_DATE(f.created_at, '%Y%m%d%H%i%s'), INTERVAL 9 HOUR), '%Y-%m-%d') AS label,
                   COUNT(*) AS count
              FROM user_feedback f
             WHERE f.created_at >= :since
               AND f.deleted_at IS NULL
             GROUP BY label
             ORDER BY label
            """, nativeQuery = true)
    List<LabelCountRow> countByDaySince(@Param("since") String since);

    /** since(포함) 이후 보이는 피드백의 플랫폼별 건수. 클라이언트가 안 보낸 것은 label 이 null 입니다. */
    @Query(value = """
            SELECT f.platform AS label, COUNT(*) AS count
              FROM user_feedback f
             WHERE f.created_at >= :since
               AND f.deleted_at IS NULL
             GROUP BY f.platform
             ORDER BY COUNT(*) DESC, f.platform
            """, nativeQuery = true)
    List<LabelCountRow> countByPlatformSince(@Param("since") String since);

    /** since(포함) 이후 보이는 피드백의 빌드 버전별 건수. 안 보낸 것은 label 이 null 입니다. */
    @Query(value = """
            SELECT f.build_ver AS label, COUNT(*) AS count
              FROM user_feedback f
             WHERE f.created_at >= :since
               AND f.deleted_at IS NULL
             GROUP BY f.build_ver
             ORDER BY COUNT(*) DESC, f.build_ver
            """, nativeQuery = true)
    List<LabelCountRow> countByBuildSince(@Param("since") String since);
}
