package com.ssafy.d205.api;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.test.web.servlet.MockMvc;
import tools.jackson.databind.ObjectMapper;

import java.time.Instant;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.ssafy.d205.global.common.Timestamps;
import com.ssafy.d205.support.IntegrationTest;

class FriendListApiTest extends IntegrationTest {

    private static final String USER_ID_HEADER = "X-User-Id";

    @Autowired
    MockMvc mvc;

    @Autowired
    ObjectMapper objectMapper;

    @Autowired
    JdbcTemplate jdbcTemplate;

    @Test
    @DisplayName("수락한 친구가 양쪽 목록에 서로 나타난다")
    void friendsAppearOnBothSides() throws Exception {
        // S15P21D205-431 에서는 목록 API가 없어 간접 확인만 했던 것입니다.
        String me = createUser();
        String other = createUser();
        befriend(me, other);

        mvc.perform(get("/api/v1/friends").header(USER_ID_HEADER, me))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.friends.length()").value(1))
                .andExpect(jsonPath("$.friends[0].userId").value(other))
                .andExpect(jsonPath("$.friends[0].nickname").isNotEmpty());

        mvc.perform(get("/api/v1/friends").header(USER_ID_HEADER, other))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.friends.length()").value(1))
                .andExpect(jsonPath("$.friends[0].userId").value(me));
    }

    @Test
    @DisplayName("접속 기록이 없는 친구는 OFFLINE")
    void friendWithoutPresenceIsOffline() throws Exception {
        // user_presence 에 행이 없는 경우입니다. LEFT JOIN 이라 목록에는 남아야 합니다.
        // INNER JOIN 이면 이 친구가 목록에서 사라집니다.
        String me = createUser();
        String other = createUser();
        befriend(me, other);

        mvc.perform(get("/api/v1/friends").header(USER_ID_HEADER, me))
                .andExpect(jsonPath("$.friends[0].presence").value("OFFLINE"));
    }

    @Test
    @DisplayName("하트비트를 보낸 친구는 ONLINE")
    void friendWithHeartbeatIsOnline() throws Exception {
        String me = createUser();
        String other = createUser();
        befriend(me, other);

        heartbeat(other, null);

        mvc.perform(get("/api/v1/friends").header(USER_ID_HEADER, me))
                .andExpect(jsonPath("$.friends[0].presence").value("ONLINE"));
    }

    @Test
    @DisplayName("sessionId를 보낸 친구는 IN_GAME")
    void friendInSessionIsInGame() throws Exception {
        String me = createUser();
        String other = createUser();
        befriend(me, other);

        heartbeat(other, "ROOM01");

        mvc.perform(get("/api/v1/friends").header(USER_ID_HEADER, me))
                .andExpect(jsonPath("$.friends[0].presence").value("IN_GAME"));
    }

    @Test
    @DisplayName("하트비트가 끊긴 친구는 저장된 값이 ONLINE이어도 OFFLINE으로 보인다")
    void staleHeartbeatShowsOffline() throws Exception {
        // 크래시로 죽은 클라이언트입니다. 종료 신호를 못 보내서 status 는 ONLINE 으로
        // 남아 있지만 실제로는 끊긴 상태입니다. 스윕을 기다리지 않고 조회 시점에
        // 판정되는지 확인합니다.
        String me = createUser();
        String other = createUser();
        befriend(me, other);
        heartbeat(other, "ROOM01");

        makeHeartbeatStale(other);

        mvc.perform(get("/api/v1/friends").header(USER_ID_HEADER, me))
                .andExpect(jsonPath("$.friends[0].presence").value("OFFLINE"));

        // 저장된 값은 아직 IN_GAME 입니다. 판정이 조회 시점에 일어난다는 증거입니다.
        String stored = jdbcTemplate.queryForObject("""
                SELECT p.status FROM user_presence p
                  JOIN users u ON u.users_seq = p.user_seq
                 WHERE u.public_id = ?
                """, String.class, other);
        assertThat(stored).isEqualTo("IN_GAME");
    }

    @Test
    @DisplayName("대기 중인 요청은 친구 목록에 없다")
    void pendingRequestIsNotAFriend() throws Exception {
        String me = createUser();
        String other = createUser();
        sendRequest(me, other);

        mvc.perform(get("/api/v1/friends").header(USER_ID_HEADER, me))
                .andExpect(jsonPath("$.friends.length()").value(0));
    }

    @Test
    @DisplayName("친구를 끊으면 목록에서 사라진다")
    void unfriendRemovesFromList() throws Exception {
        String me = createUser();
        String other = createUser();
        befriend(me, other);

        mvc.perform(delete("/api/v1/friends/{userId}", other).header(USER_ID_HEADER, me))
                .andExpect(status().isNoContent());

        mvc.perform(get("/api/v1/friends").header(USER_ID_HEADER, me))
                .andExpect(jsonPath("$.friends.length()").value(0));
        mvc.perform(get("/api/v1/friends").header(USER_ID_HEADER, other))
                .andExpect(jsonPath("$.friends.length()").value(0));
    }

    @Test
    @DisplayName("친구가 없으면 빈 배열")
    void noFriendsIsEmptyArray() throws Exception {
        mvc.perform(get("/api/v1/friends").header(USER_ID_HEADER, createUser()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.friends").isArray())
                .andExpect(jsonPath("$.friends.length()").value(0));
    }

    @Test
    @DisplayName("X-User-Id 헤더가 없으면 400")
    void missingHeaderIsBadRequest() throws Exception {
        mvc.perform(get("/api/v1/friends"))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("MISSING_HEADER"));
    }

    @Test
    @DisplayName("없는 계정으로 부르면 404")
    void unknownCallerIsNotFound() throws Exception {
        mvc.perform(get("/api/v1/friends").header(USER_ID_HEADER, UUID.randomUUID().toString()))
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

    private void sendRequest(String from, String to) throws Exception {
        mvc.perform(post("/api/v1/friend-requests")
                        .header(USER_ID_HEADER, from)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"userId\":\"" + to + "\"}"))
                .andExpect(status().isCreated());
    }

    private void befriend(String a, String b) throws Exception {
        sendRequest(a, b);
        mvc.perform(post("/api/v1/friend-requests/{userId}/accept", a).header(USER_ID_HEADER, b))
                .andExpect(status().isNoContent());
    }

    private void heartbeat(String userId, String sessionId) throws Exception {
        String body = sessionId == null ? "{}" : "{\"sessionId\":\"" + sessionId + "\"}";
        mvc.perform(put("/api/v1/presence")
                        .header(USER_ID_HEADER, userId)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(body))
                .andExpect(status().isNoContent());
    }

    /** 타임아웃(90초)보다 오래된 하트비트로 바꿔 크래시 상황을 만듭니다. */
    private void makeHeartbeatStale(String userId) {
        jdbcTemplate.update("""
                UPDATE user_presence p
                  JOIN users u ON u.users_seq = p.user_seq
                   SET p.heartbeat_at = ?
                 WHERE u.public_id = ?
                """, Timestamps.format(Instant.now().minusSeconds(200)), userId);
    }
}
