package com.ssafy.d205.domain.notification.service;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.web.socket.CloseStatus;
import org.springframework.web.socket.handler.SessionLimitExceededException;
import tools.jackson.databind.ObjectMapper;

import java.time.Clock;

import static org.assertj.core.api.Assertions.assertThatNoException;

import com.ssafy.d205.domain.notification.event.NotificationType;
import com.ssafy.d205.domain.notification.event.UserNotificationEvent;
import com.ssafy.d205.global.common.TimeProvider;

/**
 * 발송이 실패했을 때 리스너가 무엇을 하는가.
 *
 * <p>스프링 컨텍스트 없이 확인합니다. 실제로 이 실패를 일으키려면 붙어 있는 클라이언트가
 * 읽기를 멈춘 채 세션에 64KB 가 쌓여야 하는데, 통합 테스트로 그 상태를 안정적으로 만들
 * 수 없습니다.
 */
class NotificationPublisherTest {

    @Test
    @DisplayName("발송이 실패해도 예외가 리스너를 벗어나지 않는다")
    void aFailedSendDoesNotEscapeTheListener() {
        // 이것이 이 클래스가 있는 이유다. 이 리스너는 AFTER_COMMIT 이라 커밋한 요청 스레드에서
        // 돌고, 던진 예외는 트랜잭션 매니저를 타고 그 요청까지 올라간다. 막지 않으면 친구
        // 요청은 저장됐는데 보낸 사람이 500 을 받는다. 받는 쪽 연결이 죽은 것은 보낸 사람의
        // 잘못이 아니다.
        NotificationPublisher publisher = new NotificationPublisher(
                deliveryThatFails(), new ObjectMapper(), new TimeProvider(Clock.systemUTC()));

        assertThatNoException().isThrownBy(() -> publisher.onNotification(
                new UserNotificationEvent(1, NotificationType.FRIEND_REQUEST_RECEIVED, "u", "n", null)));
    }

    /**
     * 한도를 넘긴 세션에 쓰려 할 때 ConcurrentWebSocketSessionDecorator 가 내는 예외를
     * 그대로 냅니다. 이것은 IllegalStateException 이 아니라 RuntimeException 을 바로
     * 상속합니다.
     */
    private static NotificationSessionRegistry deliveryThatFails() {
        // 이벤트 발행은 붙고 끊길 때만 쓰이고 여기서는 발송만 봅니다. 아무것도 하지 않는
        // 발행자를 넘겨 스프링 컨텍스트 없이 둡니다.
        return new NotificationSessionRegistry(event -> { }) {
            @Override
            public int deliver(Integer userSeq, String json) {
                throw new SessionLimitExceededException("Buffer size limit reached", CloseStatus.NO_STATUS_CODE);
            }
        };
    }
}
