package com.ssafy.d205.api;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.test.web.servlet.MockMvc;
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

class BlockApiTest extends IntegrationTest {

    private static final String USER_ID_HEADER = "X-User-Id";

    @Autowired
    MockMvc mvc;

    @Autowired
    ObjectMapper objectMapper;

    @Autowired
    JdbcTemplate jdbcTemplate;

    @Test
    @DisplayName("차단하면 상대가 검색 결과에서 사라진다")
    void blockedUserDisappearsFromSearch() throws Exception {
        String prefix = newPrefix();
        String me = createUser();
        String other = createUserNamed(prefix + "AA");

        search(me, prefix).andExpect(jsonPath("$.users.length()").value(1));

        block(me, other);

        search(me, prefix).andExpect(jsonPath("$.users.length()").value(0));
    }

    @Test
    @DisplayName("차단하면 상대가 나에게 친구 요청을 보낼 수 없다")
    void blockedUserCannotSendRequest() throws Exception {
        // 차단은 양방향으로 적용됩니다. 차단당한 쪽이 계속 요청을 보낼 수 있으면
        // 차단의 의미가 없습니다.
        String me = createUser();
        String other = createUser();
        block(me, other);

        sendRequest(other, me)
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.code").value("TARGET_NOT_FOUND"));
    }

    @Test
    @DisplayName("차단하면 이미 성립된 친구 관계가 삭제된다")
    void blockDeletesExistingFriendship() throws Exception {
        // 이 이슈의 핵심입니다. 관계를 남겨두고 목록에서 가리는 방법도 있지만,
        // 남겨두면 언젠가 다른 경로로 새어 나옵니다.
        String me = createUser();
        String other = createUser();
        befriend(me, other);
        assertThat(friendshipRowCount(me, other)).isOne();

        block(me, other);

        assertThat(friendshipRowCount(me, other)).isZero();
        mvc.perform(get("/api/v1/friends").header(USER_ID_HEADER, me))
                .andExpect(jsonPath("$.friends.length()").value(0));
        mvc.perform(get("/api/v1/friends").header(USER_ID_HEADER, other))
                .andExpect(jsonPath("$.friends.length()").value(0));
    }

    @Test
    @DisplayName("차단하면 대기 중인 요청도 삭제된다")
    void blockDeletesPendingRequest() throws Exception {
        String me = createUser();
        String other = createUser();
        sendRequest(other, me).andExpect(status().isCreated());
        assertThat(friendshipRowCount(me, other)).isOne();

        block(me, other);

        assertThat(friendshipRowCount(me, other)).isZero();
        mvc.perform(get("/api/v1/friend-requests").header(USER_ID_HEADER, me))
                .andExpect(jsonPath("$.requests.length()").value(0));
    }

    @Test
    @DisplayName("차단은 멱등하다")
    void blockIsIdempotent() throws Exception {
        String me = createUser();
        String other = createUser();

        block(me, other);
        block(me, other);

        mvc.perform(get("/api/v1/blocks").header(USER_ID_HEADER, me))
                .andExpect(jsonPath("$.blocked.length()").value(1));
    }

    @Test
    @DisplayName("해제하면 다시 검색되고 요청을 보낼 수 있다")
    void unblockRestoresVisibility() throws Exception {
        String prefix = newPrefix();
        String me = createUser();
        String other = createUserNamed(prefix + "AA");
        block(me, other);

        mvc.perform(delete("/api/v1/blocks/{userId}", other).header(USER_ID_HEADER, me))
                .andExpect(status().isNoContent());

        search(me, prefix).andExpect(jsonPath("$.users.length()").value(1));
        sendRequest(other, me).andExpect(status().isCreated());
    }

    @Test
    @DisplayName("해제는 멱등하다 — 차단하지 않은 상대도 204")
    void unblockIsIdempotent() throws Exception {
        // DELETE 는 멱등해야 합니다. 원하는 결과(차단되지 않은 상태)가 이미 달성돼
        // 있으므로 404 가 아닙니다.
        String me = createUser();
        String other = createUser();

        mvc.perform(delete("/api/v1/blocks/{userId}", other).header(USER_ID_HEADER, me))
                .andExpect(status().isNoContent());
    }

    @Test
    @DisplayName("차단 목록에 상대와 차단 시각이 담긴다")
    void blockListHasDetails() throws Exception {
        String me = createUser();
        String other = createUser();
        block(me, other);

        mvc.perform(get("/api/v1/blocks").header(USER_ID_HEADER, me))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.blocked.length()").value(1))
                .andExpect(jsonPath("$.blocked[0].userId").value(other))
                .andExpect(jsonPath("$.blocked[0].nickname").isNotEmpty())
                .andExpect(jsonPath("$.blocked[0].blockedAt").isNotEmpty());
    }

