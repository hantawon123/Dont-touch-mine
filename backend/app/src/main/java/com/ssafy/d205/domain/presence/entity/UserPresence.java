package com.ssafy.d205.domain.presence.entity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;

import java.util.Objects;

/**
 * 사용자의 접속 상태. 유저당 한 행입니다.
 *
 * <p>users 와 분리한 이유는 하트비트가 30초마다 UPDATE 를 내기 때문입니다. 거의 바뀌지
 * 않는 계정 데이터와 같은 행에 두면 users 페이지와 인덱스가 계속 더러워집니다.
 *
 * <p><b>이 데이터는 권위가 없습니다.</b> 클라이언트가 자기 상태를 스스로 보고하고 서버는
 * 검증하지 않습니다. 보고를 빼먹으면 틀리고, 앱이 강제 종료되면 타임아웃까지 남아
 * 있습니다. 마음만 먹으면 아무 sessionId 나 주장할 수도 있습니다.
 *
 * <p>그래서 <b>"친구 방에 참여" 같은 기능은 이 값을 신뢰해서는 안 됩니다.</b> 없는 방에
 * 들어가려는 시도가 생길 수 있으므로 그쪽에서 실패를 처리해야 합니다.
 *
 * <p>권위 있게 만들려면 Photon Webhooks 로 방 입장·퇴장을 받아야 합니다. 다만 ONLINE 은
 * Photon 세션 밖의 상태라 Fusion 이 알 수 없고, 그걸 위해 어차피 하트비트가 필요합니다.
 * 하트비트가 필요하면 거기에 sessionId 를 실어 IN_GAME 까지 함께 처리하는 편이 단순합니다.
 * API 모양을 유지했으므로 나중에 Webhooks 로 바꿔도 클라이언트는 손대지 않습니다.
 */
@Entity
@Table(name = "user_presence")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class UserPresence {

    /** PK가 곧 users_seq 입니다. 유저당 한 행이라 대리키가 할 일이 없습니다. */
    @Id
    @Column(name = "user_seq")
    private Integer userSeq;

    @Enumerated(EnumType.STRING)
    @Column(name = "status", nullable = false, length = 16)
    private PresenceStatus status;

    /** Photon 룸 식별자. 룸 안(IN_LOBBY, IN_GAME)이 아니면 NULL 입니다. */
    @Column(name = "session_id", length = 64)
    private String sessionId;

    /**
     * 마지막 생존 신호.
     *
     * <p>이제 <b>서버가</b> 밉니다. 알림 WebSocket 이 살아 있는 사람들의 이 컬럼을 한
     * 문장으로 갱신합니다(PresenceHeartbeat). 클라이언트가 30초마다 PUT 을 보내던
     * 때에는 요청 하나가 이 컬럼 하나를 밀었고, 접속자 수만큼 트랜잭션이 생겼습니다.
     */
    @Column(name = "heartbeat_at", nullable = false, length = 14)
    private String heartbeatAt;

    /** 상태가 마지막으로 바뀐 시각. 하트비트마다 갱신되지 않습니다. */
    @Column(name = "updated_at", nullable = false, length = 14)
    private String updatedAt;

    private UserPresence(Integer userSeq, PresenceStatus status, String sessionId, String at) {
        this.userSeq = userSeq;
        this.status = status;
        this.sessionId = sessionId;
        this.heartbeatAt = at;
        this.updatedAt = at;
    }

    public static UserPresence of(Integer userSeq, String sessionId, SessionKind sessionKind, String now) {
        return new UserPresence(userSeq, statusFor(sessionId, sessionKind), sessionId, now);
    }

    /** 알림 채널에 처음 붙은 사람의 첫 행. 룸 밖이라 ONLINE 입니다. */
    public static UserPresence online(Integer userSeq, String now) {
        return new UserPresence(userSeq, PresenceStatus.ONLINE, null, now);
    }

    /**
     * 하트비트를 반영합니다.
     *
     * <p>heartbeat_at 은 매번 갱신하지만 updated_at 은 상태가 바뀔 때만 갱신합니다.
     * 하트비트는 30초마다 오고 상태 변화는 드물게 일어나므로 둘을 구분해야 "언제부터
     * 이 상태인지"를 알 수 있습니다.
     *
     * <p>방을 옮기는 것도 상태 변화로 봅니다. 상태 이름은 그대로지만 다른 방이라
     * sessionId 가 달라지고, 그 시점을 남기는 것이 맞습니다.
     *
     * <p>로비에서 경기로 넘어가는 것도 상태 변화입니다. 그때는 <b>sessionId 가 그대로</b>
     * 입니다 — 같은 룸이 경기 씬으로 넘어간 것이니까요. 상태 이름이 달라지는 것으로만
     * 알아챌 수 있습니다.
     */
    public void report(String sessionId, SessionKind sessionKind, String now) {
        PresenceStatus next = statusFor(sessionId, sessionKind);
        this.heartbeatAt = now;

        if (this.status != next || !Objects.equals(this.sessionId, sessionId)) {
            this.status = next;
            this.sessionId = sessionId;
            this.updatedAt = now;
        }
    }

    /**
     * 알림 채널에 붙었습니다. 이것이 곧 접속 신호입니다.
     *
     * <p><b>이미 룸 안이면 상태를 건드리지 않습니다.</b> 탭이나 기기를 하나 더 연 것일
     * 수도 있고, 경기 중에 연결이 끊겼다 다시 붙은 것일 수도 있습니다. 어느 쪽이든
     * 붙었다는 사실만으로 사람을 방에서 끌어내면 친구 목록에서 경기 중인 사람이
     * ONLINE 으로 잘못 보입니다.
     *
     * <p>정말 룸 밖으로 나온 클라이언트는 {@code HELLO_ACK} 직후 자기 상태를 다시
     * 보고하므로 그 프레임이 곧바로 교정합니다.
     */
    public void connected(String now) {
        this.heartbeatAt = now;
        if (this.status == PresenceStatus.OFFLINE) {
            this.status = PresenceStatus.ONLINE;
            this.sessionId = null;
            this.updatedAt = now;
        }
    }

    /**
     * 앱을 정상 종료하거나 알림 채널의 마지막 연결이 끊겼을 때 부릅니다. 타임아웃을
     * 기다리지 않고 바로 내려갑니다.
     */
    public void goOffline(String now) {
        this.heartbeatAt = now;
        if (this.status != PresenceStatus.OFFLINE) {
            this.status = PresenceStatus.OFFLINE;
            this.sessionId = null;
            this.updatedAt = now;
        }
    }

    /**
     * 상태를 sessionId 와 sessionKind 로 유도합니다.
     *
     * <p>클라이언트가 status 를 직접 보내지 않는 이유입니다. 보내게 하면 "IN_GAME 인데
     * sessionId 가 없다" 같은 <b>잘못된 조합이 표현 가능</b>해지고, 그걸 막는 검증
     * 규칙을 따로 만들어야 합니다. 유도하면 애초에 표현할 수 없습니다.
     *
     * <p>sessionId 가 없으면 룸 밖이라 sessionKind 를 보지 않습니다. 룸 밖인데 로비라고
     * 주장하는 요청이 와도 상태는 ONLINE 하나로 정해집니다.
     */
    private static PresenceStatus statusFor(String sessionId, SessionKind sessionKind) {
        if (sessionId == null) {
            return PresenceStatus.ONLINE;
        }
        return sessionKind == SessionKind.LOBBY ? PresenceStatus.IN_LOBBY : PresenceStatus.IN_GAME;
    }
}
