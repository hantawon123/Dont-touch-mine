package com.ssafy.d205.api;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.test.web.servlet.MockMvc;
import tools.jackson.databind.ObjectMapper;

import java.time.Instant;
import java.util.Map;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.ssafy.d205.domain.presence.service.PresenceSweeper;
import com.ssafy.d205.global.common.Timestamps;
import com.ssafy.d205.support.IntegrationTest;

class PresenceApiTest extends IntegrationTest {

    private static final String USER_ID_HEADER = "X-User-Id";

    @Autowired
    MockMvc mvc;

    @Autowired
    ObjectMapper objectMapper;

    @Autowired
    JdbcTemplate jdbcTemplate;

    @Autowired
    PresenceSweeper presenceSweeper;

    @Test
    @DisplayName("첫 하트비트가 접속 기록을 만든다")
    void firstHeartbeatCreatesRow() throws Exception {
        String me = createUser();

        heartbeat(me, null);

        Map<String, Object> row = presenceOf(me);
        assertThat(row.get("status")).isEqualTo("ONLINE");
        assertThat(row.get("session_id")).isNull();
    }

    @Test
    @DisplayName("sessionId를 보내면 IN_GAME이 되고 그 값이 저장된다")
    void sessionIdMakesInGame() throws Exception {
        String me = createUser();

        heartbeat(me, "ROOM42");

        Map<String, Object> row = presenceOf(me);
        assertThat(row.get("status")).isEqualTo("IN_GAME");
        assertThat(row.get("session_id")).isEqualTo("ROOM42");
    }

    @Test
    @DisplayName("같은 상태로 하트비트를 보내면 heartbeat_at만 갱신된다")
    void sameStatusOnlyUpdatesHeartbeat() throws Exception {
        // V3 가 두 컬럼을 구분한 이유입니다. 하트비트는 30초마다 오고 상태 변화는 드물게
        // 일어나므로, updated_at 이 매번 갱신되면 "언제부터 이 상태인지"를 알 수 없습니다.
        String me = createUser();
        heartbeat(me, null);

        // 시각이 초 단위라 같은 초에 두 번 부르면 값이 같습니다. 하트비트를 과거로
        // 밀어두고 그 값을 기준으로 삼아야 갱신 여부를 구분할 수 있습니다.
        makeHeartbeatOld(me);
        Map<String, Object> before = presenceOf(me);

        heartbeat(me, null);
        Map<String, Object> after = presenceOf(me);

        assertThat(after.get("updated_at")).isEqualTo(before.get("updated_at"));
        assertThat(after.get("heartbeat_at")).isNotEqualTo(before.get("heartbeat_at"));
    }

    @Test
    @DisplayName("상태가 바뀌면 updated_at도 갱신된다")
    void statusChangeUpdatesUpdatedAt() throws Exception {
        String me = createUser();
        heartbeat(me, null);
        makeUpdatedAtOld(me);
        Map<String, Object> before = presenceOf(me);

        heartbeat(me, "ROOM01");

        assertThat(presenceOf(me).get("updated_at")).isNotEqualTo(before.get("updated_at"));
    }

    @Test
    @DisplayName("방을 옮기면 상태는 그대로지만 updated_at이 갱신된다")
    void roomChangeUpdatesUpdatedAt() throws Exception {
        // IN_GAME 은 그대로인데 sessionId 가 달라집니다. 그 시점을 남기는 것이 맞다고
        // 보고 상태 변화로 취급합니다.
        String me = createUser();
        heartbeat(me, "ROOM01");
        makeUpdatedAtOld(me);
        Map<String, Object> before = presenceOf(me);

        heartbeat(me, "ROOM02");

        Map<String, Object> after = presenceOf(me);
        assertThat(after.get("status")).isEqualTo("IN_GAME");
        assertThat(after.get("session_id")).isEqualTo("ROOM02");
        assertThat(after.get("updated_at")).isNotEqualTo(before.get("updated_at"));
    }

    @Test
    @DisplayName("DELETE로 즉시 오프라인이 되고 sessionId가 비워진다")
    void deleteGoesOffline() throws Exception {
        String me = createUser();
        heartbeat(me, "ROOM01");

        mvc.perform(delete("/api/v1/presence").header(USER_ID_HEADER, me))
                .andExpect(status().isNoContent());

        Map<String, Object> row = presenceOf(me);
        assertThat(row.get("status")).isEqualTo("OFFLINE");
        assertThat(row.get("session_id")).isNull();
    }

