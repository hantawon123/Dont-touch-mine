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

import com.ssafy.d205.domain.invite.service.InviteSweeper;
import com.ssafy.d205.global.common.Timestamps;
import com.ssafy.d205.support.IntegrationTest;

class InviteApiTest extends IntegrationTest {

    private static final String USER_ID_HEADER = "X-User-Id";
    private static final String ROOM = "7K2M9P";

    @Autowired
    MockMvc mvc;

    @Autowired
    ObjectMapper objectMapper;

    @Autowired
    JdbcTemplate jdbcTemplate;

    @Autowired
    InviteSweeper sweeper;

    @Test
    @DisplayName("친구를 부르면 상대의 받은 초대에 방 코드와 함께 나타난다")
    void inviteReachesTheFriend() throws Exception {
        String host = createUser();
        String guest = createUser();
        befriend(host, guest);

        invite(host, guest, ROOM);

        mvc.perform(get("/api/v1/invites").header(USER_ID_HEADER, guest))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.invites.length()").value(1))
                .andExpect(jsonPath("$.invites[0].userId").value(host))
                .andExpect(jsonPath("$.invites[0].nickname").isNotEmpty())
                .andExpect(jsonPath("$.invites[0].roomCode").value(ROOM))
                .andExpect(jsonPath("$.invites[0].invitedAt").isNotEmpty());
    }

    @Test
    @DisplayName("부른 사람에게는 자기 초대가 보이지 않는다")
    void theInviterDoesNotSeeTheirOwnInvite() throws Exception {
        String host = createUser();
        String guest = createUser();
        befriend(host, guest);
        invite(host, guest, ROOM);

        // 받은 초대만 주는 목록입니다. 보낸 것까지 섞이면 화면이 자기 초대를 눌러
        // 자기 방에 들어가려는 상태가 됩니다.
        mvc.perform(get("/api/v1/invites").header(USER_ID_HEADER, host))
                .andExpect(jsonPath("$.invites").isEmpty());
    }

    @Test
    @DisplayName("같은 방으로 다시 부르면 초대가 늘지 않고 시각만 새로 간다")
    void invitingAgainRenewsInsteadOfDuplicating() throws Exception {
        String host = createUser();
        String guest = createUser();
        befriend(host, guest);

        invite(host, guest, ROOM);
        makeInviteOld(guest, 100);
        String before = inviteRowTime(guest);

        invite(host, guest, ROOM);

        mvc.perform(get("/api/v1/invites").header(USER_ID_HEADER, guest))
                .andExpect(jsonPath("$.invites.length()").value(1));

        // 두 번 눌렀다고 초대가 둘이 되지는 않고, 만료 시계가 처음부터 다시 갑니다.
        assertThat(inviteRowTime(guest)).isGreaterThan(before);
    }

    @Test
    @DisplayName("다른 방으로 부르면 초대가 따로 생긴다")
    void adifferentRoomIsADifferentInvite() throws Exception {
        String host = createUser();
        String guest = createUser();
        befriend(host, guest);

        invite(host, guest, ROOM);
        invite(host, guest, "3XQ4TZ");

        mvc.perform(get("/api/v1/invites").header(USER_ID_HEADER, guest))
                .andExpect(jsonPath("$.invites.length()").value(2));
    }

    @Test
    @DisplayName("3분이 지난 초대는 스윕을 기다리지 않고 목록에서 빠진다")
    void expiredInvitesAreFilteredOnRead() throws Exception {
        String host = createUser();
        String guest = createUser();
        befriend(host, guest);
        invite(host, guest, ROOM);

        makeInviteOld(guest, 200);

        // 스윕은 주기로 도는데 그 사이에 만료된 초대를 보여주면 없는 방으로 들어가려는
        // 시도가 됩니다. 조회가 직접 판정해야 하는 이유입니다.
        mvc.perform(get("/api/v1/invites").header(USER_ID_HEADER, guest))
                .andExpect(jsonPath("$.invites").isEmpty());

        // 행은 아직 남아 있습니다. 응답의 정확성이 스윕에 기대지 않는다는 뜻입니다.
        assertThat(inviteRows(guest)).isEqualTo(1);
    }

    @Test
    @DisplayName("스윕이 만료된 초대를 지운다")
    void sweepDeletesExpiredInvites() throws Exception {
        String host = createUser();
        String guest = createUser();
        befriend(host, guest);
        invite(host, guest, ROOM);
        makeInviteOld(guest, 200);

        sweeper.sweep();

        assertThat(inviteRows(guest)).isZero();
    }

