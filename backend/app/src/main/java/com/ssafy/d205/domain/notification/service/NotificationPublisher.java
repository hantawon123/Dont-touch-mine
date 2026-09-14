package com.ssafy.d205.domain.notification.service;

import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Component;
import org.springframework.transaction.event.TransactionPhase;
import org.springframework.transaction.event.TransactionalEventListener;
import tools.jackson.databind.ObjectMapper;

import com.ssafy.d205.domain.notification.dto.NotificationMessage;
import com.ssafy.d205.domain.notification.event.UserNotificationEvent;
import com.ssafy.d205.global.common.TimeProvider;

/**
 * 도메인이 발행한 알림 이벤트를 커밋 뒤에 WebSocket 으로 내보냅니다.
 *
 * <p><b>커밋 뒤</b>여야 합니다. 클라이언트는 알림을 받으면 REST 로 목록을 다시 읽는데, 커밋
 * 전에 보내면 그 조회가 아직 없는 행을 보러 옵니다. 롤백되면 이 메서드는 불리지 않으므로
 * 저장되지 않은 요청에 대한 알림도 나가지 않습니다.
 *
 * <p>이벤트를 발행한 서비스가 트랜잭션 밖이면 리스너가 불리지 않습니다. 친구·초대 서비스는
 * 전부 @Transactional 이라 해당하지 않지만, 새 발행 지점을 만들 때 기억해야 합니다.
 *
 * <p><b>여기서 나는 예외는 여기서 끝냅니다.</b> AFTER_COMMIT 리스너는 커밋한 요청 스레드에서
 * 돌고, 던진 예외는 트랜잭션 매니저를 타고 그 요청까지 올라갑니다. 그러면 저장은 됐는데
 * 보낸 사람이 500 을 받습니다. 알림을 못 보낸 것은 보낸 사람의 잘못이 아니고 받는 쪽 연결
 * 사정이므로, 로그만 남기고 요청은 성공으로 끝내야 합니다.
 */
@Component
@RequiredArgsConstructor
@Slf4j
public class NotificationPublisher {

    private final NotificationSessionRegistry registry;
    private final ObjectMapper objectMapper;
    private final TimeProvider timeProvider;

    @TransactionalEventListener(phase = TransactionPhase.AFTER_COMMIT)
    public void onNotification(UserNotificationEvent event) {
        try {
            String json = objectMapper.writeValueAsString(NotificationMessage.of(event, timeProvider.now()));
            int delivered = registry.deliver(event.targetSeq(), json);

            // 0 은 오류가 아닙니다. 상대가 게임을 켜지 않았을 뿐이고, 다음에 붙을 때 목록 조회로
            // 같은 내용을 봅니다.
            log.debug("{} 알림을 user_seq {} 의 연결 {}개에 보냈습니다.", event.type(), event.targetSeq(), delivered);
        } catch (RuntimeException e) {
            log.warn("{} 알림을 user_seq {} 에 보내지 못했습니다. 요청은 이미 커밋됐고 상대는 다음에 "
                    + "붙을 때 목록 조회로 메꿉니다.", event.type(), event.targetSeq(), e);
        }
    }
}
