package com.ssafy.d205.api;

import jakarta.servlet.http.Cookie;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.mock.web.MockHttpSession;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.MvcResult;
import tools.jackson.databind.ObjectMapper;

import java.time.Duration;
import java.time.Instant;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.ssafy.d205.global.common.Timestamps;
import com.ssafy.d205.support.IntegrationTest;

/**
 * 신고에서 그 방의 대화로 (S15P21D205-1030).
 *
 * <p>여기서 고정하려는 것은 넷입니다.
 *
 * <ol>
 *   <li><b>신고에서 방을 찾을 수 있다.</b> 경기 키가 상세에 실려 나오지 않으면 운영자는 어느
 *       방이었는지 알 수 없고, 채팅을 아무리 쌓아도 쓰지 못합니다.</li>
 *   <li><b>구간 밖은 안 보인다.</b> 필요한 만큼만 펼칩니다.</li>
 *   <li><b>이름을 몰라도 발화자는 구분된다.</b> 전부 "알 수 없음"이면 대화가 읽히지 않습니다.</li>
 *   <li><b>운영자만 볼 수 있다.</b> 채팅 원문은 개인정보입니다.</li>
 * </ol>
 */
class AdminChatApiTest extends IntegrationTest {

    private static final String LOGIN = "/api/v1/admin/session";
    private static final String AROUND = "/api/v1/admin/chat/around";
    private static final String CHAT = "/internal/chat";
    private static final String KEY = "test-chat-internal-key";

    @Autowired
    private MockMvc mvc;

    @Autowired
    private ObjectMapper objectMapper;

    @Test
    @DisplayName("신고 상세가 경기 키를 실어 보낸다. 없으면 대화를 찾아갈 수 없다")
    void reportDetailCarriesTheContextKey() throws Exception {
        var reporter = createUser();
        var target = createUser();
        report(reporter, target, "7K2M9P#2");

        var admin = login();
        var body = mvc.perform(get("/api/v1/admin/reports/" + target).session(admin.session()))
                .andExpect(status().isOk())
                .andReturn().getResponse().getContentAsString();

        assertThat(objectMapper.readTree(body).get("reports").get(0).get("contextKey").asText())
                .isEqualTo("7K2M9P#2");
    }

    @Test
    @DisplayName("경기 키의 방에서 신고 시각 앞뒤 대화만 보여준다")
    void showsTheConversationAroundTheReport() throws Exception {
        var room = room();
        send(room, "MATCH", null, "p1", "시작하자", "20260917010000");
        send(room, "MATCH", null, "p2", "야 시발", "20260917010500");
        // 구간 밖. 같은 방이지만 몇 시간 전 이야기입니다.
        send(room, "MATCH", null, "p1", "어제 뭐했어", "20260916220000");

        var response = around(room + "#1", "20260917010400", 15);

        assertThat(response.get("lines")).hasSize(2);
        assertThat(response.get("lines").get(0).get("message").asText()).isEqualTo("시작하자");
        assertThat(response.get("lines").get(1).get("message").asText()).isEqualTo("야 시발");
        // 원문이 나와야 판단이 됩니다. 플레이어들이 본 것은 가려진 문장입니다.
        assertThat(response.get("lines").get(1).get("masked").asBoolean()).isTrue();
        assertThat(response.get("maskedCount").asInt()).isEqualTo(1);
    }

    @Test
    @DisplayName("이름을 몰라도 발화자는 서로 구분된다")
    void unknownSpeakersAreStillToldApart() throws Exception {
        var room = room();
        send(room, "MATCH", null, "p1", "야 뭐하냐", "20260917010001");
        send(room, "MATCH", null, "p2", "닥쳐", "20260917010002");
        send(room, "MATCH", null, "p1", "미안", "20260917010003");

        var lines = around(room + "#0", "20260917010002", 5).get("lines");

        assertThat(lines).hasSize(3);
        var first = lines.get(0).get("speaker").asText();
        var second = lines.get(1).get("speaker").asText();
        assertThat(first).isNotEqualTo(second);
        // 같은 사람이 다시 말하면 같은 딱지입니다. 그러지 않으면 세 사람처럼 보입니다.
        assertThat(lines.get(2).get("speaker").asText()).isEqualTo(first);
        assertThat(lines.get(0).get("userId").isNull()).isTrue();
    }

    @Test
    @DisplayName("계정을 아는 줄은 지금 닉네임으로 보인다")
    void knownSpeakerShowsTheCurrentNickname() throws Exception {
        var room = room();
        var speaker = createUser();
        send(room, "LOBBY", speaker, "p1", "안녕하세요", "20260917020000");

        var line = around(room + "#0", "20260917020000", 5).get("lines").get(0);

        assertThat(line.get("userId").asText()).isEqualTo(speaker);
        assertThat(line.get("speaker").asText()).isNotBlank();
        assertThat(line.get("scope").asText()).isEqualTo("LOBBY");
    }

    @Test
    @DisplayName("경기 키가 없는 옛 신고는 찾을 방이 없다")
    void oldReportsWithoutAKeyFindNothing() throws Exception {
        var response = around("", "20260917010000", 15);

        assertThat(response.get("lines")).isEmpty();
        // 구간이 null 인 것으로 화면이 "대화 없음"과 "방을 모름"을 구분합니다.
        assertThat(response.get("from").isNull()).isTrue();
    }

