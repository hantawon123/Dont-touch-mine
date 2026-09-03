package com.ssafy.d205.domain.presence.service;

import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.Optional;

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
     * 하트비트를 받습니다. 접속 기록이 없으면 만들고, 있으면 갱신합니다.
     *
     * <p>클라이언트는 <b>상태가 바뀌는 순간 즉시 한 번</b> 부르고, 그 위에 30초 주기로
     * 부릅니다. 주기만 쓰면 방에 들어간 직후 최대 30초간 ONLINE 으로 보입니다. Fusion 의
     * OnPlayerJoined 와 OnDisconnectedFromServer 에서 부르면 그 지연이 없어집니다.
     * 주기 호출은 크래시 감지 전용입니다.
     */
    @Transactional
    public void report(String callerUserId, String sessionId) {
        User caller = caller(callerUserId);

        String now = timeProvider.now();
        Optional<UserPresence> existing = userPresenceRepository.findById(caller.getSeq());
        if (existing.isPresent()) {
            existing.get().report(sessionId, now);
        } else {
            userPresenceRepository.save(UserPresence.of(caller.getSeq(), sessionId, now));
        }
    }

    /** 정상 종료. 타임아웃을 기다리지 않고 친구 목록에서 바로 내려갑니다. */
    @Transactional
    public void goOffline(String callerUserId) {
        User caller = caller(callerUserId);

        // 접속 기록이 없으면 이미 오프라인입니다. 행을 만들 이유가 없습니다.
        String now = timeProvider.now();
        userPresenceRepository.findById(caller.getSeq()).ifPresent(p -> p.goOffline(now));
    }

    private User caller(String userId) {
        return userRepository.findByPublicId(userId)
                .orElseThrow(() -> new UnknownCallerException(userId));
    }
}
