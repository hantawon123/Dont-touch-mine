package com.ssafy.d205.domain.user.entity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;

import java.util.UUID;

/**
 * 게임 계정.
 *
 * <p>로그인 자격증명은 {@link UserIdentity}가 들고 있습니다. 이 엔티티에는 없습니다.
 */
@Entity
@Table(name = "users")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class User {

    /**
     * 내부 식별자. 절대 외부로 내보내지 않습니다.
     *
     * <p>컬럼이 INT UNSIGNED라 Long이 아니라 Integer입니다. Long으로 두면 Hibernate가
     * BIGINT를 기대해서 ddl-auto=validate가 기동 시점에 막습니다.
     */
    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    @Column(name = "users_seq")
    private Integer seq;

    /**
     * 외부에 노출하는 식별자. API 응답과 Photon UserId에 이 값을 씁니다.
     *
     * <p>seq를 내보내면 가입자 수가 드러나고 숫자를 하나씩 올려 다른 계정을 순회할 수
     * 있습니다. 한 번 정해지면 바뀌지 않습니다.
     */
    @Column(name = "public_id", nullable = false, length = 36, updatable = false)
    private String publicId;

    @Column(name = "nickname", nullable = false, length = 32)
    private String nickname;

    /**
     * 사용자가 닉네임을 직접 정한 시각. 아직 정하지 않았으면 NULL입니다.
     *
     * <p>발급 때 서버가 지어준 임시 닉네임인지, 사용자가 고른 것인지 구분하기 위한
     * 값입니다. 발급이 멱등해서 두 번째 요청부터는 200이 나가는데, 그것만으로는
     * 클라이언트가 입력 화면을 띄워야 하는지 알 수 없습니다.
     */
    @Column(name = "nickname_set_at", length = 14)
    private String nicknameSetAt;

    /**
     * 닉네임 검색에 나올지. 기본은 나옵니다.
     *
     * <p><b>검색에서만 빠집니다.</b> 친구 목록과 받은 요청과 받은 초대에서는 이름이
     * 그대로 보이고, userId 를 아는 사람이 보낸 친구 요청도 그대로 도착합니다.
     * 이미 아는 사람과의 화면까지 가리면 그쪽이 통째로 깨집니다.
     *
     * <p>그래도 뜻이 성립하는 이유는 userId 가 UUIDv4 라서입니다. 검색으로 찾지
     * 못하면 그 값을 알 길이 사실상 없습니다.
     */
    @Column(name = "searchable", nullable = false)
    private boolean searchable = true;

    /**
     * 운영자가 계정을 정지한 시각. 정상이면 NULL 입니다.
     *
     * <p>NULL 이 "정상"인 이유는 V16 에 있습니다. 요약하면 불리언으로 두었을 때 언제
     * 정지했는지가 남지 않아서입니다.
     *
     * <p><b>기한이 없습니다.</b> 정지와 해제 두 상태뿐이고 해제는 사람이 누릅니다.
     */
    @Column(name = "suspended_at", length = 14)
    private String suspendedAt;

    /** 정지 사유. 정지 상태일 때만 값이 있습니다. */
    @Column(name = "suspended_reason", length = 200)
    private String suspendedReason;

    @Column(name = "created_at", nullable = false, length = 14, updatable = false)
    private String createdAt;

    @Column(name = "updated_at", nullable = false, length = 14)
    private String updatedAt;

    private User(String publicId, String nickname, String at) {
        this.publicId = publicId;
        this.nickname = nickname;
        this.createdAt = at;
        this.updatedAt = at;
    }

    /**
     * public_id는 애플리케이션이 UUIDv4로 만듭니다. DB의 AUTO_INCREMENT와 달리
     * 값을 미리 알 수 있어야 하고, 순서를 유추할 수 없어야 하기 때문입니다.
     */
    public static User create(String nickname, String now) {
        return new User(UUID.randomUUID().toString(), nickname, now);
    }

    /**
     * 검색에 나올지 정합니다. <b>멱등합니다.</b> 같은 값을 다시 넣어도 됩니다.
     *
     * <p>updated_at 을 함께 갱신합니다. 사용자가 자기 계정에 한 변경이므로 닉네임을
     * 바꾼 것과 같은 성질입니다.
     */
    public void setSearchable(boolean searchable, String now) {
        this.searchable = searchable;
        this.updatedAt = now;
    }

    /** 지금 정지 상태인가. */
    public boolean isSuspended() {
        return suspendedAt != null;
    }

    /**
     * 계정을 정지합니다. <b>멱등합니다.</b> 이미 정지된 계정을 다시 정지하면 사유와
     * 시각이 새 값으로 바뀝니다.
     *
     * <p>덮어쓰는 쪽을 고른 이유는 운영자가 사유를 고쳐 적는 일이 실제로 있기 때문입니다.
     * 두 번째 요청을 거절하면 고치려면 해제했다가 다시 정지해야 하는데, 그 사이에 그
     * 사람이 들어올 수 있습니다.
     *
     * <p>updated_at 은 건드리지 않습니다. 그것은 사용자가 자기 계정에 한 변경의 시각이고,
     * 정지는 남이 건 것입니다. 섞으면 "내가 마지막으로 바꾼 때"를 묻는 화면이 거짓말을 합니다.
     */
    public void suspend(String reason, String now) {
        this.suspendedAt = now;
        this.suspendedReason = reason;
    }

    /** 정지를 해제합니다. <b>멱등합니다.</b> 정지 상태가 아니어도 부를 수 있습니다. */
    public void lift() {
        this.suspendedAt = null;
        this.suspendedReason = null;
    }

    public void rename(String nickname, String now) {
        this.nickname = nickname;
        this.updatedAt = now;

        // 첫 변경에서만 채웁니다. 매번 갱신하면 "정한 시각"이 아니라 "마지막으로 바꾼
        // 시각"이 되어 이름과 내용이 어긋납니다. 그 값은 updated_at이 들고 있습니다.
        if (this.nicknameSetAt == null) {
            this.nicknameSetAt = now;
        }
    }

    /** 서버가 지어준 임시 닉네임을 그대로 쓰고 있으면 false입니다. */
    public boolean isNicknameSet() {
        return nicknameSetAt != null;
    }
}