    @Test
    @DisplayName("접속 기록이 없는데 DELETE를 불러도 오류가 아니다")
    void deleteWithoutPresenceIsFine() throws Exception {
        // 이미 오프라인이므로 할 일이 없습니다. 행을 만들 이유도 없습니다.
        String me = createUser();

        mvc.perform(delete("/api/v1/presence").header(USER_ID_HEADER, me))
                .andExpect(status().isNoContent());

        assertThat(jdbcTemplate.queryForObject("""
                SELECT COUNT(*) FROM user_presence p
                  JOIN users u ON u.users_seq = p.user_seq
                 WHERE u.public_id = ?
                """, Integer.class, me)).isZero();
    }

    @Test
    @DisplayName("스윕이 하트비트가 끊긴 행을 실제로 오프라인으로 내린다")
    void sweepMarksStaleRowsOffline() throws Exception {
        String stale = createUser();
        String alive = createUser();
        heartbeat(stale, "ROOM01");
        heartbeat(alive, "ROOM02");
        makeHeartbeatStale(stale);

        presenceSweeper.sweep();

        Map<String, Object> sweptRow = presenceOf(stale);
        assertThat(sweptRow.get("status")).isEqualTo("OFFLINE");
        assertThat(sweptRow.get("session_id")).isNull();

        // 살아 있는 쪽은 건드리지 않아야 합니다.
        assertThat(presenceOf(alive).get("status")).isEqualTo("IN_GAME");
    }

    @Test
    @DisplayName("sessionId가 64자를 넘으면 400")
    void tooLongSessionIdIsBadRequest() throws Exception {
        String me = createUser();

        mvc.perform(put("/api/v1/presence")
                        .header(USER_ID_HEADER, me)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"sessionId\":\"" + "A".repeat(65) + "\"}"))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("INVALID_REQUEST"));
    }

    @Test
    @DisplayName("X-User-Id 헤더가 없으면 400")
    void missingHeaderIsBadRequest() throws Exception {
        mvc.perform(put("/api/v1/presence")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{}"))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("MISSING_HEADER"));
    }

    @Test
    @DisplayName("없는 계정으로 하트비트를 보내면 404")
    void unknownCallerIsNotFound() throws Exception {
        mvc.perform(put("/api/v1/presence")
                        .header(USER_ID_HEADER, UUID.randomUUID().toString())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{}"))
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.code").value("ACCOUNT_NOT_FOUND"));
    }

    private String createUser() throws Exception {
        String body = mvc.perform(post("/api/v1/accounts")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"deviceId\":\"" + UUID.randomUUID() + "\"}"))
                .andExpect(status().isCreated())
                .andReturn().getResponse().getContentAsString();
        return objectMapper.readTree(body).get("userId").asText();
    }

    private void heartbeat(String userId, String sessionId) throws Exception {
        String body = sessionId == null ? "{}" : "{\"sessionId\":\"" + sessionId + "\"}";
        mvc.perform(put("/api/v1/presence")
                        .header(USER_ID_HEADER, userId)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(body))
                .andExpect(status().isNoContent());
    }

    private Map<String, Object> presenceOf(String userId) {
        return jdbcTemplate.queryForMap("""
                SELECT p.status, p.session_id, p.heartbeat_at, p.updated_at
                  FROM user_presence p
                  JOIN users u ON u.users_seq = p.user_seq
                 WHERE u.public_id = ?
                """, userId);
    }

    /** 시각이 초 단위라 같은 초에 두 번 부르면 값이 같습니다. 과거로 밀어 구분합니다. */
    private void makeHeartbeatOld(String userId) {
        setColumn(userId, "heartbeat_at", Timestamps.format(Instant.now().minusSeconds(10)));
    }

    private void makeUpdatedAtOld(String userId) {
        setColumn(userId, "updated_at", Timestamps.format(Instant.now().minusSeconds(10)));
    }

    /** 타임아웃(90초)보다 오래된 하트비트로 바꿔 크래시 상황을 만듭니다. */
    private void makeHeartbeatStale(String userId) {
        setColumn(userId, "heartbeat_at", Timestamps.format(Instant.now().minusSeconds(200)));
    }

    private void setColumn(String userId, String column, String value) {
        jdbcTemplate.update("UPDATE user_presence p JOIN users u ON u.users_seq = p.user_seq"
                + " SET p." + column + " = ? WHERE u.public_id = ?", value, userId);
    }
}
