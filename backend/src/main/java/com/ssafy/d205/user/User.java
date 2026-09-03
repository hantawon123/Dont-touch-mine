package com.ssafy.d205.user;

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

import com.ssafy.d205.common.Timestamps;

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
    public static User create(String nickname) {
        return new User(UUID.randomUUID().toString(), nickname, Timestamps.now());
    }

    public void rename(String nickname) {
        this.nickname = nickname;
        this.updatedAt = Timestamps.now();
    }
}
