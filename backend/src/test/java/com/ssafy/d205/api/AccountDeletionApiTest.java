package com.ssafy.d205.api;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.ResultActions;
import tools.jackson.databind.ObjectMapper;

import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.patch;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.ssafy.d205.support.IntegrationTest;

class AccountDeletionApiTest extends IntegrationTest {

    private static final String USER_ID_HEADER = "X-User-Id";
    private static final String DEVICE_ID_HEADER = "X-Device-Id";

    @Autowired
    MockMvc mvc;

    @Autowired
    ObjectMapper objectMapper;

    @Autowired
    JdbcTemplate jdbcTemplate;

    @Test
    @DisplayName("삭제하면 계정 조회가 404")
    void deletedAccountIsGone() throws Exception {
        String deviceId = newDeviceId();
        String me = createUser(deviceId);

        deleteAccount(me, deviceId).andExpect(status().isNoContent());

        mvc.perform(get("/api/v1/accounts/me").header(USER_ID_HEADER, me))
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.code").value("ACCOUNT_NOT_FOUND"));
    }

    @Test
    @DisplayName("삭제하면 친구의 목록에서 사라진다")
    void deletedAccountLeavesFriendList() throws Exception {
        String deviceId = newDeviceId();
        String me = createUser(deviceId);
        String friend = createUser(newDeviceId());
        befriend(me, friend);

        deleteAccount(me, deviceId).andExpect(status().isNoContent());

        mvc.perform(get("/api/v1/friends").header(USER_ID_HEADER, friend))
                .andExpect(jsonPath("$.friends.length()").value(0));
    }

    @Test
    @DisplayName("CASCADE가 신원, 접속 상태, 친구 관계, 차단을 모두 지운다")
    void cascadeRemovesEverything() throws Exception {
        // 스키마를 믿지 않고 확인합니다. ON DELETE CASCADE 가 네 테이블에 걸려 있는데
        // 지금까지 users 를 지우는 코드가 없어서 한 번도 동작한 적이 없었습니다.
        String deviceId = newDeviceId();
        String me = createUser(deviceId);
        String friend = createUser(newDeviceId());
        String blockedByMe = createUser(newDeviceId());
        String blockedMe = createUser(newDeviceId());

        befriend(me, friend);
        heartbeat(me);
        block(me, blockedByMe);
        block(blockedMe, me);

        int seq = seqOf(me);
        assertThat(count("user_identities", seq)).isOne();
        assertThat(count("user_presence", seq)).isOne();
        assertThat(friendshipCount(seq)).isOne();
        assertThat(blockCount(seq)).isEqualTo(2);

        deleteAccount(me, deviceId).andExpect(status().isNoContent());

        assertThat(count("user_identities", seq)).isZero();
        assertThat(count("user_presence", seq)).isZero();
        assertThat(friendshipCount(seq)).isZero();
        // 내가 차단한 것과 나를 차단한 것 둘 다 사라져야 합니다. FK 가 양쪽에 걸려 있습니다.
        assertThat(blockCount(seq)).isZero();
    }

    @Test
    @DisplayName("내가 보낸 대기 요청도 함께 사라진다")
    void pendingRequestISentIsRemoved() throws Exception {
        // friendships 는 FK 가 셋(low, high, requested_by)입니다. 내가 요청한 관계도
        // 지워지는지 따로 확인합니다.
        String deviceId = newDeviceId();
        String me = createUser(deviceId);
        String other = createUser(newDeviceId());
        sendRequest(me, other).andExpect(status().isCreated());

        int seq = seqOf(me);
        assertThat(friendshipCount(seq)).isOne();

        deleteAccount(me, deviceId).andExpect(status().isNoContent());

        assertThat(friendshipCount(seq)).isZero();
        mvc.perform(get("/api/v1/friend-requests").header(USER_ID_HEADER, other))
                .andExpect(jsonPath("$.requests.length()").value(0));
    }

    @Test
    @DisplayName("나를 차단한 사람의 차단 목록에서도 사라진다")
    void disappearsFromOthersBlockList() throws Exception {
        String deviceId = newDeviceId();
        String me = createUser(deviceId);
        String blocker = createUser(newDeviceId());
        block(blocker, me);

        mvc.perform(get("/api/v1/blocks").header(USER_ID_HEADER, blocker))
                .andExpect(jsonPath("$.blocked.length()").value(1));

        deleteAccount(me, deviceId).andExpect(status().isNoContent());

        mvc.perform(get("/api/v1/blocks").header(USER_ID_HEADER, blocker))
                .andExpect(jsonPath("$.blocked.length()").value(0));
    }

    @Test
    @DisplayName("삭제한 계정의 닉네임을 다시 쓸 수 있다")
    void nicknameBecomesAvailableAgain() throws Exception {
        // uk_users_nickname 이 풀립니다. 소프트 삭제였다면 행이 남아 재사용이 막힙니다.
        String nickname = uniqueNickname();
        String deviceId = newDeviceId();
        String me = createUser(deviceId);
        rename(me, nickname).andExpect(status().isOk());

        deleteAccount(me, deviceId).andExpect(status().isNoContent());

        String other = createUser(newDeviceId());
        rename(other, nickname).andExpect(status().isOk());
    }

