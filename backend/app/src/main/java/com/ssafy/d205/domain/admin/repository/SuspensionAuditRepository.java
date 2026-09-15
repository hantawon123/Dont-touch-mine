package com.ssafy.d205.domain.admin.repository;

import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.util.List;

import com.ssafy.d205.domain.admin.entity.SuspensionAudit;

/**
 * 정지 감사 로그 조회 (S15P21D205-974).
 *
 * <p>쓰기는 save 하나뿐이라 따로 선언하지 않습니다. 읽기는 셋입니다 - 한 사람의 이력, 지금
 * 정지된 계정 목록, 최근 해제 이력.
 *
 * <p>상한을 쿼리 안에 박아 둡니다. 운영자 화면이 한 번에 훑는 양이고, 감사 로그는 지우지 않아
 * 계속 늘기 때문입니다. 페이지를 나누지 않는 이유는 지금 정지 대상이 수십 명 규모라서입니다 -
 * 그 규모를 넘으면 그때 페이지를 답니다.
 */
public interface SuspensionAuditRepository extends JpaRepository<SuspensionAudit, Integer> {

    /**
     * 한 사람의 정지·해제 이력. 최신순입니다.
     *
     * <p>user_seq 가 아니라 public_id 로 찾습니다. 탈퇴해서 user_seq 가 NULL 이 된 행도 같은
     * 사람의 이력으로 묶여야 하기 때문입니다.
     */
    @Query(value = """
            SELECT a.user_public_id     AS userId,
                   a.user_nickname      AS nickname,
                   a.action             AS action,
                   a.reason             AS reason,
                   a.admin_username     AS adminUsername,
                   a.acted_at           AS actedAt,
                   a.user_seq           AS userSeq
              FROM suspension_audit a
             WHERE a.user_public_id = :userId
             ORDER BY a.suspension_audit_seq DESC
             LIMIT 200
            """, nativeQuery = true)
    List<SuspensionAuditRow> findHistoryFor(@Param("userId") String userId);

    /**
     * 지금 정지된 계정 목록. 정지 시각이 최근인 순입니다.
     *
     * <p>감사 로그가 아니라 users 를 기준으로 셉니다. 지금 정지 상태인지는 users 만이 압니다 -
     * 감사 로그의 마지막 행으로 추정하면 이 테이블이 생기기 전에 정지된 계정이 빠집니다.
     *
     * <p>처리자는 그 계정의 마지막 정지 행에서 가져오고, 없으면 null 입니다. 이 테이블이 생기기
     * 전의 정지가 그렇습니다.
     */
    @Query(value = """
            SELECT u.public_id        AS userId,
                   u.nickname         AS nickname,
                   u.suspended_at     AS suspendedAt,
                   u.suspended_reason AS reason,
                   (SELECT a.admin_username
                      FROM suspension_audit a
                     WHERE a.user_public_id = u.public_id
                       AND a.action = 'SUSPEND'
                     ORDER BY a.suspension_audit_seq DESC
                     LIMIT 1)         AS adminUsername
              FROM users u
             WHERE u.suspended_at IS NOT NULL
             ORDER BY u.suspended_at DESC, u.users_seq DESC
             LIMIT 200
            """, nativeQuery = true)
    List<SuspendedAccountRow> findSuspendedAccounts();

    /**
     * 최근 해제 이력. 지금 정지된 목록만 보면 "왜 풀렸나"에 답할 수 없어서 함께 냅니다.
     */
    @Query(value = """
            SELECT a.user_public_id     AS userId,
                   a.user_nickname      AS nickname,
                   a.action             AS action,
                   a.reason             AS reason,
                   a.admin_username     AS adminUsername,
                   a.acted_at           AS actedAt,
                   a.user_seq           AS userSeq
              FROM suspension_audit a
             WHERE a.action = 'LIFT'
             ORDER BY a.suspension_audit_seq DESC
             LIMIT 50
            """, nativeQuery = true)
    List<SuspensionAuditRow> findRecentLifts();
}
