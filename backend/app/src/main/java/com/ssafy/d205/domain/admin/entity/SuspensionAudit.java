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
 * 운영자가 계정을 정지하거나 해제한 기록 한 건 (S15P21D205-974).
 *
 * <p><b>추가만 합니다.</b> 고치거나 지우는 메서드가 없는 것은 빠뜨린 것이 아닙니다. 고칠 수
 * 있는 감사 로그는 감사 로그가 아닙니다. users 의 정지 상태는 덮어쓰이지만 이쪽은 쌓이기만
 * 합니다.
 *
 * <p>대상의 식별자와 그 시점 닉네임을 이 행에 복사해 둡니다. 탈퇴하면 userSeq 는 NULL 이
 * 되지만 화면은 여전히 누구를 정지했는지 말할 수 있어야 합니다. 닉네임을 지금 값으로 조인하지
 * 않는 이유도 같습니다 - 개명한 계정의 이력이 운영자가 그때 본 이름과 달라지면 안 됩니다.
 */
@Entity
@Table(name = "suspension_audit")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class SuspensionAudit {

    /** 정지. 이미 정지된 계정에 사유만 고쳐 다시 누른 경우에도 이 값으로 한 행이 남습니다. */
    public static final String SUSPEND = "SUSPEND";

    /** 해제. 정지 상태가 아닌 계정에 눌러도 한 행이 남습니다 - 운영자가 실제로 누른 일입니다. */
    public static final String LIFT = "LIFT";

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    @Column(name = "suspension_audit_seq")
    private Integer seq;

    /** 대상 계정. <b>탈퇴하면 NULL 이 됩니다.</b> 기록 자체는 남습니다. */
    @Column(name = "user_seq")
    private Integer userSeq;

    /** 대상의 공개 식별자. 탈퇴해도 남으므로 이력 조회는 이 값으로 묶습니다. */
    @Column(name = "user_public_id", nullable = false, length = 36, updatable = false)
    private String userPublicId;

    /** 누른 시점의 닉네임. 지금 닉네임이 아닙니다. */
    @Column(name = "user_nickname", nullable = false, length = 32, updatable = false)
    private String userNickname;

    /** SUSPEND 또는 LIFT. */
    @Column(name = "action", nullable = false, length = 8, updatable = false)
    private String action;

    /** 정지 사유. 해제는 null 입니다. */
    @Column(name = "reason", length = 200, updatable = false)
    private String reason;

    /** 누른 운영자의 계정 이름. */
    @Column(name = "admin_username", nullable = false, length = 64, updatable = false)
    private String adminUsername;

    /** 누른 시각. yyyyMMddHHmmss, UTC. */
    @Column(name = "acted_at", nullable = false, length = 14, updatable = false)
    private String actedAt;

    private SuspensionAudit(User user, String action, String reason, String adminUsername, String at) {
        this.userSeq = user.getSeq();
        this.userPublicId = user.getPublicId();
        this.userNickname = user.getNickname();
        this.action = action;
        this.reason = reason;
        this.adminUsername = adminUsername;
        this.actedAt = at;
    }

    /** 정지 기록. 사유는 컨트롤러가 필수로 받으므로 여기서 비어 있을 수 없습니다. */
    public static SuspensionAudit suspended(User user, String reason, String adminUsername, String at) {
        return new SuspensionAudit(user, SUSPEND, reason, adminUsername, at);
    }

    /**
     * 해제 기록. 사유는 없습니다. 해제할 때 운영자에게 사유를 받지 않기 때문이고, 받지 않는
     * 이유는 해제가 "정지를 되돌린다"는 한 가지 뜻뿐이라 적을 것이 없어서입니다.
     */
    public static SuspensionAudit lifted(User user, String adminUsername, String at) {
        return new SuspensionAudit(user, LIFT, null, adminUsername, at);
    }
}
