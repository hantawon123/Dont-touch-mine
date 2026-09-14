package com.ssafy.d205.domain.report.repository;

import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.util.List;

import com.ssafy.d205.domain.report.entity.ReportStatus;
import com.ssafy.d205.domain.report.entity.UserReport;

/**
 * 신고를 저장하고, 운영자가 볼 수 있게 사람 단위로 묶어 읽습니다.
 *
 * <p>조회가 여기 생긴 것은 S15P21D205-543 부터입니다. 그 전까지 비워 둔 이유는 부를
 * 곳이 없는 조회는 실제로 필요한 모양이 정해지기 전에 계약처럼 굳기 때문입니다.
 * 이제 화면이 정해졌으므로 그 화면이 필요로 하는 모양으로 만듭니다.
 *
 * <p><b>읽는 조회는 전부 {@code deleted_at IS NULL} 을 답니다</b>(S15P21D205-900). 하나라도
 * 빠뜨리면 운영자가 숨긴 신고가 그 경로로만 다시 나타나고, 목록과 상세와 건수가 서로 다른
 * 말을 하기 시작합니다. 조회를 새로 만들 때도 같습니다.
 */
public interface UserReportRepository extends JpaRepository<UserReport, Integer> {

    /**
     * 신고당한 사람들을 묶어서 돌려줍니다. 목록 화면이 쓰는 조회입니다.
     *
     * <p><b>왜 사람 단위로 묶는가.</b> 신고를 한 건씩 나열하면 운영자가 판단할 수
     * 없습니다. 같은 사람에 대한 다섯 건이 흩어져 나오면 그 사람이 문제인지 신고한
     * 사람이 문제인지 구분되지 않습니다.
     *
     * <p><b>건수와 사람 수를 따로 셉니다.</b> 같은 사람을 여러 번 신고하는 것을 막지
     * 않기로 했으므로 한 사람이 건수를 부풀릴 수 있습니다. 둘을 나눠야 "3건 1명"과
     * "7건 5명"이 다르게 읽힙니다.
     *
     * <p><b>탈퇴한 신고자는 사람 수에서 빼고 따로 셉니다.</b> reporter_seq 가 전부
     * NULL 이라 COUNT(DISTINCT) 로는 몇 명인지 알 수 없습니다. MySQL 의 COUNT(DISTINCT)
     * 는 NULL 을 세지 않으므로 자동으로 빠지고, 그 건수만 SUM 으로 따로 셉니다.
     *
     * <p>정렬은 최근 신고 순입니다. 오래된 순으로 두면 처리하지 않고 쌓인 것이 위에
     * 남아 새로 들어온 심각한 신고가 아래로 밀립니다.
     *
     * <p>status 로 거르므로 ix_user_reports_pending 이 그대로 쓰입니다.
     */
    @Query(value = """
            SELECT u.public_id AS userId,
                   u.nickname  AS nickname,
                   COUNT(*)                        AS reportCount,
                   COUNT(DISTINCT r.reporter_seq)  AS reporterCount,
                   SUM(r.reporter_seq IS NULL)     AS fromDeletedAccounts,
                   MAX(r.created_at)               AS lastReportedAt,
                   u.suspended_at                  AS suspendedAt
              FROM user_reports r
              JOIN users u ON u.users_seq = r.reported_seq
             WHERE r.status = :status
               AND r.deleted_at IS NULL
             GROUP BY r.reported_seq, u.public_id, u.nickname, u.suspended_at
             ORDER BY MAX(r.created_at) DESC
            """, nativeQuery = true)
    List<ReportedUserRow> summarizeByStatus(@Param("status") String status);

    /**
     * 사유별 건수. 목록의 각 줄에 붙습니다.
     *
     * <p>요약 조회와 나눈 이유는 한 사람이 사유를 여러 개 갖기 때문입니다. 한 쿼리로
     * 합치면 사람마다 사유 수만큼 행이 늘어나 건수와 인원수가 부풀려집니다.
     *
     * <p>목록 전체의 사유를 한 번에 가져와 서비스가 붙입니다. 사람마다 따로 물으면
     * 목록에 스무 명이 있을 때 쿼리가 스물한 번 나갑니다.
     */
    @Query(value = """
            SELECT u.public_id AS userId,
                   r.reason    AS reason,
                   COUNT(*)    AS count
              FROM user_reports r
              JOIN users u ON u.users_seq = r.reported_seq
             WHERE r.status = :status
               AND r.deleted_at IS NULL
             GROUP BY u.public_id, r.reason
            """, nativeQuery = true)
    List<ReasonCountRow> countReasonsByStatus(@Param("status") String status);