    @Test
    @DisplayName("스윕이 살아있는 초대는 건드리지 않는다")
    void sweepLeavesLiveInvitesAlone() throws Exception {
        String host = createUser();
        String guest = createUser();
        befriend(host, guest);
        invite(host, guest, ROOM);

        sweeper.sweep();

        assertThat(inviteRows(guest)).isEqualTo(1);
    }

    @Test
    @DisplayName("거절하면 목록에서 사라진다")
    void decliningRemovesTheInvite() throws Exception {
        String host = createUser();
        String guest = createUser();
        befriend(host, guest);
        invite(host, guest, ROOM);

        mvc.perform(delete("/api/v1/invites/{userId}", host).header(USER_ID_HEADER, guest))
                .andExpect(status().isNoContent());

        mvc.perform(get("/api/v1/invites").header(USER_ID_HEADER, guest))
                .andExpect(jsonPath("$.invites").isEmpty());
    }

    @Test
    @DisplayName("없는 초대를 거절해도 성공이다")
    void decliningNothingSucceeds() throws Exception {
        String host = createUser();
        String guest = createUser();
        befriend(host, guest);

        // DELETE 는 멱등해야 합니다. 만료돼 사라진 초대를 거절하는 것도 같은 경우이고,
        // 원하는 결과인 "그 초대가 없는 상태"가 이미 달성돼 있습니다.
        mvc.perform(delete("/api/v1/invites/{userId}", host).header(USER_ID_HEADER, guest))
                .andExpect(status().isNoContent());
    }

    @Test
    @DisplayName("친구가 아니면 부를 수 없다")
    void strangersCannotBeInvited() throws Exception {
        String host = createUser();
        String stranger = createUser();

        mvc.perform(post("/api/v1/invites")
                        .header(USER_ID_HEADER, host)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(body(stranger, ROOM)))
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.code").value("NOT_FRIENDS"));
    }

    @Test
    @DisplayName("요청만 보내둔 상대는 아직 친구가 아니다")
    void aPendingRequestIsNotAFriendship() throws Exception {
        String host = createUser();
        String guest = createUser();
        sendRequest(host, guest);

        mvc.perform(post("/api/v1/invites")
                        .header(USER_ID_HEADER, host)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(body(guest, ROOM)))
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.code").value("NOT_FRIENDS"));
    }

    @Test
    @DisplayName("차단이 걸린 상대는 없는 사람과 같게 답한다")
    void blockingHidesTheTarget() throws Exception {
        String host = createUser();
        String guest = createUser();
        befriend(host, guest);

        mvc.perform(put("/api/v1/blocks/{userId}", host).header(USER_ID_HEADER, guest))
                .andExpect(status().isNoContent());

        // 차단당했다는 사실이 드러나면 안 됩니다. 계정이 없는 경우와 구분되지 않습니다.
        mvc.perform(post("/api/v1/invites")
                        .header(USER_ID_HEADER, host)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(body(guest, ROOM)))
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.code").value("TARGET_NOT_FOUND"));
    }

    @Test
    @DisplayName("차단하면 이미 보낸 초대도 함께 사라진다")
    void blockingRemovesInvitesAlreadySent() throws Exception {
        String host = createUser();
        String guest = createUser();
        befriend(host, guest);
        invite(host, guest, ROOM);

        mvc.perform(put("/api/v1/blocks/{userId}", guest).header(USER_ID_HEADER, host))
                .andExpect(status().isNoContent());

        // 남겨두면 차단이 뚫립니다. 관계가 끊긴 뒤에도 이미 받아둔 초대로 상대의 방에
        // 들어갈 수 있고, 차단은 상대가 나를 찾지도 닿지도 못하게 하는 것입니다.
        mvc.perform(get("/api/v1/invites").header(USER_ID_HEADER, guest))
                .andExpect(jsonPath("$.invites").isEmpty());
        assertThat(inviteRows(guest)).isZero();
    }

    @Test
    @DisplayName("차단당한 쪽이 보낸 초대도 사라진다")
    void blockingRemovesInvitesInBothDirections() throws Exception {
        String host = createUser();
        String guest = createUser();
        befriend(host, guest);
        invite(guest, host, ROOM);

        // 차단은 양방향으로 적용되므로 초대도 양쪽이 없어져야 합니다.
        mvc.perform(put("/api/v1/blocks/{userId}", guest).header(USER_ID_HEADER, host))
                .andExpect(status().isNoContent());

        mvc.perform(get("/api/v1/invites").header(USER_ID_HEADER, host))
                .andExpect(jsonPath("$.invites").isEmpty());
    }

