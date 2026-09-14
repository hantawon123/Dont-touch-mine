package com.ssafy.d205.domain.notification.controller;

import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.scheduling.TaskScheduler;
import org.springframework.stereotype.Component;
import org.springframework.web.socket.CloseStatus;
import org.springframework.web.socket.TextMessage;
import org.springframework.web.socket.WebSocketSession;
import org.springframework.web.socket.handler.TextWebSocketHandler;
import tools.jackson.core.JacksonException;
import tools.jackson.databind.JsonNode;
import tools.jackson.databind.ObjectMapper;

import java.io.IOException;
import java.time.Instant;
import java.util.Map;
import java.util.Optional;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.ScheduledFuture;

import com.ssafy.d205.domain.notification.service.NotificationSessionRegistry;
import com.ssafy.d205.domain.presence.dto.UpdatePresenceRequest;
import com.ssafy.d205.domain.presence.entity.SessionKind;
import com.ssafy.d205.domain.presence.service.PresenceService;
import com.ssafy.d205.domain.user.entity.User;
import com.ssafy.d205.domain.user.repository.UserRepository;
import com.ssafy.d205.global.config.NotificationProperties;
import com.ssafy.d205.global.security.AccountTokens;

/**
 * {@code /ws/notifications} 로 들어오는 연결의 입구.
 *
 * <p>연결이 열리면 클라이언트가 먼저 말해야 합니다.
 * <pre>{ "type": "HELLO", "userId": "...", "token": "..." }</pre>
 * 브라우저의 WebSocket 은 헤더를 붙일 수 없어 X-User-Id 를 쓸 수 없고, 쿼리스트링에 넣으면
 * nginx 접근 로그에 남습니다. 그래서 첫 프레임으로 받습니다. {@code token} 은 REST 의
 * X-Account-Token 과 같은 값이고 같은 규칙으로 봅니다({@link AccountTokens}). 없으면 REST 와
 * 똑같은 구멍이 됩니다 - 남의 id 를 아는 사람이 그 사람의 알림을 받습니다.
 *
 * <p>정해진 시간 안에 HELLO 가 없으면 끊습니다. 누구 것도 아닌 연결이 쌓이지 않게 하려는
 * 것이고, 정상 클라이언트는 연결 직후 바로 보내므로 걸리지 않습니다.
 *
 * <p>HELLO 가 받아들여지면 {@code { "type": "HELLO_ACK" }} 를 돌려줍니다. 클라이언트는 이것을
 * 받은 시점을 "연결됨"으로 보고 놓친 것이 없도록 목록을 전체 조회합니다. 연결이 열린 시점을
 * 쓰면 서버가 아직 나를 모르는 사이에 온 알림을 놓칩니다.
 *
 * <p>묶인 뒤에는 방을 오갈 때 {@code PRESENCE} 프레임을 보냅니다.
 * <pre>{ "type": "PRESENCE", "sessionId": "...", "sessionKind": "LOBBY" }</pre>
 * 접속했다는 사실은 이 연결이 살아 있는 것으로 이미 알기 때문에, 클라이언트가 주기적으로
 * 보내야 하는 것은 아무것도 없습니다. 프레임은 <b>상태가 바뀌는 순간에만</b> 나갑니다.
 */
@Component
@RequiredArgsConstructor
@Slf4j
public class NotificationWebSocketHandler extends TextWebSocketHandler {

    private static final String HELLO = "HELLO";
    private static final String HELLO_ACK = "{\"type\":\"HELLO_ACK\"}";
    private static final String PRESENCE = "PRESENCE";

    private final NotificationSessionRegistry registry;
    private final UserRepository userRepository;
    private final PresenceService presenceService;
    private final ObjectMapper objectMapper;
    private final TaskScheduler scheduler;
    private final NotificationProperties properties;
    private final AccountTokens tokens;

    /** 아직 HELLO 를 보내지 않은 연결마다 걸어 둔 마감. HELLO 가 오면 취소합니다. */
    private final Map<String, ScheduledFuture<?>> helloDeadlines = new ConcurrentHashMap<>();

    @Override
    public void afterConnectionEstablished(WebSocketSession session) {
        ScheduledFuture<?> deadline = scheduler.schedule(
                () -> closeIfStillSilent(session),
                Instant.now().plusMillis(properties.helloTimeoutMs()));
        helloDeadlines.put(session.getId(), deadline);
    }

