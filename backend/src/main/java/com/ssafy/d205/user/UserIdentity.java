package com.ssafy.d205.user;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.FetchType;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.ManyToOne;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;

import com.ssafy.d205.common.Timestamps;

/**
 * 계정에 연결된 외부 신원.
 *
 * <p>지금은 DEVICE 하나뿐이고, Steam이 붙으면 같은 유저에 STEAM 행이 추가됩니다.
 * users에 steam_id 컬럼을 두지 않는 이유는 플랫폼이 늘 때마다 컬럼이 늘고 한 계정에
 * 기기를 여러 대 연결할 수 없기 때문입니다.
 */
@Entity
@Table(name = "user_identities")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class UserIdentity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    @Column(name = "user_identities_seq")
    private Integer seq;

    /**
     * LAZY입니다. 신원만 필요한 조회에서 users를 항상 함께 읽을 이유가 없습니다.
     *
     * <p>open-in-view가 꺼져 있으므로 이 값을 꺼내 쓰는 코드는 트랜잭션 안에
     * 있어야 합니다. 밖에서 만지면 LazyInitializationException이 납니다.
     */
    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "user_seq", nullable = false, updatable = false)
    private User user;

    @Enumerated(EnumType.STRING)
    @Column(name = "provider", nullable = false, length = 20, updatable = false)
    private AuthProvider provider;

    /**
     * DEVICE는 클라이언트가 첫 실행에 만든 식별자, STEAM은 SteamID64입니다.
     *
     * <p><b>이 값은 자격증명입니다.</b> public_id와 성질이 정반대이므로 어떤 API
     * 응답에도 담아서는 안 됩니다. 응답 DTO에 이 필드를 넣지 마세요.
     */
    @Column(name = "provider_user_id", nullable = false, length = 36, updatable = false)
    private String providerUserId;

    @Column(name = "linked_at", nullable = false, length = 14, updatable = false)
    private String linkedAt;

    private UserIdentity(User user, AuthProvider provider, String providerUserId) {
        this.user = user;
        this.provider = provider;
        this.providerUserId = providerUserId;
        this.linkedAt = Timestamps.now();
    }

    public static UserIdentity link(User user, AuthProvider provider, String providerUserId) {
        return new UserIdentity(user, provider, providerUserId);
    }
}
