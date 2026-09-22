package com.ssafy.d205.api;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.test.web.servlet.MockMvc;
import tools.jackson.databind.ObjectMapper;

import java.util.List;
import java.util.Map;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.ssafy.d205.support.IntegrationTest;

/**
 * 게임 서버만 부르는 채팅 경로 (S15P21D205-1027).
 *
 * <p>여기서 고정하려는 것은 넷입니다.
 *
 * <ol>
 *   <li><b>키 없이는 존재하지 않는다.</b> 목록이 새면 그것이 우회 목록이 되고, 기록을 아무나
 *       올릴 수 있으면 남의 이름으로 가짜 대화를 심을 수 있습니다. 정지의 근거가 되는 기록이라
 *       위조 가능하면 없는 것보다 나쁩니다.</li>
 *   <li><b>가린 여부는 게임 서버가 정하고 백엔드는 받아 적는다</b>(S15P21D205-1096). 한때
 *       저장 시점에 다시 판정했는데, 그러면 같은 규칙이 두 언어에 있어야 하고 두 목록이 갈리는
 *       순간 기록이 사람들이 본 화면과 어긋납니다.</li>
 *   <li><b>원문이 저장된다.</b> 가린 것만 남기면 조사가 되지 않습니다.</li>
 *   <li><b>계정을 못 찾아도 줄은 남는다.</b> 그 줄을 버리면 남은 대화가 한쪽 말만 남아 뜻이
 *       뒤집힙니다.</li>
 * </ol>
 */
class InternalChatApiTest extends IntegrationTest {

    private static final String KEY_HEADER = "X-Internal-Key";
    private static final String KEY = "test-chat-internal-key";
    private static final String CHAT = "/internal/chat";
    private static final String BLOCKLIST = "/internal/chat/blocklist";

    @Autowired
    private MockMvc mvc;

    @Autowired
    private ObjectMapper objectMapper;

    @Autowired
    private JdbcTemplate jdbc;

    @Test
    @DisplayName("키가 없으면 경로 자체가 없는 것처럼 404 로 답한다")
    void missingKeyIsNotFound() throws Exception {
        mvc.perform(get(BLOCKLIST))
                .andExpect(status().isNotFound());

        mvc.perform(post(CHAT)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(batch("7K2M9P", "MATCH", null, "p1", "안녕", false, "20260916120000")))
                .andExpect(status().isNotFound());
    }

    @Test
    @DisplayName("키가 틀려도 404 다. 401 이나 403 은 여기 무언가 있다고 알려주는 셈이다")
    void wrongKeyIsAlsoNotFound() throws Exception {
        mvc.perform(get(BLOCKLIST).header(KEY_HEADER, "wrong-key"))
                .andExpect(status().isNotFound());
    }

    @Test
    @DisplayName("목록은 금칙어와 허용 목록을 함께 준다")
    void blocklistIncludesAllowlist() throws Exception {
        String body = mvc.perform(get(BLOCKLIST).header(KEY_HEADER, KEY))
                .andExpect(status().isOk())
                .andReturn().getResponse().getContentAsString();

        var json = objectMapper.readTree(body);

        // 금칙어만 주면 게임 서버가 "Analyst" 를 가립니다. 허용 목록은 그 오탐을 갚는 쪽이라
        // 함께 나가야 하고, 한쪽만 받은 서버는 목록을 절반만 가진 셈입니다.
        assertThat(json.get("blocked").isArray()).isTrue();
        assertThat(json.get("allowed").isArray()).isTrue();
        assertThat(json.get("blocked")).isNotEmpty();
    }

    @Test
    @DisplayName("원문을 저장하고 가린 여부는 받은 값을 그대로 적는다")
    void storesOriginalAndTheFlagAsSent() throws Exception {
        String room = room();

        mvc.perform(post(CHAT)
                        .header(KEY_HEADER, KEY)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(batch(room, "MATCH", null, "p1", "시발", true, "20260916120001")))
                .andExpect(status().isAccepted());

        mvc.perform(post(CHAT)
                        .header(KEY_HEADER, KEY)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(batch(room, "LOBBY", null, "p2", "어디 숨었어", false, "20260916120002")))
                .andExpect(status().isAccepted());

        List<Map<String, Object>> rows = jdbc.queryForList(
                "SELECT message, masked, scope FROM chat_logs WHERE room_code = ? ORDER BY sent_at", room);

        assertThat(rows).hasSize(2);
        // 가린 말이 아니라 원문이 남아야 조사가 됩니다.
        assertThat(rows.get(0).get("message")).isEqualTo("시발");
        assertThat((Boolean) rows.get(0).get("masked")).isTrue();
        assertThat(rows.get(1).get("message")).isEqualTo("어디 숨었어");
        assertThat((Boolean) rows.get(1).get("masked")).isFalse();
        assertThat(rows.get(1).get("scope")).isEqualTo("LOBBY");
    }