    @Override
    protected void handleTextMessage(WebSocketSession session, TextMessage message) throws IOException {
        JsonNode frame;
        try {
            frame = objectMapper.readTree(message.getPayload());
        } catch (JacksonException e) {
            log.debug("연결 {} 이 JSON 이 아닌 프레임을 보냈습니다. 무시합니다.", session.getId());
            return;
        }

        // 모르는 type 은 조용히 버립니다. 끊어버리면 새 프레임을 추가한 클라이언트가 옛
        // 서버에 붙었을 때 계속 튕깁니다.
        String type = frame.path("type").asString();
        if (PRESENCE.equals(type)) {
            reportPresence(session, frame);
            return;
        }
        if (!HELLO.equals(type)) {
            return;
        }
        if (registry.isBound(session)) {
            return;
        }

        String userId = frame.path("userId").asString();

        // AccountTokenInterceptor 와 같은 검사입니다. 이 채널은 인터셉터를 지나지 않으므로
        // 여기서 직접 봅니다. 비밀이 없는 서버는 REST 와 마찬가지로 검사하지 않습니다.
        // DB 를 보기 전에 끝내는 것도 같습니다 - 토큰 없이 남의 id 로 "그 계정이 있는가"를
        // 알아내는 길을 열지 않습니다.
        if (tokens.isEnabled() && !tokens.matches(userId, frame.path("token").asString())) {
            close(session, CloseStatus.POLICY_VIOLATION.withReason("UNAUTHORIZED"));
            return;
        }

        Optional<User> user = userId.isBlank() ? Optional.empty() : userRepository.findByPublicId(userId);
        if (user.isEmpty()) {
            close(session, CloseStatus.POLICY_VIOLATION.withReason("UNKNOWN_USER"));
            return;
        }

        // 정지된 계정은 여기서도 막습니다.
        //
        // SuspensionInterceptor 가 못 잡는 유일한 경로입니다. 그쪽은 X-User-Id 헤더를
        // 보는데, 브라우저의 WebSocket 은 헤더를 붙일 수 없어 이 채널은 HELLO 프레임에
        // userId 를 담습니다(위 주석). 헤더가 없으니 인터셉터는 통과시킵니다.
        //
        // 막지 않으면 정지된 사람이 API 는 전부 403 을 받으면서 친구 요청과 초대 알림만
        // 실시간으로 계속 받습니다.
        if (user.get().isSuspended()) {
            close(session, CloseStatus.POLICY_VIOLATION.withReason("SUSPENDED"));
            return;
        }

        cancelDeadline(session);
        WebSocketSession bound = registry.bind(user.get().getSeq(), session);
        bound.sendMessage(new TextMessage(HELLO_ACK));
    }

    @Override
    public void afterConnectionClosed(WebSocketSession session, CloseStatus status) {
        cancelDeadline(session);
        registry.unbind(session);
    }

    @Override
    public void handleTransportError(WebSocketSession session, Throwable exception) {
        log.debug("알림 연결 {} 에 전송 오류: {}", session.getId(), exception.getMessage());
        close(session, CloseStatus.SERVER_ERROR);
    }

    /**
     * 방을 오가는 것을 접속 상태에 반영합니다.
     *
     * <p><b>잘못된 프레임은 조용히 버립니다.</b> 푸시 채널에는 400 으로 되돌릴 자리가
     * 없고, 끊어버리면 오타 하나로 실시간 알림 전체를 잃습니다. REST 쪽은 같은 입력을
     * 400 으로 되돌립니다 — 그쪽은 응답을 기다리는 호출자가 있으니까요.
     *
     * <p>기본값과 길이 한도는 {@link UpdatePresenceRequest} 에서 가져옵니다. REST 본문과
     * 이 프레임이 같은 규칙을 써야 하고, 두 곳에 적으면 한쪽만 고쳐지는 날이 옵니다.
     */
    private void reportPresence(WebSocketSession session, JsonNode frame) {
        Integer userSeq = registry.userSeqOf(session);
        if (userSeq == null) {
            // HELLO 전에는 누구의 프레임인지 알 수 없습니다.
            log.debug("묶이지 않은 연결 {} 의 PRESENCE 프레임을 버립니다.", session.getId());
            return;
        }

        // 룸 밖이면 sessionId 를 빼고 보냅니다. 빈 값도 같은 뜻으로 읽습니다.
        String raw = frame.path("sessionId").asString();
        String sessionId = raw.isBlank() ? null : raw;
        if (sessionId != null && sessionId.length() > UpdatePresenceRequest.MAX_SESSION_ID_LENGTH) {
            log.debug("연결 {} 이 너무 긴 sessionId 를 보냈습니다. 버립니다.", session.getId());
            return;
        }

        String kind = frame.path("sessionKind").asString();
        SessionKind sessionKind;
        try {
            sessionKind = kind.isBlank() ? null : SessionKind.valueOf(kind);
        } catch (IllegalArgumentException e) {
            log.debug("연결 {} 이 모르는 sessionKind '{}' 를 보냈습니다. 버립니다.", session.getId(), kind);
            return;
        }

        // REST 본문과 같은 값을 같은 규칙으로 읽습니다. 없는 sessionKind 를 MATCH 로
        // 메꾸는 것이 그 규칙이고, 기본값은 이 record 안에만 있습니다.
        UpdatePresenceRequest reported = new UpdatePresenceRequest(sessionId, sessionKind);
        try {
            presenceService.reportBound(userSeq, reported.sessionId(), reported.sessionKindOrMatch());
        } catch (RuntimeException e) {
            // 여기서 던지면 컨테이너가 이 연결을 SERVER_ERROR 로 닫습니다. 상태를 한 번
            // 쓰지 못한 것 때문에 실시간 알림 전체를 잃을 이유가 없습니다. 상태는 다음
            // 프레임이나 재연결 뒤의 보고가 메꿉니다.
            log.warn("user_seq {} 의 PRESENCE 프레임을 반영하지 못했습니다. 연결은 유지합니다.",
                    userSeq, e);
        }
    }

    private void closeIfStillSilent(WebSocketSession session) {
        helloDeadlines.remove(session.getId());
        if (!registry.isBound(session)) {
            close(session, CloseStatus.POLICY_VIOLATION.withReason("HELLO_TIMEOUT"));
        }
    }

    private void cancelDeadline(WebSocketSession session) {
        ScheduledFuture<?> deadline = helloDeadlines.remove(session.getId());
        if (deadline != null) {
            deadline.cancel(false);
        }
    }

    private void close(WebSocketSession session, CloseStatus status) {
        try {
            if (session.isOpen()) {
                session.close(status);
            }
        } catch (IOException e) {
            log.debug("알림 연결 {} 을 닫지 못했습니다: {}", session.getId(), e.getMessage());
        }
    }
}
