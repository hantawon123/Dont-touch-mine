package com.ssafy.d205.domain.notification.service;

import lombok.RequiredArgsConstructor;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Component;

/**
 * 살아 있는 알림 연결에 주기적으로 ping 을 보냅니다.
 *
 * <p>서버가 주도하는 이유는 브라우저 탭이 뒤로 가면 클라이언트 쪽 타이머가 크게 느려지기
 * 때문입니다. 클라이언트에 맡기면 멀쩡한 연결을 서버가 죽었다고 오해합니다. ping 을 받은
 * 브라우저와 .NET 은 pong 을 자동으로 돌려주므로 클라이언트 코드는 필요 없습니다.
 *
 * <p>주기는 nginx 의 proxy_read_timeout(90초)보다 짧아야 합니다. 그렇지 않으면 조용한 연결을
 * nginx 가 먼저 끊고, 클라이언트는 이유 없이 재연결을 반복합니다.
 */
@Component
@RequiredArgsConstructor
public class NotificationKeepAlive {

    private final NotificationSessionRegistry registry;

    @Scheduled(fixedDelayString = "${notifications.ping-interval-ms:30000}")
    public void ping() {
        registry.pingAll();
    }
}
