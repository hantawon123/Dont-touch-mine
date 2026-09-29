package com.ssafy.d205.global.config;

import lombok.RequiredArgsConstructor;
import org.springframework.boot.context.properties.EnableConfigurationProperties;
import org.springframework.context.annotation.Configuration;
import org.springframework.web.socket.config.annotation.EnableWebSocket;
import org.springframework.web.socket.config.annotation.WebSocketConfigurer;
import org.springframework.web.socket.config.annotation.WebSocketHandlerRegistry;

import com.ssafy.d205.domain.notification.controller.NotificationWebSocketHandler;

/**
 * 실시간 알림 채널을 엽니다. 경로는 {@code /ws/notifications} 하나입니다.
 *
 * <p>STOMP 를 쓰지 않습니다. 필요한 것은 서버가 한 사람에게 JSON 한 줄을 미는 것뿐이고,
 * STOMP 는 그 위에 구독·목적지 프로토콜을 얹어 Unity 쪽에 파서를 하나 더 요구합니다.
 *
 * <p>보안 체인은 SecurityConfig 의 openChain 이 이 경로도 permitAll 로 덮습니다. 핸드셰이크는
 * 평범한 GET 이라 그 체인을 지나고, 이후 프레임은 Security 를 거치지 않습니다. 사람 식별은
 * 핸들러가 첫 프레임으로 합니다.
 *
 * <p>nginx 는 이 경로에 Upgrade 헤더를 넘기는 별도 location 이 필요합니다.
 * backend/deploy/nginx/d205.conf 의 {@code /ws/} 블록입니다.
 */
@Configuration
@EnableWebSocket
@EnableConfigurationProperties(NotificationProperties.class)
@RequiredArgsConstructor
public class WebSocketConfig implements WebSocketConfigurer {

    public static final String NOTIFICATIONS_PATH = "/ws/notifications";

    private final NotificationWebSocketHandler notificationHandler;
    private final NotificationProperties properties;

    @Override
    public void registerWebSocketHandlers(WebSocketHandlerRegistry registry) {
        registry.addHandler(notificationHandler, NOTIFICATIONS_PATH)
                .setAllowedOriginPatterns(properties.allowedOrigins().toArray(String[]::new));
    }
}