    @Test
    @DisplayName("차단한 사람이 없으면 빈 배열")
    void emptyBlockListIsEmptyArray() throws Exception {
        mvc.perform(get("/api/v1/blocks").header(USER_ID_HEADER, createUser()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.blocked").isArray())
                .andExpect(jsonPath("$.blocked.length()").value(0));
    }

    @Test
    @DisplayName("내가 차단당한 것은 내 목록에 나오지 않는다")
    void beingBlockedIsNotInMyList() throws Exception {
        // 누가 나를 차단했는지 알려주면 차단이 조용히 적용된다는 성질이 깨집니다.
        String me = createUser();
        String other = createUser();
        block(other, me);

        mvc.perform(get("/api/v1/blocks").header(USER_ID_HEADER, me))
                .andExpect(jsonPath("$.blocked.length()").value(0));
    }

    @Test
    @DisplayName("차단은 방향별로 별개 행이다")
    void blocksAreDirectional() throws Exception {
        String a = createUser();
        String b = createUser();

        block(a, b);
        block(b, a);

        mvc.perform(get("/api/v1/blocks").header(USER_ID_HEADER, a))
                .andExpect(jsonPath("$.blocked.length()").value(1));
        mvc.perform(get("/api/v1/blocks").header(USER_ID_HEADER, b))
                .andExpect(jsonPath("$.blocked.length()").value(1));

        // a 가 해제해도 b 의 차단은 남습니다.
        mvc.perform(delete("/api/v1/blocks/{userId}", b).header(USER_ID_HEADER, a))
                .andExpect(status().isNoContent());
        mvc.perform(get("/api/v1/blocks").header(USER_ID_HEADER, b))
                .andExpect(jsonPath("$.blocked.length()").value(1));
    }

    @Test
    @DisplayName("자기 자신을 차단하면 400")
    void selfBlockIsBadRequest() throws Exception {
        String me = createUser();

        mvc.perform(put("/api/v1/blocks/{userId}", me).header(USER_ID_HEADER, me))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("SELF_BLOCK"));
    }

    @Test
    @DisplayName("없는 상대를 차단하면 404")
    void blockingUnknownTargetIsNotFound() throws Exception {
        mvc.perform(put("/api/v1/blocks/{userId}", UUID.randomUUID().toString())
                        .header(USER_ID_HEADER, createUser()))
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.code").value("TARGET_NOT_FOUND"));
    }

    @Test
    @DisplayName("X-User-Id 헤더가 없으면 400")
    void missingHeaderIsBadRequest() throws Exception {
        mvc.perform(get("/api/v1/blocks"))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("MISSING_HEADER"));
    }

    @Test
    @DisplayName("없는 계정으로 부르면 404 ACCOUNT_NOT_FOUND")
    void unknownCallerIsNotFound() throws Exception {
        mvc.perform(get("/api/v1/blocks").header(USER_ID_HEADER, UUID.randomUUID().toString()))
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.code").value("ACCOUNT_NOT_FOUND"));
    }

    private static String newPrefix() {
        return "B" + UUID.randomUUID().toString().replace("-", "").substring(0, 5);
    }

    private String createUser() throws Exception {
        String body = mvc.perform(post("/api/v1/accounts")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"deviceId\":\"" + UUID.randomUUID() + "\"}"))
                .andExpect(status().isCreated())
                .andReturn().getResponse().getContentAsString();
        return objectMapper.readTree(body).get("userId").asText();
    }

    private String createUserNamed(String nickname) throws Exception {
        String userId = createUser();
        mvc.perform(patch("/api/v1/accounts/me")
                        .header(USER_ID_HEADER, userId)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"nickname\":\"" + nickname + "\"}"))
                .andExpect(status().isOk());
        return userId;
    }

    private void block(String blocker, String blocked) throws Exception {
        mvc.perform(put("/api/v1/blocks/{userId}", blocked).header(USER_ID_HEADER, blocker))
                .andExpect(status().isNoContent());
    }

    private org.springframework.test.web.servlet.ResultActions sendRequest(String from, String to)
            throws Exception {
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

    private org.springframework.test.web.servlet.ResultActions search(String caller, String nickname)
            throws Exception {
        return mvc.perform(get("/api/v1/users")
                        .param("nickname", nickname)
                        .header(USER_ID_HEADER, caller))
                .andExpect(status().isOk());
    }

    /**
     * friendships 행이 실제로 지워졌는지 DB에서 직접 확인합니다. 목록 API 만 보면
     * 필터로 가려진 것과 행이 없는 것을 구분할 수 없습니다.
     */
    private int friendshipRowCount(String userIdA, String userIdB) {
        return jdbcTemplate.queryForObject("""
                SELECT COUNT(*)
                  FROM friendships f
                  JOIN users a ON a.users_seq IN (f.user_low_seq, f.user_high_seq)
                  JOIN users b ON b.users_seq IN (f.user_low_seq, f.user_high_seq)
                 WHERE a.public_id = ? AND b.public_id = ? AND a.users_seq <> b.users_seq
                """, Integer.class, userIdA, userIdB);
    }
}