    @Test
    @DisplayName("자기 자신은 부를 수 없다")
    void nobodyInvitesThemselves() throws Exception {
        String me = createUser();

        mvc.perform(post("/api/v1/invites")
                        .header(USER_ID_HEADER, me)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(body(me, ROOM)))
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.code").value("TARGET_NOT_FOUND"));
    }

    @Test
    @DisplayName("헷갈리는 글자가 든 방 코드는 거절한다")
    void confusableLettersAreRejected() throws Exception {
        String host = createUser();
        String guest = createUser();
        befriend(host, guest);

        // I O U L 은 클라이언트의 알파벳에 없습니다. 1 과 I, 0 과 O 를 눈으로 구분하기
        // 어려워서 뺀 글자들이라, 서버가 받아주면 만들어질 수 없는 코드가 저장됩니다.
        for (String bad : new String[] { "7K2M9I", "7K2M9O", "7K2M9U", "7K2M9L" }) {
            mvc.perform(post("/api/v1/invites")
                            .header(USER_ID_HEADER, host)
                            .contentType(MediaType.APPLICATION_JSON)
                            .content(body(guest, bad)))
                    .andExpect(status().isBadRequest())
                    .andExpect(jsonPath("$.code").value("INVALID_REQUEST"));
        }
    }

    @Test
    @DisplayName("길이가 다르거나 소문자인 방 코드는 거절한다")
    void malformedRoomCodesAreRejected() throws Exception {
        String host = createUser();
        String guest = createUser();
        befriend(host, guest);

        for (String bad : new String[] { "7K2M9", "7K2M9PQ", "7k2m9p", "" }) {
            mvc.perform(post("/api/v1/invites")
                            .header(USER_ID_HEADER, host)
                            .contentType(MediaType.APPLICATION_JSON)
                            .content(body(guest, bad)))
                    .andExpect(status().isBadRequest())
                    .andExpect(jsonPath("$.code").value("INVALID_REQUEST"));
        }
    }

    @Test
    @DisplayName("계정이 사라지면 그 사람이 주고받은 초대도 사라진다")
    void deletingAnAccountTakesItsInvitesWithIt() throws Exception {
        String hostDevice = UUID.randomUUID().toString();
        String host = createUser(hostDevice);
        String guest = createUser();
        befriend(host, guest);
        invite(host, guest, ROOM);

        mvc.perform(delete("/api/v1/accounts/me")
                        .header(USER_ID_HEADER, host)
                        .header("X-Device-Id", hostDevice))
                .andExpect(status().isNoContent());

        // 탈퇴가 흔적을 남기지 않는다는 약속의 일부입니다. FK 의 CASCADE 가 지웁니다.
        mvc.perform(get("/api/v1/invites").header(USER_ID_HEADER, guest))
                .andExpect(jsonPath("$.invites").isEmpty());
    }

    private String body(String userId, String roomCode) {
        return "{\"userId\":\"" + userId + "\",\"roomCode\":\"" + roomCode + "\"}";
    }

    private void invite(String from, String to, String roomCode) throws Exception {
        mvc.perform(post("/api/v1/invites")
                        .header(USER_ID_HEADER, from)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(body(to, roomCode)))
                .andExpect(status().isCreated());
    }

    private String createUser() throws Exception {
        return createUser(UUID.randomUUID().toString());
    }

    private String createUser(String deviceId) throws Exception {
        String body = mvc.perform(post("/api/v1/accounts")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"deviceId\":\"" + deviceId + "\"}"))
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

    /** 만료(3분)를 넘기도록 초대를 과거로 밉니다. */
    private void makeInviteOld(String inviteeUserId, int secondsAgo) {
        jdbcTemplate.update("""
                UPDATE room_invites i
                  JOIN users u ON u.users_seq = i.invitee_seq
                   SET i.created_at = ?
                 WHERE u.public_id = ?
                """, Timestamps.format(Instant.now().minusSeconds(secondsAgo)), inviteeUserId);
    }

    private int inviteRows(String inviteeUserId) {
        Integer count = jdbcTemplate.queryForObject("""
                SELECT COUNT(*)
                  FROM room_invites i
                  JOIN users u ON u.users_seq = i.invitee_seq
                 WHERE u.public_id = ?
                """, Integer.class, inviteeUserId);
        return count == null ? 0 : count;
    }

    private String inviteRowTime(String inviteeUserId) {
        return jdbcTemplate.queryForObject("""
                SELECT i.created_at
                  FROM room_invites i
                  JOIN users u ON u.users_seq = i.invitee_seq
                 WHERE u.public_id = ?
                """, String.class, inviteeUserId);
    }
}
