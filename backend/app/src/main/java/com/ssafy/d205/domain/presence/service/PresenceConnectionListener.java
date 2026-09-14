package com.ssafy.d205.domain.presence.service;

import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.context.event.EventListener;
import org.springframework.stereotype.Component;
import org.springframework.transaction.annotation.Propagation;
import org.springframework.transaction.annotation.Transactional;

import com.ssafy.d205.domain.notification.event.UserConnectedEvent;
import com.ssafy.d205.domain.notification.event.UserDisconnectedEvent;

/**
 * 알림 연결이 열리고 닫히는 것을 접속 상태에 반영합니다.
 *
 * <p>이것이 <b>주기 하트비트를 없앨 수 있는 근거</b>입니다(S15P21D205-890). 연결이 살아
 * 있다는 사실 자체가 온라인 신호이므로, 클라이언트가 30초마다 그 사실을 다시 알려 줄
 * 필요가 없습니다.
 *
 * <p><b>REQUIRES_NEW 가 필요합니다.</b> 끊김은 세 경로로 오는데, 그중 하나가 알림을 보내다
 * 죽은 연결을 발견하는 것이고 그 발송은 요청 트랜잭션의 AFTER_COMMIT 단계에서 돕니다.
 * 그 자리에서 기본 REQUIRED 로 쓰면 이미 커밋을 끝낸 트랜잭션에 참여해 <b>UPDATE 가
 * 조용히 사라집니다.</b> 새 트랜잭션을 열어야 실제로 커밋됩니다. 트랜잭션이 없는
 * 스케줄러·컨테이너 스레드에서는 REQUIRED 와 똑같이 동작하므로 한 정책으로 둡니다.
 *
 * <p><b>여기서 나는 예외는 여기서 끝냅니다.</b> 접속 상태를 쓰지 못한 것 때문에 알림
 * 채널이 끊기면 실시간 알림 전체를 잃습니다. 알림은 접속 상태 없이도 동작하므로 로그만
 * 남깁니다. 클라이언트가 다시 붙으면 같은 쓰기를 다시 시도합니다.
 */
@Component
@RequiredArgsConstructor
@Slf4j
public class PresenceConnectionListener {

    private final PresenceService presenceService;

    @EventListener
    @Transactional(propagation = Propagation.REQUIRES_NEW)
    public void onConnected(UserConnectedEvent event) {
        try {
            presenceService.connected(event.userSeq());
        } catch (RuntimeException e) {
            log.warn("user_seq {} 의 접속을 기록하지 못했습니다. 친구 목록에서 오프라인으로 "
                    + "보이지만 알림은 정상입니다.", event.userSeq(), e);
        }
    }

    @EventListener
    @Transactional(propagation = Propagation.REQUIRES_NEW)
    public void onDisconnected(UserDisconnectedEvent event) {
        try {
            presenceService.disconnected(event.userSeq());
        } catch (RuntimeException e) {
            // 놓쳐도 영구적이지 않습니다. 하트비트가 더 이상 밀리지 않으므로 90초 뒤
            // 스윕이 같은 일을 합니다. 즉시 내려가지 않는 것이 차이입니다.
            log.warn("user_seq {} 의 종료를 기록하지 못했습니다. 스윕이 타임아웃 뒤 내립니다.",
                    event.userSeq(), e);
        }
    }
}
