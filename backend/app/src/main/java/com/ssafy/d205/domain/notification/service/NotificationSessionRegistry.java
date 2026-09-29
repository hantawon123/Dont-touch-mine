package com.ssafy.d205.domain.notification.service;

import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.context.ApplicationEventPublisher;
import org.springframework.stereotype.Component;
import org.springframework.web.socket.CloseStatus;
import org.springframework.web.socket.PingMessage;
import org.springframework.web.socket.TextMessage;
import org.springframework.web.socket.WebSocketMessage;
import org.springframework.web.socket.WebSocketSession;
import org.springframework.web.socket.handler.ConcurrentWebSocketSessionDecorator;
import org.springframework.web.socket.handler.SessionLimitExceededException;

import java.io.IOException;
import java.util.Map;
import java.util.Set;
import java.util.concurrent.ConcurrentHashMap;

import com.ssafy.d205.domain.notification.event.UserConnectedEvent;
import com.ssafy.d205.domain.notification.event.UserDisconnectedEvent;

/**
 * 누가 어느 연결에 붙어 있는지. 알림을 보낼 때 여기서 상대의 연결을 찾습니다.
 *
 * <p><b>인메모리입니다.</b> 앱이 한 인스턴스라는 전제이고, 둘 이상으로 늘리면 상대가 다른
 * 인스턴스에 붙어 있을 수 있어 Redis pub/sub 같은 공유 통로가 필요합니다. 그때 바뀌는 곳이
 * 이 클래스 하나이도록 발송 코드는 이 안에만 둡니다.
 *
 * <p>한 사람이 여러 연결을 가질 수 있습니다. 탭을 둘 열었거나 기기가 둘인 경우이고, 알림은
 * 전부에게 갑니다. 마지막 연결이 끊길 때 그 사람의 항목이 사라집니다.
 *
 * <p>세션을 {@link ConcurrentWebSocketSessionDecorator} 로 감싸 둡니다. Tomcat 의 세션은
 * 동시에 두 스레드가 보내면 깨지는데, 알림은 커밋한 요청 스레드에서 나가고 ping 은 스케줄러
 * 스레드에서 나가 겹칠 수 있습니다. 감싸면 한 번에 하나만 보내고, 상대가 받지 않아 버퍼가
 * 쌓이면 연결을 끊습니다.
 *
 * <p><b>이 클래스는 DB 를 모릅니다.</b> 연결이 열리고 닫히는 것은 접속 상태의 유일한 근거가
 * 됐지만(S15P21D205-890), 그 사실을 프레즌스에 알리는 일은 이벤트로 넘깁니다. 나중에
 * 인스턴스를 늘려 Redis pub/sub 으로 바꿀 때 손대는 곳을 이 파일 하나로 묶어 두려는
 * 것입니다.
 */
@Component
@RequiredArgsConstructor
@Slf4j
public class NotificationSessionRegistry {

    /** 한 메시지 전송에 허용하는 시간. 넘으면 그 연결은 죽은 것으로 보고 끊습니다. */
    private static final int SEND_TIME_LIMIT_MS = 5_000;

    /** 상대가 받지 않아 쌓일 수 있는 바이트. 알림 하나가 200바이트 안팎이라 넉넉합니다. */
    private static final int BUFFER_SIZE_LIMIT = 64 * 1024;

    private record Binding(Integer userSeq, WebSocketSession session) {
    }

    private final Map<Integer, Set<WebSocketSession>> byUser = new ConcurrentHashMap<>();
    private final Map<String, Binding> bySessionId = new ConcurrentHashMap<>();

    private final ApplicationEventPublisher events;

    /**
     * 연결을 사람에 묶고 {@link UserConnectedEvent} 를 발행합니다.
     *
     * <p>발행은 <b>동기</b>입니다. 프레즌스가 ONLINE 을 쓰는 것이 이 메서드가 돌아오기
     * 전에 끝나므로, 핸들러가 곧이어 보내는 {@code HELLO_ACK} 를 받은 클라이언트가 친구
     * 목록을 바로 조회해도 자기가 온라인으로 보입니다.
     *
     * @return 이후 보내기에 써야 하는, 감싸진 세션. 원본으로 보내면 동시 전송 보호가 빠집니다
     */
    public WebSocketSession bind(Integer userSeq, WebSocketSession raw) {
        WebSocketSession session = new ConcurrentWebSocketSessionDecorator(raw, SEND_TIME_LIMIT_MS, BUFFER_SIZE_LIMIT);
        bySessionId.put(raw.getId(), new Binding(userSeq, session));
        byUser.computeIfAbsent(userSeq, key -> ConcurrentHashMap.newKeySet()).add(session);
        events.publishEvent(new UserConnectedEvent(userSeq));
        return session;
    }

