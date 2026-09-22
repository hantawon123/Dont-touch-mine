package com.ssafy.d205.domain.admin.repository;

import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.util.List;

import com.ssafy.d205.domain.admin.entity.NicknameAudit;

/**
 * 개명 감사 로그 조회 (S15P21D205-1047).
 *
 * <p>쓰기는 save 하나뿐이라 따로 선언하지 않습니다. 읽기는 사용자 상세의 이력 하나입니다.
 *
 * <p>상한을 쿼리 안에 박아 두는 이유는 {@link SuspensionAuditRepository} 와 같습니다.
 */
public interface NicknameAuditRepository extends JpaRepository<NicknameAudit, Integer> {

    /**
     * 한 사람의 개명 이력. 최신순입니다.
     *
     * <p>user_seq 가 아니라 public_id 로 찾습니다. 탈퇴해서 user_seq 가 NULL 이 된 행도 같은
     * 사람의 이력으로 묶여야 하기 때문입니다.
     *
     * <p>정렬은 acted_at 이 아니라 seq 입니다. 같은 초에 두 번 눌렀을 때도 순서가 정해집니다.
     */
    @Query(value = """
            SELECT a.user_public_id  AS userId,
                   a.before_nickname AS beforeNickname,
                   a.after_nickname  AS afterNickname,
                   a.reason          AS reason,
                   a.admin_username  AS adminUsername,
                   a.acted_at        AS actedAt,
                   a.user_seq        AS userSeq
              FROM nickname_audit a
             WHERE a.user_public_id = :userId
             ORDER BY a.nickname_audit_seq DESC
             LIMIT 200
            """, nativeQuery = true)
    List<NicknameAuditRow> findHistoryFor(@Param("userId") String userId);
}