    @Test
    @DisplayName("욕설을 안 가렸다고 보내면 안 가린 것으로 남는다. 다시 판정하지 않는다")
    void doesNotJudgeTheMessageAgain() throws Exception {
        // 이관의 핵심입니다(S15P21D205-1096). 백엔드 목록에 있는 말이라도 게임 서버가 안
        // 가렸다고 하면 안 가린 것으로 남습니다 - 기록은 사람들이 본 화면을 말해야 하고,
        // 목록을 못 받아 필터 없이 뜬 방이 실제로 그런 상태입니다.
        String room = room();

        mvc.perform(post(CHAT)
                        .header(KEY_HEADER, KEY)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(batch(room, "MATCH", null, "p1", "시발", false, "20260916120020")))
                .andExpect(status().isAccepted());

        Boolean masked = jdbc.queryForObject(
                "SELECT masked FROM chat_logs WHERE room_code = ?", Boolean.class, room);

        assertThat(masked).isFalse();
    }

    @Test
    @DisplayName("가린 여부를 빠뜨리면 400 이다. 조용히 안 가린 것이 되면 안 된다")
    void rejectsMissingMaskedFlag() throws Exception {
        // 원시형으로 받으면 빠진 필드가 false 가 되어 기록이 통째로 "아무것도 안 가려졌다" 가
        // 됩니다. 게임 서버가 필드를 빠뜨렸다는 사실이 드러나는 편이 낫습니다.
        mvc.perform(post(CHAT)
                        .header(KEY_HEADER, KEY)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"messages\":[{\"roomCode\":\"" + room() + "\","
                                + "\"scope\":\"MATCH\",\"userPublicId\":null,"
                                + "\"senderRef\":\"p1\",\"message\":\"안녕\","
                                + "\"sentAt\":\"20260916120021\"}]}"))
                .andExpect(status().isBadRequest());
    }

    @Test
    @DisplayName("계정이 있으면 그 사람으로 남는다")
    void linksKnownAccount() throws Exception {
        String room = room();
        String userId = createUser();

        mvc.perform(post(CHAT)
                        .header(KEY_HEADER, KEY)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(batch(room, "MATCH", userId, "p1", "안녕", false, "20260916120003")))
                .andExpect(status().isAccepted());

        Integer senderSeq = jdbc.queryForObject(
                "SELECT sender_seq FROM chat_logs WHERE room_code = ?", Integer.class, room);
        Integer expected = jdbc.queryForObject(
                "SELECT users_seq FROM users WHERE public_id = ?", Integer.class, userId);

        assertThat(senderSeq).isEqualTo(expected);
    }

    @Test
    @DisplayName("계정을 몰라도 줄은 남고 발화자는 구분된다")
    void keepsLineWhenAccountUnknown() throws Exception {
        String room = room();

        // 백엔드가 죽어 있을 때 저장된 크리덴셜로 들어온 클라이언트는 자기 계정을 모릅니다
        // (S15P21D205-925). 그 줄을 버리면 대화가 한쪽 말만 남습니다.
        mvc.perform(post(CHAT)
                        .header(KEY_HEADER, KEY)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"messages\":["
                                + entry(room, "MATCH", null, "p1", "야 뭐하냐", false, "20260916120004") + ","
                                + entry(room, "MATCH", null, "p2", "닥쳐", true, "20260916120005") + ","
                                + entry(room, "MATCH", null, "p1", "미안", false, "20260916120006")
                                + "]}"))
                .andExpect(status().isAccepted());

        List<Map<String, Object>> rows = jdbc.queryForList(
                "SELECT sender_seq, sender_ref FROM chat_logs WHERE room_code = ? ORDER BY sent_at", room);

        assertThat(rows).hasSize(3);
        assertThat(rows).allSatisfy(row -> assertThat(row.get("sender_seq")).isNull());
        // 누구인지는 몰라도 몇 사람이 말했는지는 구분됩니다.
        assertThat(rows.stream().map(row -> row.get("sender_ref")).distinct()).hasSize(2);
    }

    @Test
    @DisplayName("같은 말이 여러 줄 있어도 줄마다 제 발신자와 제 판정이 붙는다")
    void repeatedMessagesKeepTheirOwnSenderAndVerdict() throws Exception {
        // 묶음을 배치 insert 로 넣습니다(S15P21D205-1077). 줄과 값(발신자, 판정)을 자리로 맞추는
        // 코드라, 어긋나면 남의 말이 남의 이름으로 남습니다. 정지의 근거가 되는 기록이므로
        // 같은 말이 반복되는 흔한 경우로 고정해 둡니다.
        String room = room();
        String first = createUser();
        String second = createUser();

        mvc.perform(post(CHAT)
                        .header(KEY_HEADER, KEY)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"messages\":["
                                 + entry(room, "MATCH", first, "p1", "ㅋㅋ", false, "20260916120010") + ","
                                 + entry(room, "MATCH", second, "p2", "ㅋㅋ", false, "20260916120011") + ","
                                 + entry(room, "MATCH", first, "p1", "시발", true, "20260916120012") + ","
                                 + entry(room, "MATCH", second, "p2", "시발", true, "20260916120013") + "]}"))
                .andExpect(status().isAccepted());

        List<Map<String, Object>> rows = jdbc.queryForList("""
                SELECT u.public_id AS speaker, c.sender_ref AS ref, c.message AS message, c.masked AS masked
                  FROM chat_logs c JOIN users u ON u.users_seq = c.sender_seq
                 WHERE c.room_code = ? ORDER BY c.sent_at
                """, room);

        assertThat(rows).hasSize(4);
        assertThat(rows.get(0)).containsEntry("speaker", first).containsEntry("ref", "p1");
        assertThat(rows.get(1)).containsEntry("speaker", second).containsEntry("ref", "p2");
        assertThat(rows.get(2)).containsEntry("speaker", first);
        assertThat(rows.get(3)).containsEntry("speaker", second);
        // 같은 말이 반복돼도 받은 값이 줄마다 제자리에 붙습니다.
        assertThat(rows.get(0).get("masked")).isEqualTo(false);
        assertThat(rows.get(1).get("masked")).isEqualTo(false);
        assertThat(rows.get(2).get("masked")).isEqualTo(true);
        assertThat(rows.get(3).get("masked")).isEqualTo(true);
    }

    @Test
    @DisplayName("80자를 넘는 말은 400 이다. 클라이언트 상한과 같아야 한다")
    void rejectsTooLongMessage() throws Exception {
        mvc.perform(post(CHAT)
                        .header(KEY_HEADER, KEY)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(batch(room(), "MATCH", null, "p1", "가".repeat(81), false, "20260916120007")))
                .andExpect(status().isBadRequest());
    }

    private static String room() {
        return UUID.randomUUID().toString().substring(0, 8).toUpperCase();
    }

    private static String batch(String room, String scope, String userId, String ref,
                                String message, boolean masked, String sentAt) {
        return "{\"messages\":[" + entry(room, scope, userId, ref, message, masked, sentAt) + "]}";
    }

    private static String entry(String room, String scope, String userId, String ref,
                                String message, boolean masked, String sentAt) {
        return "{\"roomCode\":\"" + room + "\","
                + "\"scope\":\"" + scope + "\","
                + "\"userPublicId\":" + (userId == null ? "null" : "\"" + userId + "\"") + ","
                + "\"senderRef\":\"" + ref + "\","
                + "\"message\":\"" + message + "\","
                + "\"masked\":" + masked + ","
                + "\"sentAt\":\"" + sentAt + "\"}";
    }

    private String createUser() throws Exception {
        String body = mvc.perform(post("/api/v1/accounts")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"deviceId\":\"" + UUID.randomUUID() + "\"}"))
                .andExpect(status().isCreated())
                .andReturn().getResponse().getContentAsString();

        return objectMapper.readTree(body).get("userId").asText();
    }
}