    public boolean isBound(WebSocketSession session) {
        return bySessionId.containsKey(session.getId());
    }

    /** 이 연결이 누구 것인지. 묶이지 않은 연결이면 null 입니다. */
    public Integer userSeqOf(WebSocketSession session) {
        Binding binding = bySessionId.get(session.getId());
        return binding == null ? null : binding.userSeq();
    }

    /**
     * 지금 붙어 있는 사람들. 하트비트를 한 문장으로 밀 때 씁니다.
     *
     * <p>연결이 아니라 <b>사람</b>의 집합입니다. 탭을 셋 열어 둔 사람은 한 번만 나옵니다.
     */
    public Set<Integer> boundUserSeqs() {
        return Set.copyOf(byUser.keySet());
    }

    /**
     * 연결을 지웁니다. 묶이지 않은 연결이면 아무 일도 없습니다.
     *
     * <p>그 사람의 마지막 연결이었으면 {@link UserDisconnectedEvent} 를 발행합니다.
     * computeIfPresent 가 null 을 돌려주는 것이 그 신호입니다 — 남은 세션이 없어
     * 항목째로 사라졌다는 뜻입니다.
     */
    public void unbind(WebSocketSession session) {
        Binding binding = bySessionId.remove(session.getId());
        if (binding == null) {
            return;
        }
        Set<WebSocketSession> remaining = byUser.computeIfPresent(binding.userSeq(), (key, sessions) -> {
            sessions.remove(binding.session());
            return sessions.isEmpty() ? null : sessions;
        });
        if (remaining == null) {
            events.publishEvent(new UserDisconnectedEvent(binding.userSeq()));
        }
    }

    /**
     * 한 사람의 모든 연결에 보냅니다.
     *
     * @return 실제로 보낸 연결 수. 0 이면 그 사람은 지금 붙어 있지 않고, 알림은 버려집니다.
     *         놓친 알림은 클라이언트가 다시 붙을 때 목록을 전체 조회해 메꿉니다
     */
    public int deliver(Integer userSeq, String json) {
        Set<WebSocketSession> sessions = byUser.get(userSeq);
        if (sessions == null) {
            return 0;
        }
        int sent = 0;
        for (WebSocketSession session : sessions) {
            if (trySend(session, new TextMessage(json))) {
                sent++;
            }
        }
        return sent;
    }

    /**
     * 모든 연결에 ping. 노트북을 덮거나 Wi-Fi 가 바뀌면 TCP 는 아무 말 없이 죽는데, 그 연결에
     * 알림을 계속 쓰면 실패가 조용히 쌓입니다. 주기적으로 찔러서 죽은 것을 걷어냅니다.
     */
    public void pingAll() {
        for (Binding binding : bySessionId.values()) {
            trySend(binding.session(), new PingMessage());
        }
    }

    public int connectionCount() {
        return bySessionId.size();
    }

    private boolean trySend(WebSocketSession session, WebSocketMessage<?> message) {
        try {
            session.sendMessage(message);
            return true;
        } catch (IOException | IllegalStateException | SessionLimitExceededException e) {
            // 데코레이터는 시간·버퍼 한도를 넘기면 스스로 세션을 닫고 SessionLimitExceededException
            // 을 냅니다. 이것은 IllegalStateException 이 아니라 RuntimeException 을 바로 상속하므로
            // 따로 적어야 합니다. 어느 쪽이든 이 연결은 끝났습니다.
            log.debug("알림 전송에 실패해 연결 {} 을 정리합니다: {}", session.getId(), e.getMessage());
            unbind(session);
            closeQuietly(session);
            return false;
        }
    }

    private static void closeQuietly(WebSocketSession session) {
        try {
            if (session.isOpen()) {
                session.close(CloseStatus.SESSION_NOT_RELIABLE);
            }
        } catch (IOException ignored) {
            // 이미 끊긴 연결을 닫는 것이라 실패해도 할 일이 없습니다.
        }
    }
}
