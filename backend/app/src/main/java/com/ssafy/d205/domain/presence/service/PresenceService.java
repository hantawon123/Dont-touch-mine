package com.ssafy.d205.domain.presence.service;

import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.Collection;
import java.util.Optional;

import com.ssafy.d205.domain.presence.entity.SessionKind;
import com.ssafy.d205.domain.presence.entity.UserPresence;
import com.ssafy.d205.domain.presence.repository.UserPresenceRepository;
import com.ssafy.d205.domain.user.entity.User;
import com.ssafy.d205.domain.user.repository.UserRepository;
import com.ssafy.d205.global.common.TimeProvider;
import com.ssafy.d205.global.exception.UnknownCallerException;

@Service
@RequiredArgsConstructor
public class PresenceService {

    private final UserPresenceRepository userPresenceRepository;
    private final UserRepository userRepository;
    private final TimeProvider timeProvider;

    /**
     * REST 로 들어온 상태 보고. 접속 기록이 없으면 만들고, 있으면 갱신합니다.
     *
     * <p>클라이언트는 <b>상태가 바뀌는 순간에만</b> 부릅니다. 주기 호출은 없앴습니다 —
     * 접속해 있다는 사실은 알림 WebSocket 연결이 살아 있는 것으로 알고, 하트비트는
     * 서버가 밉니다(PresenceHeartbeat).
     *
     * <p>알림 채널에 붙은 클라이언트는 이 엔드포인트 대신 {@code PRESENCE} 프레임을
     * 씁니다. 여기가 남아 있는 이유는 프레임을 아직 쓰지 않는 클라이언트와, 알림 채널이
     * 끊긴 사이에도 방 이동을 알려야 하는 경우입니다.
     *
     * <p>로비에서 경기로 넘어가는 것도 그 순간에 한 번 불러야 합니다. 룸이 바뀌지 않아
     * sessionId 는 그대로이고 sessionKind 만 달라집니다.
     */
    @Transactional
    public void report(String callerUserId, String sessionId, SessionKind sessionKind) {
        reportBound(caller(callerUserId).getSeq(), sessionId, sessionKind);
    }

    /**
     * 알림 채널의 {@code PRESENCE} 프레임으로 들어온 상태 보고.
     *
     * <p>public_id 대신 users_seq 를 받습니다. 프레임을 보낸 연결은 이미 HELLO 로 사람에
     * 묶여 있어 레지스트리가 seq 를 알고 있고, REST 처럼 users 를 다시 조회할 이유가
     * 없습니다. 방을 오갈 때마다 나가던 조회 하나가 없어집니다.
     */
    @Transactional
    public void reportBound(Integer userSeq, String sessionId, SessionKind sessionKind) {
        String now = timeProvider.now();
        Optional<UserPresence> existing = userPresenceRepository.findById(userSeq);
        if (existing.isPresent()) {
            existing.get().report(sessionId, sessionKind, now);
        } else {
            userPresenceRepository.save(UserPresence.of(userSeq, sessionId, sessionKind, now));
        }
    }

    /**
     * 알림 채널에 붙었습니다. 접속 기록이 없으면 ONLINE 으로 만듭니다.
     *
     * <p>이미 룸 안이면 상태를 그대로 두는 판단은 엔티티에 있습니다. 두 번째 탭이나
     * 경기 중 재접속이 사람을 방에서 끌어내면 안 됩니다.
     */
    @Transactional
    public void connected(Integer userSeq) {
        String now = timeProvider.now();
        userPresenceRepository.findById(userSeq)
                .ifPresentOrElse(p -> p.connected(now),
                                 () -> userPresenceRepository.save(UserPresence.online(userSeq, now)));
    }

    /** 정상 종료. 타임아웃을 기다리지 않고 친구 목록에서 바로 내려갑니다. */
    @Transactional
    public void goOffline(String callerUserId) {
        disconnected(caller(callerUserId).getSeq());
    }

    /** 알림 채널의 마지막 연결이 끊겼습니다. 정상 종료와 결과가 같습니다. */
    @Transactional
    public void disconnected(Integer userSeq) {
        // 접속 기록이 없으면 이미 오프라인입니다. 행을 만들 이유가 없습니다.
        String now = timeProvider.now();
        userPresenceRepository.findById(userSeq).ifPresent(p -> p.goOffline(now));
    }

    /**
     * 붙어 있는 사람들의 하트비트를 한 문장으로 밉니다.
     *
     * @return 갱신된 행 수. 접속 기록이 아직 없는 사람은 세지 않으므로 넘긴 수보다 작을 수 있습니다
     */
    @Transactional
    public int refreshHeartbeats(Collection<Integer> userSeqs) {
        if (userSeqs.isEmpty()) {
            return 0;
        }
        return userPresenceRepository.refreshHeartbeats(userSeqs, timeProvider.now());
    }

    private User caller(String userId) {
        return userRepository.findByPublicId(userId)
                .orElseThrow(() -> new UnknownCallerException(userId));
    }
}