    @Test
    @DisplayName("같은 기기로 다시 발급하면 새 계정이 생긴다")
    void sameDeviceGetsNewAccount() throws Exception {
        // user_identities 행이 CASCADE 로 지워졌으므로 그 기기는 처음 보는 기기가 됩니다.
        String deviceId = newDeviceId();
        String before = createUser(deviceId);

        deleteAccount(before, deviceId).andExpect(status().isNoContent());

        String after = issueRequest(deviceId)
                .andExpect(status().isCreated())
                .andReturn().getResponse().getContentAsString();
        assertThat(objectMapper.readTree(after).get("userId").asText()).isNotEqualTo(before);
    }

    @Test
    @DisplayName("남의 기기 식별자로는 삭제되지 않는다")
    void cannotDeleteWithSomeoneElsesDeviceId() throws Exception {
        // X-User-Id 는 공개 식별자라 남의 id 를 아는 사람이 부를 수 있습니다. 실제
        // 자격증명인 기기 식별자를 함께 확인하는 이유입니다.
        String myDeviceId = newDeviceId();
        String me = createUser(myDeviceId);
        String othersDeviceId = newDeviceId();
        createUser(othersDeviceId);

        deleteAccount(me, othersDeviceId)
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.code").value("ACCOUNT_NOT_FOUND"));

        // 계정이 살아 있어야 합니다.
        mvc.perform(get("/api/v1/accounts/me").header(USER_ID_HEADER, me))
                .andExpect(status().isOk());
    }

    @Test
    @DisplayName("없는 기기 식별자로는 삭제되지 않는다")
    void cannotDeleteWithUnknownDeviceId() throws Exception {
        String deviceId = newDeviceId();
        String me = createUser(deviceId);

        deleteAccount(me, newDeviceId())
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.code").value("ACCOUNT_NOT_FOUND"));

        mvc.perform(get("/api/v1/accounts/me").header(USER_ID_HEADER, me))
                .andExpect(status().isOk());
    }

    @Test
    @DisplayName("X-Device-Id 헤더가 없으면 400")
    void missingDeviceHeaderIsBadRequest() throws Exception {
        String me = createUser(newDeviceId());

        mvc.perform(delete("/api/v1/accounts/me").header(USER_ID_HEADER, me))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("MISSING_HEADER"));
    }

    @Test
    @DisplayName("없는 계정으로 부르면 404")
    void unknownCallerIsNotFound() throws Exception {
        deleteAccount(UUID.randomUUID().toString(), newDeviceId())
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.code").value("ACCOUNT_NOT_FOUND"));
    }

    private static String newDeviceId() {
        return UUID.randomUUID().toString();
    }

    private static String uniqueNickname() {
        return "Del" + UUID.randomUUID().toString().replace("-", "").substring(0, 8);
    }

    private ResultActions issueRequest(String deviceId) throws Exception {
        return mvc.perform(post("/api/v1/accounts")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"deviceId\":\"" + deviceId + "\"}"));
    }

    private String createUser(String deviceId) throws Exception {
        String body = issueRequest(deviceId)
                .andExpect(status().isCreated())
                .andReturn().getResponse().getContentAsString();
        return objectMapper.readTree(body).get("userId").asText();
    }

    private ResultActions rename(String userId, String nickname) throws Exception {
        return mvc.perform(patch("/api/v1/accounts/me")
                .header(USER_ID_HEADER, userId)
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"nickname\":\"" + nickname + "\"}"));
    }

    private ResultActions deleteAccount(String userId, String deviceId) throws Exception {
        return mvc.perform(delete("/api/v1/accounts/me")
                .header(USER_ID_HEADER, userId)
                .header(DEVICE_ID_HEADER, deviceId));
    }

    private ResultActions sendRequest(String from, String to) throws Exception {
        return mvc.perform(post("/api/v1/friend-requests")
                .header(USER_ID_HEADER, from)
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"userId\":\"" + to + "\"}"));
    }

    private void befriend(String a, String b) throws Exception {
        sendRequest(a, b).andExpect(status().isCreated());
        mvc.perform(post("/api/v1/friend-requests/{userId}/accept", a).header(USER_ID_HEADER, b))
                .andExpect(status().isNoContent());
    }

    private void block(String blocker, String blocked) throws Exception {
        mvc.perform(put("/api/v1/blocks/{userId}", blocked).header(USER_ID_HEADER, blocker))
                .andExpect(status().isNoContent());
    }

    private void heartbeat(String userId) throws Exception {
        mvc.perform(put("/api/v1/presence")
                        .header(USER_ID_HEADER, userId)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{}"))
                .andExpect(status().isNoContent());
    }

    /** 삭제 전에 내부 seq 를 잡아둡니다. 삭제되면 public_id 로는 찾을 수 없습니다. */
    private int seqOf(String userId) {
        return jdbcTemplate.queryForObject(
                "SELECT users_seq FROM users WHERE public_id = ?", Integer.class, userId);
    }

    private int count(String table, int userSeq) {
        return jdbcTemplate.queryForObject(
                "SELECT COUNT(*) FROM " + table + " WHERE user_seq = ?", Integer.class, userSeq);
    }

    /** low, high, requested_by 세 FK 중 어디에 걸려 있어도 셉니다. */
    private int friendshipCount(int userSeq) {
        return jdbcTemplate.queryForObject("""
                SELECT COUNT(*) FROM friendships
                 WHERE user_low_seq = ? OR user_high_seq = ? OR requested_by_seq = ?
                """, Integer.class, userSeq, userSeq, userSeq);
    }

    private int blockCount(int userSeq) {
        return jdbcTemplate.queryForObject("""
                SELECT COUNT(*) FROM user_blocks
                 WHERE blocker_seq = ? OR blocked_seq = ?
                """, Integer.class, userSeq, userSeq);
    }
}