    /**
     * 한 사람에 대한 신고를 하나씩. 상세 화면이 씁니다.
     *
     * <p>목록에 메모까지 담지 않는 이유는 신고가 수백 건 쌓인 사람이 있으면 목록
     * 응답이 통째로 무거워지기 때문입니다. 대부분은 목록의 사유 분포로 판단이 끝나고,
     * 더 봐야 할 때만 이쪽을 부릅니다.
     *
     * <p>신고자가 누구인지는 담지 않습니다. 운영자가 판단할 때 필요한 것은 무엇이
     * 몇 번 일어났는가이고, 신고자를 드러내면 보복의 여지가 생깁니다. 인원수는
     * 목록의 reporterCount 로 충분합니다.
     */
    @Query(value = """
            SELECT r.user_reports_seq AS id,
                   r.reason           AS reason,
                   r.memo             AS memo,
                   r.created_at       AS createdAt,
                   r.status           AS status
              FROM user_reports r
              JOIN users u ON u.users_seq = r.reported_seq
             WHERE u.public_id = :userId
               AND (:status IS NULL OR r.status = :status)
               AND r.deleted_at IS NULL
             ORDER BY r.created_at DESC
            """, nativeQuery = true)
    List<ReportDetailRow> findByReportedUserId(@Param("userId") String userId,
                                               @Param("status") String status);

    /**
     * 한 사람에 대한 미검토 신고를 전부 가져옵니다. 검토 처리가 씁니다.
     *
     * <p>운영자는 신고 한 건이 아니라 사람을 보고 판단하므로, 다섯 건 쌓인 사람을
     * 다섯 번 누르게 할 이유가 없습니다. 한 번 누르면 그 사람의 미검토 신고가 함께
     * 마무리됩니다.
     *
     * <p>이미 검토한 것은 건드리지 않습니다. 어제 기각한 것을 오늘 조치함으로 덮으면
     * 그때 무슨 판단을 했는지가 사라집니다. 검토 뒤에 새로 들어온 신고만 다음 차례에
     * 다시 올라옵니다.
     */
    @Query("""
            SELECT r FROM UserReport r
             WHERE r.status = com.ssafy.d205.domain.report.entity.ReportStatus.PENDING
               AND r.reportedSeq = :reportedSeq
               AND r.deletedAt IS NULL
            """)
    List<UserReport> findPendingAbout(@Param("reportedSeq") Integer reportedSeq);

    /**
     * 한 사람에 대한 신고 중 아직 보이는 것 전부. 상태를 가리지 않습니다.
     *
     * <p>이미 숨긴 것을 빼는 이유는 {@link UserReport#hide(String)} 가 시각을 덮어쓰지
     * 않기 때문입니다. 넘겨도 결과는 같지만 부를 이유가 없습니다.
     */
    @Query("""
            SELECT r FROM UserReport r
             WHERE r.reportedSeq = :reportedSeq
               AND r.deletedAt IS NULL
            """)
    List<UserReport> findVisibleAbout(@Param("reportedSeq") Integer reportedSeq);

    /**
     * 위와 같되 한 검토 상태만.
     *
     * <p><b>화면이 보여준 것과 치우는 것을 같게 하려고 있습니다</b>(S15P21D205-900).
     * 목록은 status 로 걸러 보여주므로, 상태를 가리지 않고 치우면 운영자가 ACTIONED
     * 화면에서 누른 한 번에 보지도 못한 PENDING 신고까지 사라집니다.
     *
     * <p>조건을 하나의 조회에 {@code (:status IS NULL OR ...)} 로 합치지 않고 나눴습니다.
     * JPQL 에서 enum 파라미터에 null 을 넘기면 Hibernate 가 타입을 정하지 못해 실행
     * 시점에 터집니다. 부르는 쪽이 갈라 주는 편이 안전합니다.
     */
    @Query("""
            SELECT r FROM UserReport r
             WHERE r.reportedSeq = :reportedSeq
               AND r.status = :status
               AND r.deletedAt IS NULL
            """)
    List<UserReport> findVisibleAbout(@Param("reportedSeq") Integer reportedSeq,
                                      @Param("status") ReportStatus status);

