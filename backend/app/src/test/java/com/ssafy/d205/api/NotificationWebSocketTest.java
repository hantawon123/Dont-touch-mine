package com.ssafy.d205.api;

import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.http.MediaType;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.web.socket.CloseStatus;
import org.springframework.web.socket.TextMessage;
import org.springframework.web.socket.WebSocketSession;
import org.springframework.web.socket.client.standard.StandardWebSocketClient;
import org.springframework.web.socket.handler.TextWebSocketHandler;
import tools.jackson.databind.JsonNode;
import tools.jackson.databind.ObjectMapper;

import java.io.IOException;
import java.time.Duration;
import java.time.Instant;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.BlockingQueue;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.LinkedBlockingQueue;
import java.util.concurrent.TimeUnit;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.ssafy.d205.domain.notification.service.NotificationSessionRegistry;
import com.ssafy.d205.domain.presence.service.PresenceHeartbeat;
import com.ssafy.d205.global.common.Timestamps;
import com.ssafy.d205.support.IntegrationTest;

/**
 * 실시간 알림 채널을 실제 소켓으로 시험합니다.
 *
 * <p>다른 API 테스트와 달리 서버를 진짜 포트에 띄웁니다. MockMvc 는 서블릿 호출을 흉내낼 뿐
 * WebSocket 핸드셰이크와 프레임은 흉내내지 못합니다. 친구 요청·초대 같은 <b>일으키는 쪽</b>은
 * 여전히 MockMvc 로 부르고, <b>받는 쪽</b>만 소켓으로 붙습니다. 두 경로가 같은 컨텍스트라
 * 커밋 뒤 발송이 실제 소켓으로 나갑니다.
 */
@SpringBootTest(webEnvironment = SpringBootTest.WebEnvironment.RANDOM_PORT)
class NotificationWebSocketTest extends IntegrationTest {

    private static final String USER_ID_HEADER = "X-User-Id";
    private static final String ROOM = "7K2M9P";
    private static final Duration WAIT = Duration.ofSeconds(5);

    // Boot 4 에서 @LocalServerPort 가 든 모듈이 webmvc-test 스타터에 딸려오지 않아 속성으로 읽습니다.
    @Value("${local.server.port}")
    int port;

    @Autowired
    MockMvc mvc;

    @Autowired
    ObjectMapper objectMapper;

    @Autowired
    NotificationSessionRegistry registry;

    @Autowired
    JdbcTemplate jdbcTemplate;

    @Autowired
    PresenceHeartbeat presenceHeartbeat;

    private final List<Client> clients = new ArrayList<>();

    @AfterEach
    void closeClients() {
        for (Client client : clients) {
            client.closeQuietly();
        }
        clients.clear();
    }

    @Test
    @DisplayName("HELLO 를 보내면 HELLO_ACK 가 온다")
    void helloIsAcknowledged() throws Exception {
        String me = createUser();

        Client client = connect();
        client.hello(me);

        // 클라이언트는 이 프레임을 받은 시점을 "연결됨"으로 봅니다. 연결이 열린 시점을 쓰면
        // 서버가 아직 나를 모르는 사이의 알림을 놓칩니다.
        assertThat(client.next().path("type").asText()).isEqualTo("HELLO_ACK");
    }

    @Test
    @DisplayName("친구 요청을 받으면 상대에게 즉시 프레임이 간다")
    void friendRequestIsPushedToTheTarget() throws Exception {
        String host = createUser();
        String guest = createUser();
        Client guestClient = connectAs(guest);

        sendRequest(host, guest);

        JsonNode frame = guestClient.next();
        assertThat(frame.path("type").asText()).isEqualTo("FRIEND_REQUEST_RECEIVED");
        assertThat(frame.path("from").path("userId").asText()).isEqualTo(host);
        assertThat(frame.path("from").path("nickname").asText()).isNotBlank();
        assertThat(frame.path("sentAt").asText()).matches("\\d{14}");
        assertThat(frame.path("roomCode").isNull()).isTrue();
    }

    @Test
    @DisplayName("요청을 수락하면 요청자에게 프레임이 간다")
    void acceptingIsPushedToTheRequester() throws Exception {
        String host = createUser();
        String guest = createUser();
        Client hostClient = connectAs(host);
        sendRequest(host, guest);

        accept(guest, host);

        JsonNode frame = hostClient.next();
        assertThat(frame.path("type").asText()).isEqualTo("FRIEND_REQUEST_ACCEPTED");
        assertThat(frame.path("from").path("userId").asText()).isEqualTo(guest);
    }

