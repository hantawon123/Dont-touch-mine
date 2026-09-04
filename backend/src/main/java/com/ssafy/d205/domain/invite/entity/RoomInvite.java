package com.ssafy.d205.domain.invite.entity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;

/**
 * 친구에게 건넨 방 코드.
 *
 * <p>서버는 실시간 통신을 하지 않으므로 초대는 밀어주는 것이 아니라 받는 쪽이 주기적으로
 * 조회해서 가져갑니다. 그래서 이 행은 "지금 유효한 초대"를 뜻하고, 유효 기간이 지나면
 * 조회에서 빠집니다.
 *
 * <p><b>거절은 상태가 아니라 행 삭제입니다.</b> REJECTED 같은 값을 남기면
 * uk_room_invites_target 때문에 같은 사람을 같은 방에 다시 부를 수 없게 됩니다.
 * friendships 가 같은 이유로 같은 선택을 했습니다.
 *
 * <p><b>방 코드는 검증하지 않습니다.</b> 이 서버는 Photon 의 방 목록을 모릅니다. 코드가
 * 가리키는 방이 아직 있는지는 클라이언트가 입장을 시도할 때 알게 되고, 없으면 입장이
 * 실패합니다. 만료 시간은 그 실패를 줄이려는 것이지 없애는 장치가 아닙니다.
 */
@Entity
@Table(name = "room_invites")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class RoomInvite {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    @Column(name = "room_invites_seq")
    private Integer seq;

    @Column(name = "inviter_seq", nullable = false)
    private Integer inviterSeq;

    @Column(name = "invitee_seq", nullable = false)
    private Integer inviteeSeq;

    @Column(name = "room_code", nullable = false, length = 6)
    private String roomCode;

    /** 보낸 시각. yyyyMMddHHmmss, UTC. 만료 판정의 기준입니다. */
    @Column(name = "created_at", nullable = false, length = 14)
    private String createdAt;

    private RoomInvite(Integer inviterSeq, Integer inviteeSeq, String roomCode, String now) {
        this.inviterSeq = inviterSeq;
        this.inviteeSeq = inviteeSeq;
        this.roomCode = roomCode;
        this.createdAt = now;
    }

    public static RoomInvite of(Integer inviterSeq, Integer inviteeSeq, String roomCode, String now) {
        return new RoomInvite(inviterSeq, inviteeSeq, roomCode, now);
    }

    /**
     * 같은 사람을 같은 방에 다시 부릅니다.
     *
     * <p>새 행이 아니라 시각만 새로 씁니다. 초대는 "이 방으로 오라"는 한 가지 뜻이고
     * 두 번 눌렀다고 두 개가 되지는 않습니다. 다시 부르면 만료 시계가 처음부터 갑니다.
     */
    public void renew(String now) {
        this.createdAt = now;
    }
}
