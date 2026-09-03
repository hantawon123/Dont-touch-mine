package com.ssafy.d205.domain.friend.entity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;

import com.ssafy.d205.global.common.Timestamps;

/**
 * 두 사용자 사이의 친구 관계. 요청과 성립을 한 행으로 다룹니다.
 *
 * <p>하나의 관계를 한 행으로만 저장하기 위해 두 seq 를 <b>크기 순으로 정렬</b>해서
 * 넣습니다. 그래서 A가 B에게 요청한 뒤 B가 A에게 요청해도 uk_friendships_pair 가
 * 막습니다. 애플리케이션에서 양방향을 조회해 검사하는 방식은 동시 요청에 뚫립니다.
 *
 * <p>정렬은 {@link #request} 에서만 합니다. DB의 ck_friendships_order 가 뒷받침하므로
 * 다른 경로로 정렬이 어긋난 행이 들어가면 기동이 아니라 삽입 시점에 실패합니다.
 *
 * <p>연관을 @ManyToOne 으로 걸지 않고 seq 를 그대로 둡니다. 정렬 규칙을 다루는 코드가
 * seq 만 필요하고, 연관을 셋 걸면 목록 조회에서 쓰지도 않는 조회가 따라붙습니다.
 * 닉네임이 필요한 목록은 조인 쿼리로 가져옵니다.
 */
@Entity
@Table(name = "friendships")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class Friendship {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    @Column(name = "friendships_seq")
    private Integer seq;

    /** 항상 두 seq 중 작은 값입니다. */
    @Column(name = "user_low_seq", nullable = false, updatable = false)
    private Integer userLowSeq;

    /** 항상 두 seq 중 큰 값입니다. */
    @Column(name = "user_high_seq", nullable = false, updatable = false)
    private Integer userHighSeq;

    /**
     * 누가 요청을 보냈는지.
     *
     * <p>관계를 대칭으로 저장하므로 이 값이 없으면 수락 버튼을 어느 쪽에 보여줄지
     * 알 수 없습니다.
     */
    @Column(name = "requested_by_seq", nullable = false, updatable = false)
    private Integer requestedBySeq;

    @Enumerated(EnumType.STRING)
    @Column(name = "status", nullable = false, length = 16)
    private FriendshipStatus status;

    @Column(name = "created_at", nullable = false, length = 14, updatable = false)
    private String createdAt;

    /** PENDING 동안 NULL 입니다. */
    @Column(name = "accepted_at", length = 14)
    private String acceptedAt;

    private Friendship(int lowSeq, int highSeq, int requestedBySeq, FriendshipStatus status, String at) {
        this.userLowSeq = lowSeq;
        this.userHighSeq = highSeq;
        this.requestedBySeq = requestedBySeq;
        this.status = status;
        this.createdAt = at;
        this.acceptedAt = status == FriendshipStatus.ACCEPTED ? at : null;
    }

    /** 요청 상태로 만듭니다. 두 seq 를 정렬해 넣습니다. */
    public static Friendship request(int requesterSeq, int targetSeq) {
        return new Friendship(Math.min(requesterSeq, targetSeq),
                              Math.max(requesterSeq, targetSeq),
                              requesterSeq,
                              FriendshipStatus.PENDING,
                              Timestamps.now());
    }

    public void accept() {
        this.status = FriendshipStatus.ACCEPTED;
        this.acceptedAt = Timestamps.now();
    }

    public boolean isPending() {
        return status == FriendshipStatus.PENDING;
    }

    public boolean isRequestedBy(int userSeq) {
        return requestedBySeq == userSeq;
    }
}
