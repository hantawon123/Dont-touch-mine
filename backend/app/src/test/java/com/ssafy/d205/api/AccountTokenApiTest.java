package com.ssafy.d205.api;

import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.http.MediaType;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.test.context.TestPropertySource;
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
import java.util.ArrayList;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.BlockingQueue;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.LinkedBlockingQueue;
import java.util.concurrent.TimeUnit;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.ssafy.d205.support.IntegrationTest;

/**
 * 게임 API 와 알림 소켓의 계정 토큰 검사 (S15P21D205-975).
 *
 * <p>X-User-Id 는 공개 식별자라 그것만 믿으면 남의 이름으로 신고·친구 끊기·피드백을 보낼 수
 * 있고, 정지된 사람이 남의 id 로 정지 검사를 피할 수 있습니다. 계정 응답의 photonToken 을
 * X-Account-Token 으로 함께 요구해 그 길을 닫습니다.
 *
 * <p>비밀을 테스트 프로퍼티로 넣습니다. 나머지 스위트는 비밀 없이 돌아 검사가 꺼진 채이고,
 * 그래서 기존 테스트 열여덟 파일의 헤더를 고치지 않아도 됩니다. 인터셉터는 거부만 하고
 * 통과한 요청의 동작을 바꾸지 않으므로, 검사 자체는 이 클래스 하나가 봅니다.
 *
 * <p>소켓 검사가 있어 진짜 포트에 띄웁니다(NotificationWebSocketTest 와 같은 이유).
 */
@SpringBootTest(webEnvironment = SpringBootTest.WebEnvironment.RANDOM_PORT)
@TestPropertySource(properties = {
        "photon.auth.secret=test-account-token-secret",
        "photon.auth.key=test-dashboard-key"
})
class AccountTokenApiTest extends IntegrationTest {

    private static final String USER_ID = "X-User-Id";
    private static final String TOKEN = "X-Account-Token";
    private static final String ME = "/api/v1/accounts/me";
    private static final Duration WAIT = Duration.ofSeconds(5);

    @Value("${local.server.port}")
    int port;

    @Autowired
    MockMvc mvc;

    @Autowired
    ObjectMapper objectMapper;

    @Autowired
    JdbcTemplate jdbcTemplate;

    private final List<Client> clients = new ArrayList<>();

    @AfterEach
    void closeClients() {
        for (Client client : clients) {
            client.closeQuietly();
        }
        clients.clear();
    }

