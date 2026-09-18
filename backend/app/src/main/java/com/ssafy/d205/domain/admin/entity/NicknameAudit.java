package com.ssafy.d205.domain.admin.entity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;

import com.ssafy.d205.domain.user.entity.User;

/**
 * 운영자가 남의 닉네임을 바꾼 기록 한 건 (S15P21D205-1047).
 *
 * <p><b>추가만 합니다.</b> {@link SuspensionAudit} 와 같은 이유입니다 - 고칠 수 있는 감사 로그는
 * 감사 로그가 아닙니다. users 의 닉네임은 덮어쓰이지만 이쪽은 쌓이기만 합니다.
 *
 * <p>바꾸기 전 이름을 이 행에 남기는 것이 핵심입니다. 부적절한 닉네임을 치우는 것이 이 기능의
 * 목적인데, 치운 이름을 남기지 않으면 무엇을 왜 치웠는지가 함께 사라집니다.
 *
 * <p>사용자가 스스로 바꾼 개명은 여기에 남지 않습니다. 이 테이블은 계정의 이름 변경 이력이 아니라
 * 운영자가 한 행위의 기록입니다.
 */
@Entity
@Table(name = "nickname_audit")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class NicknameAudit {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    @Column(name = "nickname_audit_seq")
    private Integer seq;

    /** 대상 계정. <b>탈퇴하면 NULL 이 됩니다.</b> 기록 자체는 남습니다. */
    @Column(name = "user_seq")
    private Integer userSeq;

    /** 대상의 공개 식별자. 탈퇴해도 남으므로 이력 조회는 이 값으로 묶습니다. */
    @Column(name = "user_public_id", nullable = false, length = 36, updatable = false)
    private String userPublicId;

    /** 바꾸기 전 이름. 부적절한 닉네임을 치운 경우 이 값이 그 판단의 근거입니다. */
    @Column(name = "before_nickname", nullable = false, length = 32, updatable = false)
    private String beforeNickname;

    /** 바꾼 뒤 이름. 그 뒤에 또 바뀌었더라도 이 행은 그때 붙인 이름을 말해야 합니다. */
    @Column(name = "after_nickname", nullable = false, length = 32, updatable = false)
    private String afterNickname;

    /** 운영자가 적은 사유. 부적절한 닉네임 버튼은 고정 문구입니다. */
    @Column(name = "reason", nullable = false, length = 200, updatable = false)
    private String reason;

    /** 누른 운영자의 계정 이름. */
    @Column(name = "admin_username", nullable = false, length = 64, updatable = false)
    private String adminUsername;

    /** 누른 시각. yyyyMMddHHmmss, UTC. */
    @Column(name = "acted_at", nullable = false, length = 14, updatable = false)
    private String actedAt;

    /**
     * <b>닉네임을 바꾸기 전에 만들어야 합니다.</b> 바꾸기 전 이름을 엔티티에서 읽기 때문에,
     * {@code user.renameByAdmin(...)} 뒤에 부르면 before 와 after 가 같은 값이 됩니다.
     */
    public static NicknameAudit of(User user, String after, String reason, String adminUsername, String at) {
        NicknameAudit audit = new NicknameAudit();
        audit.userSeq = user.getSeq();
        audit.userPublicId = user.getPublicId();
        audit.beforeNickname = user.getNickname();
        audit.afterNickname = after;
        audit.reason = reason;
        audit.adminUsername = adminUsername;
        audit.actedAt = at;
        return audit;
    }
}