    /**
     * 한 사람에 대한 신고를 통째로 지웁니다. 숨긴 것까지 함께 사라집니다.
     *
     * <p>엔티티를 읽어 와 지우지 않고 한 문장으로 지웁니다. 수백 건 쌓인 사람이 있을 수
     * 있고, 지우기 전에 값을 볼 이유가 없습니다.
     *
     * <p>이미 숨긴 행도 지웁니다. 운영자가 보기에 "이 사람 신고 전부 삭제"인데 숨긴 것만
     * 남으면 나중에 그 행들의 출처를 아무도 설명하지 못합니다.
     *
     * @return 지운 건수
     */
    @Modifying(clearAutomatically = true, flushAutomatically = true)
    @Query("DELETE FROM UserReport r WHERE r.reportedSeq = :reportedSeq")
    int deleteByReportedSeq(@Param("reportedSeq") Integer reportedSeq);

    /**
     * 위와 같되 한 검토 상태만. 화면이 보여준 것과 지우는 것을 맞춥니다.
     *
     * <p>그 상태의 숨긴 행도 함께 지웁니다. 숨김과 삭제는 "다시 볼 수 있는가"만 다르고,
     * 지우기로 한 범위 안에 숨긴 것만 남겨 두면 그 행의 출처를 나중에 설명하지 못합니다.
     *
     * @return 지운 건수
     */
    @Modifying(clearAutomatically = true, flushAutomatically = true)
    @Query("DELETE FROM UserReport r WHERE r.reportedSeq = :reportedSeq AND r.status = :status")
    int deleteByReportedSeqAndStatus(@Param("reportedSeq") Integer reportedSeq,
                                     @Param("status") ReportStatus status);

    /**
     * 한 사람이 <b>받은</b> 신고. 신고자를 함께 담습니다. 관리 화면 사용자 상세용(S15P21D205-973).
     *
     * <p>{@link #findByReportedUserId} 와 나눈 이유는 그쪽이 신고자를 일부러 감추기 때문입니다.
     * 그 조회의 계약을 바꾸지 않고 운영자용 자리에서만 신고자를 봅니다.
     *
     * <p>신고자가 탈퇴했으면 LEFT JOIN 이라 상대편 컬럼이 NULL 로 옵니다. 행 자체는 남습니다.
     */
    @Query(value = """
            SELECT r.user_reports_seq AS id,
                   r.reason           AS reason,
                   r.memo             AS memo,
                   r.created_at       AS createdAt,
                   r.status           AS status,
                   o.public_id        AS counterpartUserId,
                   o.nickname         AS counterpartNickname
              FROM user_reports r
              JOIN users u ON u.users_seq = r.reported_seq
              LEFT JOIN users o ON o.users_seq = r.reporter_seq
             WHERE u.public_id = :userId
               AND r.deleted_at IS NULL
             ORDER BY r.created_at DESC, r.user_reports_seq DESC
             LIMIT 200
            """, nativeQuery = true)
    List<AdminReportRow> findReceivedForAdmin(@Param("userId") String userId);

    /**
     * 한 사람이 <b>한</b> 신고. 신고당한 사람을 함께 담습니다. 관리 화면 사용자 상세용.
     *
     * <p>"이 사람이 남을 얼마나 신고하나"를 보는 자리입니다. 기각된 것이 많으면 무고성 신고를
     * 반복하는 사람입니다. 신고당한 쪽은 탈퇴하면 행이 함께 지워지므로(CASCADE) 상대편이
     * NULL 인 경우는 사실상 없지만, 조인 모양은 위와 맞춥니다.
     */
    @Query(value = """
            SELECT r.user_reports_seq AS id,
                   r.reason           AS reason,
                   r.memo             AS memo,
                   r.created_at       AS createdAt,
                   r.status           AS status,
                   o.public_id        AS counterpartUserId,
                   o.nickname         AS counterpartNickname
              FROM user_reports r
              JOIN users u ON u.users_seq = r.reporter_seq
              LEFT JOIN users o ON o.users_seq = r.reported_seq
             WHERE u.public_id = :userId
               AND r.deleted_at IS NULL
             ORDER BY r.created_at DESC, r.user_reports_seq DESC
             LIMIT 200
            """, nativeQuery = true)
    List<AdminReportRow> findMadeForAdmin(@Param("userId") String userId);
}