    @Test
    @DisplayName("맞는 토큰을 실으면 지금까지처럼 된다")
    void theRightTokenPasses() throws Exception {
        Account me = createAccount();

        mvc.perform(get(ME).header(USER_ID, me.userId()).header(TOKEN, me.token()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.userId").value(me.userId()));
    }

    @Test
    @DisplayName("토큰 없이 X-User-Id 만 보내면 401")
    void aBareUserIdIsRefused() throws Exception {
        // 지금까지의 클라이언트가 보내던 모양입니다. 이 요청이 통과하면 이 기능은 없는 것입니다.
        Account me = createAccount();

        mvc.perform(get(ME).header(USER_ID, me.userId()))
                .andExpect(status().isUnauthorized())
                .andExpect(jsonPath("$.code").value("UNAUTHORIZED"));
    }

    @Test
    @DisplayName("남의 userId 에 내 토큰을 붙이면 401")
    void someoneElsesIdWithMyTokenIsRefused() throws Exception {
        // 이 테스트가 이 기능의 요점입니다. 같은 방에 있던 사람은 서로의 userId 를 압니다.
        Account me = createAccount();
        Account other = createAccount();

        mvc.perform(get(ME).header(USER_ID, other.userId()).header(TOKEN, me.token()))
                .andExpect(status().isUnauthorized())
                .andExpect(jsonPath("$.code").value("UNAUTHORIZED"));
    }

    @Test
    @DisplayName("토큰 자리에 아무 문자열이 와도 500 이 아니라 401")
    void garbageInTheTokenSlotIsRefusedNotCrashed() throws Exception {
        Account me = createAccount();

        mvc.perform(get(ME).header(USER_ID, me.userId()).header(TOKEN, "not base64 at all !!"))
                .andExpect(status().isUnauthorized());
    }

    @Test
    @DisplayName("계정 발급은 토큰 없이 된다")
    void issuingNeedsNoToken() throws Exception {
        // 토큰은 이 응답으로 받습니다. 여기서 요구하면 아무도 첫 토큰을 받을 수 없습니다.
        Account issued = createAccount();

        assertThat(issued.userId()).isNotBlank();
        assertThat(issued.token()).isNotBlank();
    }

    @Test
    @DisplayName("정지된 계정도 토큰이 없으면 403 이 아니라 401 이다")
    void suspensionIsOnlyVisibleWithAToken() throws Exception {
        // 순서가 뜻입니다. 토큰 검사가 정지 검사보다 앞에 있어야 남의 id 를 넣어 정지 여부를
        // 밖에서 알아낼 수 없습니다(정지면 403, 아니면 401 로 갈리는 것을 막습니다).
        Account me = createAccount();
        suspend(me.userId());

        mvc.perform(get(ME).header(USER_ID, me.userId()))
                .andExpect(status().isUnauthorized());

        mvc.perform(get(ME).header(USER_ID, me.userId()).header(TOKEN, me.token()))
                .andExpect(status().isForbidden())
                .andExpect(jsonPath("$.code").value("SUSPENDED"));
    }

    @Test
    @DisplayName("관리자 로그인과 헬스 체크는 영향이 없다")
    void adminAndHealthAreUntouched() throws Exception {
        mvc.perform(get("/actuator/health")).andExpect(status().isOk());

        // 관리자 경로는 세션으로 인증합니다. X-User-Id 가 없으니 어차피 지나가지만, 제외
        // 경로에서 빠지면 정지를 푸는 길이 막히므로 고정해 둡니다.
        mvc.perform(post("/api/v1/admin/session")
                        .param("username", "test-admin")
                        .param("password", "test-password"))
                .andExpect(status().isNoContent());
    }

    @Test
    @DisplayName("HELLO 에 맞는 토큰이 있으면 HELLO_ACK 가 온다")
    void helloWithTheRightTokenIsAcknowledged() throws Exception {
        Account me = createAccount();
        Client client = connect();

        client.hello(me.userId(), me.token());

        assertThat(client.next().path("type").asText()).isEqualTo("HELLO_ACK");
    }

    @Test
    @DisplayName("HELLO 에 토큰이 없거나 남의 것이면 끊는다")
    void helloWithoutItsTokenIsClosed() throws Exception {
        // REST 를 막고 이 채널을 열어 두면 남의 친구 요청·초대 알림을 계속 받을 수 있습니다.
        Account me = createAccount();
        Account other = createAccount();

        Client bare = connect();
        bare.hello(me.userId(), null);
        assertThat(bare.closed.await(WAIT.toMillis(), TimeUnit.MILLISECONDS)).isTrue();
        assertThat(bare.closeStatus.getCode()).isEqualTo(CloseStatus.POLICY_VIOLATION.getCode());
        assertThat(bare.closeStatus.getReason()).isEqualTo("UNAUTHORIZED");

        Client stolen = connect();
        stolen.hello(me.userId(), other.token());
        assertThat(stolen.closed.await(WAIT.toMillis(), TimeUnit.MILLISECONDS)).isTrue();
        assertThat(stolen.closeStatus.getCode()).isEqualTo(CloseStatus.POLICY_VIOLATION.getCode());
    }

    private record Account(String userId, String token) {
    }

    private Account createAccount() throws Exception {
        String body = mvc.perform(post("/api/v1/accounts")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"deviceId\":\"" + UUID.randomUUID() + "\"}"))
                .andExpect(status().isCreated())
                .andReturn().getResponse().getContentAsString();

        JsonNode json = objectMapper.readTree(body);
        return new Account(json.get("userId").asText(), json.get("photonToken").asText());
    }

    private void suspend(String userId) {
        // SQL 로 직접 바꿉니다. 정지 API 를 부르려면 운영자 로그인이 필요한데 이 테스트의
        // 관심사가 아닙니다(NotificationWebSocketTest 와 같은 방식).
        jdbcTemplate.update(
                "UPDATE users SET suspended_at = ?, suspended_reason = ? WHERE public_id = ?",
                "20260914000000", "테스트", userId);
    }

    /** 테스트용 소켓 클라이언트. 받은 프레임을 큐에 쌓고, 닫히면 래치를 내립니다. */
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

        void hello(String userId, String token) throws IOException {
            StringBuilder frame = new StringBuilder("{\"type\":\"HELLO\",\"userId\":\"")
                    .append(userId).append('"');
            if (token != null) {
                frame.append(",\"token\":\"").append(token).append('"');
            }
            session.sendMessage(new TextMessage(frame.append('}').toString()));
        }

        JsonNode next() throws InterruptedException {
            JsonNode frame = inbox.poll(WAIT.toMillis(), TimeUnit.MILLISECONDS);
            assertThat(frame).as(WAIT.toSeconds() + "초 안에 프레임이 오지 않았습니다.").isNotNull();
            return frame;
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
}