    @Test
    @DisplayName("이 사람이 여러 방에서 한 말을 모아 본다")
    void collectsWhatOnePersonSaidAcrossRooms() throws Exception {
        var speaker = createUser();
        var first = room();
        var second = room();
        // 고정 날짜를 쓰면 이 테스트는 오늘에 기댑니다. 조회 구간은 지금부터 거슬러 세고
        // 컨트롤러가 최대 30일로 깎으므로, 한 달만 지나면 고정 날짜가 구간 밖으로 밀려
        // 코드가 멀쩡한데도 깨집니다.
        send(first, "MATCH", speaker, "p1", "시발", ago(Duration.ofHours(2)));
        send(second, "MATCH", speaker, "p1", "병신", ago(Duration.ofHours(1)));
        send(second, "MATCH", null, "p2", "그만해", ago(Duration.ofMinutes(59)));

        var admin = login();
        var body = mvc.perform(get("/api/v1/admin/chat/by/" + speaker)
                        .param("days", "7")
                        .session(admin.session()))
                .andExpect(status().isOk())
                .andReturn().getResponse().getContentAsString();

        var lines = objectMapper.readTree(body).get("lines");
        assertThat(lines).hasSize(2);
        // 한 방에서 한 번은 실수일 수 있어도 여러 방에서 반복하면 다른 판단이 됩니다.
        assertThat(lines.get(0).get("roomCode").asText()).isNotEqualTo(lines.get(1).get("roomCode").asText());
    }

    @Test
    @DisplayName("없는 계정을 물으면 404 다. 말한 적 없는 사람과 구분된다")
    void unknownAccountIsNotFound() throws Exception {
        var admin = login();
        mvc.perform(get("/api/v1/admin/chat/by/" + UUID.randomUUID()).session(admin.session()))
                .andExpect(status().isNotFound());
    }

    @Test
    @DisplayName("시각 형식이 아니면 400 이다. 500 으로 터지지 않는다")
    void malformedTimeIsRejected() throws Exception {
        var admin = login();
        mvc.perform(get(AROUND)
                        .param("contextKey", "7K2M9P#1")
                        .param("reportedAt", "어제쯤")
                        .session(admin.session()))
                .andExpect(status().isBadRequest());
    }

    @Test
    @DisplayName("로그인하지 않으면 대화를 볼 수 없다")
    void requiresAnAdminSession() throws Exception {
        mvc.perform(get(AROUND).param("contextKey", "7K2M9P#1").param("reportedAt", "20260917010000"))
                .andExpect(status().is4xxClientError());
    }

    private tools.jackson.databind.JsonNode around(String contextKey, String at, int minutes)
            throws Exception {
        var admin = login();
        var body = mvc.perform(get(AROUND)
                        .param("contextKey", contextKey)
                        .param("reportedAt", at)
                        .param("minutes", String.valueOf(minutes))
                        .session(admin.session()))
                .andExpect(status().isOk())
                .andReturn().getResponse().getContentAsString();
        return objectMapper.readTree(body);
    }

    private void send(String room, String scope, String userId, String ref, String message, String at)
            throws Exception {
        mvc.perform(post(CHAT)
                        .header("X-Internal-Key", KEY)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"messages\":[{"
                                + "\"roomCode\":\"" + room + "\","
                                + "\"scope\":\"" + scope + "\","
                                + "\"userPublicId\":" + (userId == null ? "null" : "\"" + userId + "\"") + ","
                                + "\"senderRef\":\"" + ref + "\","
                                + "\"message\":\"" + message + "\","
                                + "\"sentAt\":\"" + at + "\"}]}"))
                .andExpect(status().isAccepted());
    }

    private void report(String reporter, String target, String contextKey) throws Exception {
        mvc.perform(post("/api/v1/reports")
                        .header("X-User-Id", reporter)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"userId\":\"" + target + "\",\"reason\":\"ABUSE\","
                                + "\"contextKey\":\"" + contextKey + "\"}"))
                .andExpect(status().isCreated());
    }

    /** 지금으로부터 얼마 전. 고정 날짜를 쓰면 테스트가 달력에 기댑니다. */
    private static String ago(Duration duration) {
        return Timestamps.format(Instant.now().minus(duration));
    }

    private static String room() {
        return UUID.randomUUID().toString().substring(0, 8).toUpperCase();
    }

    private String createUser() throws Exception {
        var body = mvc.perform(post("/api/v1/accounts")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"deviceId\":\"" + UUID.randomUUID() + "\"}"))
                .andExpect(status().isCreated())
                .andReturn().getResponse().getContentAsString();
        return objectMapper.readTree(body).get("userId").asText();
    }

    private Admin login() throws Exception {
        var session = new MockHttpSession();
        MvcResult result = mvc.perform(post(LOGIN)
                        .session(session)
                        .param("username", "test-admin")
                        .param("password", "test-password"))
                .andExpect(status().isNoContent())
                .andReturn();
        return new Admin(session, result.getResponse().getCookie("XSRF-TOKEN"));
    }

    private record Admin(MockHttpSession session, Cookie csrf) {
    }
}