    @Test
    @DisplayName("서로 요청을 보내 자동 수락되면 먼저 보낸 쪽이 수락 프레임을 받는다")
    void mutualRequestsNotifyTheFirstRequester() throws Exception {
        String host = createUser();
        String guest = createUser();
        Client hostClient = connectAs(host);
        sendRequest(host, guest);

        // 상대도 나에게 요청하면 새 요청이 아니라 내 요청의 수락입니다. 그래서 201 이 아니라
        // 200 이고, 요청자인 나에게 "받았다"가 아니라 "수락됐다"가 와야 합니다.
        mvc.perform(post("/api/v1/friend-requests")
                        .header(USER_ID_HEADER, guest)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"userId\":\"" + host + "\"}"))
                .andExpect(status().isOk());

        JsonNode frame = hostClient.next();
        assertThat(frame.path("type").asText()).isEqualTo("FRIEND_REQUEST_ACCEPTED");
        assertThat(frame.path("from").path("userId").asText()).isEqualTo(guest);
    }

    @Test
    @DisplayName("요청이 거절되면 요청자에게 지워졌다는 프레임이 간다")
    void decliningIsPushedAsRemoved() throws Exception {
        String host = createUser();
        String guest = createUser();
        Client hostClient = connectAs(host);
        sendRequest(host, guest);

        mvc.perform(delete("/api/v1/friend-requests/{userId}", host).header(USER_ID_HEADER, guest))
                .andExpect(status().isNoContent());

        JsonNode frame = hostClient.next();
        assertThat(frame.path("type").asText()).isEqualTo("FRIEND_REQUEST_REMOVED");
        assertThat(frame.path("from").path("userId").asText()).isEqualTo(guest);
    }

    @Test
    @DisplayName("방으로 부르면 상대에게 방 코드가 실린 프레임이 간다")
    void inviteIsPushedWithTheRoomCode() throws Exception {
        String host = createUser();
        String guest = createUser();
        befriend(host, guest);
        Client guestClient = connectAs(guest);

        invite(host, guest, ROOM);

        JsonNode frame = guestClient.next();
        assertThat(frame.path("type").asText()).isEqualTo("ROOM_INVITE_RECEIVED");
        assertThat(frame.path("from").path("userId").asText()).isEqualTo(host);
        assertThat(frame.path("roomCode").asText()).isEqualTo(ROOM);
    }

    @Test
    @DisplayName("같은 방으로 다시 부르면 프레임이 한 번 더 간다")
    void invitingAgainPushesAgain() throws Exception {
        String host = createUser();
        String guest = createUser();
        befriend(host, guest);
        Client guestClient = connectAs(guest);
        invite(host, guest, ROOM);
        guestClient.next();

        // 갱신은 저장 행을 늘리지 않지만 알림은 다시 갑니다. 상대 화면에서 토스트가 이미
        // 사라졌을 수 있고, 다시 부른 것은 다시 봐 달라는 뜻입니다.
        invite(host, guest, ROOM);

        assertThat(guestClient.next().path("type").asText()).isEqualTo("ROOM_INVITE_RECEIVED");
    }

    @Test
    @DisplayName("친구를 끊으면 상대에게 끊겼다는 프레임이 간다")
    void unfriendingPushesFriendRemoved() throws Exception {
        String host = createUser();
        String guest = createUser();
        befriend(host, guest);
        invite(host, guest, ROOM);
        Client guestClient = connectAs(guest);

        mvc.perform(delete("/api/v1/friends/{userId}", guest).header(USER_ID_HEADER, host))
                .andExpect(status().isNoContent());

        JsonNode frame = guestClient.next();
        assertThat(frame.path("type").asText()).isEqualTo("FRIEND_REMOVED");
        assertThat(frame.path("from").path("userId").asText()).isEqualTo(host);
    }

    @Test
    @DisplayName("한 사람이 여러 연결을 열어 두면 전부 받는다")
    void everyConnectionOfTheSameUserReceives() throws Exception {
        String host = createUser();
        String guest = createUser();
        Client first = connectAs(guest);
        Client second = connectAs(guest);

        sendRequest(host, guest);

        assertThat(first.next().path("type").asText()).isEqualTo("FRIEND_REQUEST_RECEIVED");
        assertThat(second.next().path("type").asText()).isEqualTo("FRIEND_REQUEST_RECEIVED");
    }

    @Test
    @DisplayName("요청이 실패하면 아무것도 가지 않는다")
    void nothingIsPushedWhenTheRequestFails() throws Exception {
        String host = createUser();
        String guest = createUser();
        befriend(host, guest);
        Client guestClient = connectAs(guest);

        // 이미 친구라 409 입니다. 저장이 없었으니 알림도 없어야 합니다.
        mvc.perform(post("/api/v1/friend-requests")
                        .header(USER_ID_HEADER, host)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"userId\":\"" + guest + "\"}"))
                .andExpect(status().isConflict());

        assertThat(guestClient.nothingWithin(Duration.ofSeconds(1))).isTrue();
    }

    @Test
    @DisplayName("HELLO 없이 가만히 있는 연결은 서버가 끊는다")
    void aSilentConnectionIsClosed() throws Exception {
        Client client = connect();

        // 테스트 프로필의 마감은 1초입니다. 누구 것도 아닌 연결이 쌓이지 않게 합니다.
        assertThat(client.closed.await(WAIT.toMillis(), TimeUnit.MILLISECONDS)).isTrue();
        assertThat(client.closeStatus.getCode()).isEqualTo(CloseStatus.POLICY_VIOLATION.getCode());
    }

    @Test
    @DisplayName("모르는 사용자로 HELLO 하면 끊는다")
    void anUnknownUserIsClosed() throws Exception {
        Client client = connect();

        client.hello(UUID.randomUUID().toString());

        assertThat(client.closed.await(WAIT.toMillis(), TimeUnit.MILLISECONDS)).isTrue();
        assertThat(client.closeStatus.getCode()).isEqualTo(CloseStatus.POLICY_VIOLATION.getCode());
    }

    @Test
    @DisplayName("정지된 계정으로 HELLO 하면 끊는다")
    void aSuspendedUserIsClosed() throws Exception {
        // SuspensionInterceptor 가 못 잡는 유일한 경로입니다. 그쪽은 X-User-Id 헤더를
        // 보는데 이 채널은 헤더를 못 쓰고 HELLO 프레임에 userId 를 담습니다. 막지 않으면
        // 정지된 사람이 API 는 전부 403 을 받으면서 알림만 실시간으로 계속 받습니다.
        String userId = createUser();
        // SQL 로 직접 바꿉니다. 엔티티를 트랜잭션 밖에서 고치면 저장되지 않고, 정지
        // API 를 부르려면 운영자 로그인이 필요한데 이 테스트의 관심사가 아닙니다.
        jdbcTemplate.update(
                "UPDATE users SET suspended_at = ?, suspended_reason = ? WHERE public_id = ?",
                "20260911000000", "정지", userId);

        Client client = connect();
        client.hello(userId);

        assertThat(client.closed.await(WAIT.toMillis(), TimeUnit.MILLISECONDS)).isTrue();
        assertThat(client.closeStatus.getCode()).isEqualTo(CloseStatus.POLICY_VIOLATION.getCode());
    }

    @Test
    @DisplayName("연결을 닫으면 레지스트리에서 빠진다")
    void closingRemovesTheBinding() throws Exception {
        String me = createUser();
        int before = registry.connectionCount();
        Client client = connectAs(me);
        assertThat(registry.connectionCount()).isEqualTo(before + 1);

        client.session.close();

        // 닫힘은 서버 쪽 스레드에서 처리되므로 잠깐 기다립니다.
        long deadline = System.currentTimeMillis() + WAIT.toMillis();
        while (registry.connectionCount() != before && System.currentTimeMillis() < deadline) {
            Thread.sleep(20);
        }
        assertThat(registry.connectionCount()).isEqualTo(before);
    }

    @Test
    @DisplayName("붙으면 접속 상태가 ONLINE 이 된다")
    void connectingMakesOnline() throws Exception {
        String me = createUser();

        connectAs(me);

        // 연결이 살아 있다는 것 자체가 온라인 신호입니다. 클라이언트가 따로 알려 주는
        // 것은 아무것도 없습니다.
        assertThat(presenceOf(me).get("status")).isEqualTo("ONLINE");
    }

    @Test
    @DisplayName("주기 하트비트가 없어도 친구 목록에서 ONLINE 으로 남는다")
    void statusSurvivesWithoutAClientHeartbeat() throws Exception {
        String me = createUser();
        String friend = createUser();
        befriend(friend, me);
        connectAs(me);

        // 클라이언트가 90초 넘게 아무것도 보내지 않은 상황을 만듭니다. 예전에는 이것이
        // 곧 크래시였고, 조회가 이 사람을 오프라인으로 판정했습니다. 이제 붙어 있는
        // 사람의 하트비트는 서버가 밉니다.
        makeHeartbeatStale(me);
        presenceHeartbeat.refresh();

        mvc.perform(get("/api/v1/friends").header(USER_ID_HEADER, friend))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.friends[0].userId").value(me))
                .andExpect(jsonPath("$.friends[0].presence").value("ONLINE"));
    }

    @Test
    @DisplayName("붙어 있지 않으면 갱신이 되지 않아 만료가 오프라인으로 데려간다")
    void aUserWhoIsNotConnectedExpires() throws Exception {
        String me = createUser();
        String friend = createUser();
        befriend(friend, me);

        // 소켓 없이 REST 로만 보고한 사람입니다. 갱신 대상이 아니므로 낡은 채로 남습니다.
        // 서버가 죽었다 살아난 뒤 남은 행이 정리되는 것이 이 경로입니다.
        heartbeat(me, null);
        makeHeartbeatStale(me);
        presenceHeartbeat.refresh();

        mvc.perform(get("/api/v1/friends").header(USER_ID_HEADER, friend))
                .andExpect(jsonPath("$.friends[0].presence").value("OFFLINE"));
    }

    @Test
    @DisplayName("연결을 닫으면 만료를 기다리지 않고 OFFLINE 이 된다")
    void closingGoesOfflineWithoutWaitingForTheTimeout() throws Exception {
        String me = createUser();
        Client client = connectAs(me);

        client.session.close();

        awaitPresence(me, "OFFLINE");
    }

    @Test
    @DisplayName("연결이 둘이면 하나가 닫혀도 ONLINE 이고, 마지막이 닫히면 OFFLINE 이다")
    void onlyTheLastConnectionClosingGoesOffline() throws Exception {
        String me = createUser();
        Client first = connectAs(me);
        Client second = connectAs(me);
        int both = registry.connectionCount();

        first.session.close();

        // 탭을 둘 열어 둔 사람이 하나를 닫은 것은 접속이 끊긴 것이 아닙니다. 여기서
        // 내려버리면 남은 탭에서 자기가 오프라인으로 보입니다.
        awaitConnectionCount(both - 1);
        assertThat(presenceOf(me).get("status")).isEqualTo("ONLINE");

        second.session.close();

        awaitPresence(me, "OFFLINE");
    }

    @Test
    @DisplayName("PRESENCE 프레임으로 로비와 경기 중을 알린다")
    void presenceFrameReportsLobbyAndMatch() throws Exception {
        String me = createUser();
        Client client = connectAs(me);

        client.presence(ROOM, "LOBBY");
        awaitPresence(me, "IN_LOBBY");

        // 로비에서 경기로 넘어가도 룸은 그대로입니다. sessionId 가 같고 상태만 바뀝니다.
        client.presence(ROOM, "MATCH");
        awaitPresence(me, "IN_GAME");
        assertThat(presenceOf(me).get("session_id")).isEqualTo(ROOM);

        // 룸을 나오면 sessionId 를 빼고 보냅니다.
        client.presence(null, null);
        awaitPresence(me, "ONLINE");
        assertThat(presenceOf(me).get("session_id")).isNull();
    }

    @Test
    @DisplayName("PRESENCE 프레임으로 로비에 들어간 친구는 초대가 막힌다")
    void invitingAFriendWhoReportedLobbyIsBlocked() throws Exception {
        String host = createUser();
        String guest = createUser();
        befriend(host, guest);
        Client guestClient = connectAs(guest);

        guestClient.presence(ROOM, "LOBBY");
        awaitPresence(guest, "IN_LOBBY");

        // 885 의 차단이 프레임으로 들어온 상태에도 그대로 걸립니다.
        mvc.perform(post("/api/v1/invites")
                        .header(USER_ID_HEADER, host)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"userId\":\"" + guest + "\",\"roomCode\":\"" + ROOM + "\"}"))
                .andExpect(status().isConflict());
    }

    @Test
    @DisplayName("두 번째 연결이 경기 중인 사람을 방에서 끌어내지 않는다")
    void aSecondConnectionDoesNotPullTheUserOutOfTheMatch() throws Exception {
        String me = createUser();
        Client first = connectAs(me);
        first.presence(ROOM, "MATCH");
        awaitPresence(me, "IN_GAME");

        connectAs(me);

        // 기기를 하나 더 켠 것이거나 경기 중 재접속입니다. 붙었다는 사실만으로 ONLINE 으로
        // 되돌리면 친구 목록에서 경기 중인 사람을 부를 수 있는 것처럼 보입니다.
        assertThat(presenceOf(me).get("status")).isEqualTo("IN_GAME");
        assertThat(presenceOf(me).get("session_id")).isEqualTo(ROOM);
    }

    @Test
    @DisplayName("HELLO 없이 보낸 PRESENCE 프레임은 무시한다")
    void aPresenceFrameWithoutHelloIsIgnored() throws Exception {
        String me = createUser();
        Client client = connect();

        client.presence(ROOM, "MATCH");

        // 누구의 프레임인지 알 수 없습니다. 끊지 않는 이유는 HELLO 마감이 이미 그 일을
        // 하기 때문이고, 그 마감에 걸려 닫히는 것으로 확인합니다.
        assertThat(client.closed.await(WAIT.toMillis(), TimeUnit.MILLISECONDS)).isTrue();
        assertThat(presenceRowMissing(me)).isTrue();
    }

    @Test
    @DisplayName("모르는 sessionKind 프레임은 버리고 연결은 살려 둔다")
    void anUnknownSessionKindFrameIsDropped() throws Exception {
        String me = createUser();
        Client client = connectAs(me);

        client.presence(ROOM, "SOMETHING_ELSE");

        // 푸시 채널에는 400 으로 되돌릴 자리가 없습니다. 끊어버리면 오타 하나로 실시간
        // 알림 전체를 잃습니다.
        assertThat(client.nothingWithin(Duration.ofSeconds(1))).isTrue();
        assertThat(client.session.isOpen()).isTrue();
        assertThat(presenceOf(me).get("status")).isEqualTo("ONLINE");
    }

    /** 테스트용 클라이언트. 받은 프레임을 큐에 쌓고, 닫히면 래치를 내립니다. */
    private final class Client extends TextWebSocketHandler {

        final BlockingQueue<JsonNode> inbox = new LinkedBlockingQueue<>();
        final CountDownLatch closed = new CountDownLatch(1);
        volatile CloseStatus closeStatus;
        WebSocketSession session;

        @Override
        protected void handleTextMessage(WebSocketSession session, TextMessage message) {
            inbox.add(objectMapper.readTree(message.getPayload()));
        }

        @Override
        public void afterConnectionClosed(WebSocketSession session, CloseStatus status) {
            closeStatus = status;
            closed.countDown();
        }

        void hello(String userId) throws IOException {
            session.sendMessage(new TextMessage("{\"type\":\"HELLO\",\"userId\":\"" + userId + "\"}"));
        }

        /** 방을 오갈 때 보내는 프레임. 룸 밖이면 둘 다 null 로 둡니다. */
        void presence(String sessionId, String sessionKind) throws IOException {
            StringBuilder frame = new StringBuilder("{\"type\":\"PRESENCE\"");
            if (sessionId != null) {
                frame.append(",\"sessionId\":\"").append(sessionId).append('"');
            }
            if (sessionKind != null) {
                frame.append(",\"sessionKind\":\"").append(sessionKind).append('"');
            }
            session.sendMessage(new TextMessage(frame.append('}').toString()));
        }

        JsonNode next() throws InterruptedException {
            JsonNode frame = inbox.poll(WAIT.toMillis(), TimeUnit.MILLISECONDS);
            assertThat(frame).as(WAIT.toSeconds() + "초 안에 프레임이 오지 않았습니다.").isNotNull();
            return frame;
        }

        boolean nothingWithin(Duration duration) throws InterruptedException {
            return inbox.poll(duration.toMillis(), TimeUnit.MILLISECONDS) == null;
        }

        void closeQuietly() {
            try {
                if (session != null && session.isOpen()) {
                    session.close();
                }
            } catch (IOException ignored) {
                // 테스트 뒷정리라 실패해도 할 일이 없습니다.
            }
        }
    }

    private Client connect() throws Exception {
        Client client = new Client();
        client.session = new StandardWebSocketClient()
                .execute(client, "ws://localhost:" + port + "/ws/notifications")
                .get(WAIT.toMillis(), TimeUnit.MILLISECONDS);
        clients.add(client);
        return client;
    }

    private Client connectAs(String userId) throws Exception {
        Client client = connect();
        client.hello(userId);
        assertThat(client.next().path("type").asText()).isEqualTo("HELLO_ACK");
        return client;
    }

    private String createUser() throws Exception {
        String body = mvc.perform(post("/api/v1/accounts")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"deviceId\":\"" + UUID.randomUUID() + "\"}"))
                .andExpect(status().isCreated())
                .andReturn().getResponse().getContentAsString();
        return objectMapper.readTree(body).get("userId").asText();
    }

    private void sendRequest(String from, String to) throws Exception {
        mvc.perform(post("/api/v1/friend-requests")
                        .header(USER_ID_HEADER, from)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"userId\":\"" + to + "\"}"))
                .andExpect(status().isCreated());
    }

    private void accept(String me, String requester) throws Exception {
        mvc.perform(post("/api/v1/friend-requests/{userId}/accept", requester).header(USER_ID_HEADER, me))
                .andExpect(status().isNoContent());
    }

    private void befriend(String a, String b) throws Exception {
        sendRequest(a, b);
        accept(b, a);
    }

    private void invite(String from, String to, String roomCode) throws Exception {
        mvc.perform(post("/api/v1/invites")
                        .header(USER_ID_HEADER, from)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"userId\":\"" + to + "\",\"roomCode\":\"" + roomCode + "\"}"))
                .andExpect(status().isCreated());
    }

    /** REST 하트비트. 소켓 없이 보고한 사람을 만들 때만 씁니다. */
    private void heartbeat(String userId, String sessionId) throws Exception {
        mvc.perform(put("/api/v1/presence")
                        .header(USER_ID_HEADER, userId)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(sessionId == null ? "{}" : "{\"sessionId\":\"" + sessionId + "\"}"))
                .andExpect(status().isNoContent());
    }

    private Map<String, Object> presenceOf(String userId) {
        return jdbcTemplate.queryForMap("""
                SELECT p.status, p.session_id, p.heartbeat_at
                  FROM user_presence p
                  JOIN users u ON u.users_seq = p.user_seq
                 WHERE u.public_id = ?
                """, userId);
    }

    private boolean presenceRowMissing(String userId) {
        Integer rows = jdbcTemplate.queryForObject("""
                SELECT COUNT(*)
                  FROM user_presence p
                  JOIN users u ON u.users_seq = p.user_seq
                 WHERE u.public_id = ?
                """, Integer.class, userId);
        return rows != null && rows == 0;
    }

    /** 타임아웃(90초)보다 오래된 하트비트로 바꿔 클라이언트가 조용한 상황을 만듭니다. */
    private void makeHeartbeatStale(String userId) {
        jdbcTemplate.update("UPDATE user_presence p JOIN users u ON u.users_seq = p.user_seq"
                + " SET p.heartbeat_at = ? WHERE u.public_id = ?",
                Timestamps.format(Instant.now().minusSeconds(200)), userId);
    }

    /**
     * 접속 상태가 기대한 값이 될 때까지 기다립니다.
     *
     * <p>연결이 닫히는 것과 프레임이 처리되는 것은 서버 쪽 스레드에서 일어납니다. 클라이언트가
     * {@code close()} 나 {@code sendMessage()} 에서 돌아온 시점에는 아직 쓰이지 않았을 수
     * 있어서, 곧바로 확인하면 간헐적으로 실패합니다.
     */
    private void awaitPresence(String userId, String expected) throws InterruptedException {
        long deadline = System.currentTimeMillis() + WAIT.toMillis();
        while (System.currentTimeMillis() < deadline) {
            if (expected.equals(presenceOf(userId).get("status"))) {
                return;
            }
            Thread.sleep(20);
        }
        assertThat(presenceOf(userId).get("status")).isEqualTo(expected);
    }

    private void awaitConnectionCount(int expected) throws InterruptedException {
        long deadline = System.currentTimeMillis() + WAIT.toMillis();
        while (registry.connectionCount() != expected && System.currentTimeMillis() < deadline) {
            Thread.sleep(20);
        }
        assertThat(registry.connectionCount()).isEqualTo(expected);
    }
}
