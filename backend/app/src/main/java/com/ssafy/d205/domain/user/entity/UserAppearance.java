package com.ssafy.d205.domain.user.entity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;

/**
 * 캐릭터 외형. 유저당 한 행이고, 행이 없으면 아직 고르지 않은 것입니다.
 *
 * <p>네 파츠는 <b>모두 클라이언트가 정한 id</b>입니다. 서버는 어떤 id 가 실제로 있는지
 * 모르고 {@link AppearancePolicy}의 형식만 봅니다. 그래서 이 엔티티에는 "기본값"이 없습니다.
 * 기본 파츠가 무엇인지는 클라이언트만 알고, 초기화는 행을 지우는 것입니다.
 *
 * <p>쓰기는 이 엔티티를 거치지 않습니다. {@code UserAppearanceRepository.upsert} 가
 * 한 문장으로 넣거나 덮어씁니다. 그래서 여기에는 값을 바꾸는 메서드가 없고, 읽기용입니다.
 */
@Entity
@Table(name = "user_appearances")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class UserAppearance {

    /** PK 가 곧 users_seq 입니다. */
    @Id
    @Column(name = "user_seq")
    private Integer userSeq;

    @Column(name = "body_color", nullable = false, length = AppearancePolicy.MAX_LENGTH)
    private String bodyColor;

    @Column(name = "hood", nullable = false, length = AppearancePolicy.MAX_LENGTH)
    private String hood;

    @Column(name = "shoes", nullable = false, length = AppearancePolicy.MAX_LENGTH)
    private String shoes;

    @Column(name = "face", nullable = false, length = AppearancePolicy.MAX_LENGTH)
    private String face;

    @Column(name = "updated_at", nullable = false, length = 14)
    private String updatedAt;
}
