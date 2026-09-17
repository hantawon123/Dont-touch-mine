package com.ssafy.d205.api;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.test.web.servlet.MockMvc;
import tools.jackson.databind.ObjectMapper;

import java.time.Duration;
import java.time.Instant;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.ssafy.d205.domain.chat.service.ChatLogSweeper;
import com.ssafy.d205.global.common.Timestamps;
import com.ssafy.d205.support.IntegrationTest;

/**
 * 채팅은 오래 두지 않습니다 (S15P21D205-1031).
 *
 * <p>이 표에는 가리기 전의 원문이 들어 있습니다. 목적이 "신고가 들어왔을 때 확인"이므로 그
 * 목적에 필요한 만큼만 둡니다. <b>이 작업이 멈추면 개인정보가 무한정 쌓입니다.</b>
 *
 * <p>고정 날짜를 쓰지 않습니다. 보관 기간은 지금부터 거슬러 세므로, 달력에 기대는 테스트는
 * 시간이 지나면 코드가 멀쩡한데도 깨집니다.
 */
class ChatLogRetentionTest extends IntegrationTest {

    private static final String CHAT = "/internal/chat";
    private static final String KEY = "test-chat-internal-key";

    @Autowired
    private MockMvc mvc;

    @Autowired
    private ObjectMapper objectMapper;

    @Autowired
    private JdbcTemplate jdbc;

    @Autowired
    private ChatLogSweeper sweeper;

    @Test
    @DisplayName("보관 기간이 지난 줄은 지우고 최근 줄은 남긴다")
    void sweepsOnlyWhatIsOldEnough() throws Exception {
        var room = room();
        send(room, null, "p1", "오래된 말", ago(Duration.ofDays(5)));
        send(room, null, "p1", "어제 말", ago(Duration.ofDays(1)));

        sweeper.sweep();

        assertThat(messagesIn(room)).containsExactly("어제 말");
    }

    @Test
    @DisplayName("아직 판단하지 않은 신고가 가리키는 구간은 남긴다")
    void keepsWhatAPendingReportPointsAt() throws Exception {
        var room = room();
        var reporter = createUser();
        var target = createUser();
        var said = ago(Duration.ofDays(5));

        send(room, target, "p1", "문제가 된 말", said);
        // 같은 방이지만 신고 시각에서 한참 떨어진 줄. 지킬 이유가 없습니다.
        send(room, target, "p1", "한참 전 말", ago(Duration.ofDays(5).plusHours(12)));
        report(reporter, target, room + "#1", said);

        sweeper.sweep();

        // 판단할 근거를 지우면 신고만 남고 판단은 못 합니다.
        assertThat(messagesIn(room)).containsExactly("문제가 된 말");
    }

    @Test
    @DisplayName("경기 키가 없는 옛 신고는 지킬 방을 알 수 없다")
    void cannotProtectReportsWithoutAKey() throws Exception {
        var room = room();
        var reporter = createUser();
        var target = createUser();
        var said = ago(Duration.ofDays(5));

        send(room, target, "p1", "문제가 된 말", said);
        report(reporter, target, null, said);

        sweeper.sweep();

        assertThat(messagesIn(room)).isEmpty();
    }

    private String[] messagesIn(String room) {
        return jdbc.queryForList(
                        "SELECT message FROM chat_logs WHERE room_code = ? ORDER BY sent_at", String.class, room)
                .toArray(String[]::new);
    }

    private static String ago(Duration duration) {
        return Timestamps.format(Instant.now().minus(duration));
    }

    private static String room() {
        return UUID.randomUUID().toString().substring(0, 8).toUpperCase();
    }

    private void send(String room, String userId, String ref, String message, String at) throws Exception {
        mvc.perform(post(CHAT)
                        .header("X-Internal-Key", KEY)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"messages\":[{"
                                + "\"roomCode\":\"" + room + "\","
                                + "\"scope\":\"MATCH\","
                                + "\"userPublicId\":" + (userId == null ? "null" : "\"" + userId + "\"") + ","
                                + "\"senderRef\":\"" + ref + "\","
                                + "\"message\":\"" + message + "\","
                                + "\"sentAt\":\"" + at + "\"}]}"))
                .andExpect(status().isAccepted());
    }

    /**
     * 신고를 넣고 그 시각을 채팅과 같은 때로 맞춥니다.
     *
     * <p>신고 API 는 지금 시각으로 기록하므로, 며칠 전 대화에 대한 신고를 만들려면 행을 직접
     * 손봐야 합니다. 여기서 확인하려는 것은 신고 접수가 아니라 <b>지킬 구간을 어떻게 고르는가</b>
     * 입니다.
     */
    private void report(String reporter, String target, String contextKey, String at) throws Exception {
        mvc.perform(post("/api/v1/reports")
                        .header("X-User-Id", reporter)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"userId\":\"" + target + "\",\"reason\":\"ABUSE\""
                                + (contextKey == null ? "" : ",\"contextKey\":\"" + contextKey + "\"")
                                + "}"))
                .andExpect(status().isCreated());

        jdbc.update("UPDATE user_reports r JOIN users u ON u.users_seq = r.reported_seq"
                + " SET r.created_at = ? WHERE u.public_id = ?", at, target);
    }

    private String createUser() throws Exception {
        var body = mvc.perform(post("/api/v1/accounts")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"deviceId\":\"" + UUID.randomUUID() + "\"}"))
                .andExpect(status().isCreated())
                .andReturn().getResponse().getContentAsString();
        return objectMapper.readTree(body).get("userId").asText();
    }
}
