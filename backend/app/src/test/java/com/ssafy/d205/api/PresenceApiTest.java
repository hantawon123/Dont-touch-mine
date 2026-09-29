package com.ssafy.d205.api;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.test.web.servlet.MockMvc;
import tools.jackson.databind.ObjectMapper;

import java.time.Instant;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.ssafy.d205.domain.presence.entity.SessionKind;
import com.ssafy.d205.domain.presence.service.PresenceService;
import com.ssafy.d205.domain.presence.service.PresenceSweeper;
import com.ssafy.d205.global.common.Timestamps;
import com.ssafy.d205.support.IntegrationTest;
import com.ssafy.d205.support.StatementRecorder;

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

    @Autowired
    PresenceService presenceService;

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
    @DisplayName("sessionKind 없이 sessionId만 보내면 IN_GAME이 되고 그 값이 저장된다")
    void sessionIdWithoutKindMakesInGame() throws Exception {
        // sessionKind 가 생기기 전의 클라이언트가 보내는 모양입니다. 그때 룸 안은 곧
        // IN_GAME 이었으므로 없는 값을 MATCH 로 읽습니다. LOBBY 로 읽으면 이미 배포된
        // 클라이언트에서 경기 중인 사용자가 전부 로비로 보입니다.
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
    @DisplayName("sessionKind가 LOBBY면 IN_LOBBY가 된다")
    void lobbyKindMakesInLobby() throws Exception {
        String me = createUser();

        heartbeat(me, "ROOM42", "LOBBY");

        Map<String, Object> row = presenceOf(me);
        assertThat(row.get("status")).isEqualTo("IN_LOBBY");
        assertThat(row.get("session_id")).isEqualTo("ROOM42");
    }

    @Test
    @DisplayName("sessionKind가 MATCH면 IN_GAME이 된다")
    void matchKindMakesInGame() throws Exception {
        String me = createUser();

        heartbeat(me, "ROOM42", "MATCH");

        assertThat(presenceOf(me).get("status")).isEqualTo("IN_GAME");
    }

    @Test
    @DisplayName("로비에서 경기로 넘어가면 sessionId가 같아도 updated_at이 갱신된다")
    void lobbyToMatchUpdatesUpdatedAt() throws Exception {
        // 로비와 경기는 같은 Photon 룸이라 sessionId 가 바뀌지 않습니다. 방을 옮긴 것과
        // 달리 sessionKind 만 달라지고, 그것도 상태 변화이므로 시점을 남겨야 합니다.
        String me = createUser();
        heartbeat(me, "ROOM42", "LOBBY");
        makeUpdatedAtOld(me);
        Map<String, Object> before = presenceOf(me);

        heartbeat(me, "ROOM42", "MATCH");

        Map<String, Object> after = presenceOf(me);
        assertThat(after.get("status")).isEqualTo("IN_GAME");
        assertThat(after.get("session_id")).isEqualTo("ROOM42");
        assertThat(after.get("updated_at")).isNotEqualTo(before.get("updated_at"));
    }

    @Test
    @DisplayName("sessionId 없이 sessionKind만 보내면 ONLINE이다")
    void kindWithoutSessionIdIsOnline() throws Exception {
        // 룸 밖인데 로비라고 주장하는 요청입니다. 400 으로 되돌릴 수도 있지만 상태는 이미
        // ONLINE 하나로 정해져 있어 되돌려서 나아지는 것이 없습니다.
        String me = createUser();

        perform(me, "{\"sessionKind\":\"LOBBY\"}");

        Map<String, Object> row = presenceOf(me);
        assertThat(row.get("status")).isEqualTo("ONLINE");
        assertThat(row.get("session_id")).isNull();
    }

    @Test
    @DisplayName("모르는 sessionKind는 400")
    void unknownSessionKindIsBadRequest() throws Exception {
        String me = createUser();

        mvc.perform(put("/api/v1/presence")
                        .header(USER_ID_HEADER, me)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"sessionId\":\"ROOM42\",\"sessionKind\":\"HIGHLIGHT\"}"))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("INVALID_REQUEST"));
    }

    @Test
    @DisplayName("빈 문자열 sessionKind는 400")
    void emptySessionKindIsBadRequest() throws Exception {
        // 클라이언트가 실수로 보낼 수 있는 모양이라 확인해 둡니다. Unity 의 JsonUtility 는
        // null 문자열을 "" 로 씁니다. sessionKind 를 채우지 않은 DTO 를 그대로 보내면 이
        // 요청이 되고, 조용히 MATCH 로 처리되는 것보다 거절하는 편이 낫습니다 -- 클라이언트가
        // 그 필드를 보내려 했다는 뜻이니까요.
        String me = createUser();

        mvc.perform(put("/api/v1/presence")
                        .header(USER_ID_HEADER, me)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"sessionId\":\"ROOM42\",\"sessionKind\":\"\"}"))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("INVALID_REQUEST"));
    }

    @Test
    @DisplayName("하트비트 갱신은 사람이 몇 명이든 UPDATE 한 문장이다")
    void refreshingIsOneStatementForAnyNumberOfUsers() throws Exception {
        // 이것이 890 의 요점입니다. 예전에는 접속자 한 명이 30초마다 요청 하나를 보내
        // 트랜잭션 하나와 UPDATE 하나를 만들었습니다. 접속자 수에 그대로 비례했습니다.
        List<Integer> everyone = new ArrayList<>();
        for (int i = 0; i < 5; i++) {
            String user = createUser();
            heartbeat(user, null);
            everyone.add(seqOf(user));
        }

        // 예전 방식을 같은 계측으로 재봅니다. 이 줄이 없으면 검사기가 아무것도 세지 못하는
        // 상태에서도 아래 두 단정이 통과할 수 있습니다 — 0 이 아니라 1 을 세는지, 그리고
        // 5 와 1 을 구분하는지가 여기서 드러납니다.
        //
        // 사람마다 다른 방을 주는 이유가 있습니다. 같은 상태를 다시 보고하면 시각이 초
        // 단위라 같은 초 안에서는 바뀌는 필드가 없고, Hibernate 가 UPDATE 를 아예 내지
        // 않습니다. 그러면 다섯 번 불러도 문장이 다섯 개가 아닙니다.
        StatementRecorder.start();
        for (int i = 0; i < everyone.size(); i++) {
            presenceService.reportBound(everyone.get(i), "ROOM" + i, SessionKind.MATCH);
        }
        List<String> oneByOne = StatementRecorder.stop();

        StatementRecorder.start();
        presenceService.refreshHeartbeats(List.of(everyone.getFirst()));
        List<String> forOne = StatementRecorder.stop();

        StatementRecorder.start();
        presenceService.refreshHeartbeats(everyone);
        List<String> forFive = StatementRecorder.stop();

        assertThat(StatementRecorder.writesOn(oneByOne, "user_presence")).hasSize(5);
        assertThat(StatementRecorder.writesOn(forOne, "user_presence")).hasSize(1);
        assertThat(StatementRecorder.writesOn(forFive, "user_presence")).hasSize(1);
    }

    @Test
    @DisplayName("갱신은 넘긴 사람만 밀고 나머지는 건드리지 않는다")
    void refreshingTouchesOnlyTheGivenUsers() throws Exception {
        String connected = createUser();
        String gone = createUser();
        heartbeat(connected, "ROOM01");
        heartbeat(gone, "ROOM02");
        makeHeartbeatStale(connected);
        makeHeartbeatStale(gone);

        presenceService.refreshHeartbeats(List.of(seqOf(connected)));

        // 붙어 있는 쪽은 되살아나고, 끊긴 쪽은 낡은 채로 남아 스윕이 데려갑니다.
        String threshold = Timestamps.format(Instant.now().minusSeconds(90));
        assertThat((String) presenceOf(connected).get("heartbeat_at")).isGreaterThan(threshold);
        assertThat((String) presenceOf(gone).get("heartbeat_at")).isLessThan(threshold);
    }

    @Test
    @DisplayName("갱신은 상태를 건드리지 않는다")
    void refreshingLeavesTheStatusAlone() throws Exception {
        String me = createUser();
        heartbeat(me, "ROOM42", "LOBBY");
        makeUpdatedAtOld(me);
        String updatedAt = (String) presenceOf(me).get("updated_at");

        presenceService.refreshHeartbeats(List.of(seqOf(me)));

        // 접속·종료·방 이동만 상태를 바꿉니다. 갱신이 상태까지 손대면 방금 오프라인이 된
        // 사람을 한 틱 늦게 되살리는 경합이 생깁니다.
        Map<String, Object> row = presenceOf(me);
        assertThat(row.get("status")).isEqualTo("IN_LOBBY");
        assertThat(row.get("session_id")).isEqualTo("ROOM42");
        assertThat(row.get("updated_at")).isEqualTo(updatedAt);
    }

    @Test
    @DisplayName("붙어 있는 사람이 없으면 갱신은 아무 문장도 내지 않는다")
    void refreshingNobodyIssuesNoStatement() {
        // IN () 는 MySQL 문법 오류입니다. 호출부가 빈 목록을 걸러내는지 봅니다.
        StatementRecorder.start();
        int refreshed = presenceService.refreshHeartbeats(List.of());
        List<String> statements = StatementRecorder.stop();

        assertThat(refreshed).isZero();
        assertThat(StatementRecorder.writesOn(statements, "user_presence")).isEmpty();
    }

    @Test
    @DisplayName("스윕이 로비에 있던 행도 오프라인으로 내린다")
    void sweepMarksStaleLobbyRowsOffline() throws Exception {
        // 스윕 조건이 status IN (...) 목록이라 PresenceStatus 에 값을 더하면 그 목록에도
        // 넣어야 합니다. 빠뜨리면 이 사람은 하트비트가 끊겨도 영원히 스윕되지 않고,
        // 조회는 PresenceTimeout 이 계산해 맞게 나오므로 눈에 띄지도 않습니다.
        String stale = createUser();
        heartbeat(stale, "ROOM42", "LOBBY");
        makeHeartbeatStale(stale);

        presenceSweeper.sweep();

        Map<String, Object> row = presenceOf(stale);
        assertThat(row.get("status")).isEqualTo("OFFLINE");
        assertThat(row.get("session_id")).isNull();
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

    /** sessionKind 를 빼고 보냅니다. 서버는 없는 값을 MATCH 로 읽습니다. */
    private void heartbeat(String userId, String sessionId) throws Exception {
        perform(userId, sessionId == null ? "{}" : "{\"sessionId\":\"" + sessionId + "\"}");
    }

    private void heartbeat(String userId, String sessionId, String sessionKind) throws Exception {
        perform(userId, "{\"sessionId\":\"" + sessionId + "\",\"sessionKind\":\"" + sessionKind + "\"}");
    }

    private void perform(String userId, String body) throws Exception {
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

    private Integer seqOf(String userId) {
        return jdbcTemplate.queryForObject("SELECT users_seq FROM users WHERE public_id = ?",
                Integer.class, userId);
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
