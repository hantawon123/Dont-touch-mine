package com.ssafy.d205.api;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.ResultActions;
import tools.jackson.databind.ObjectMapper;

import java.util.UUID;

import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.ssafy.d205.domain.user.entity.User;
import com.ssafy.d205.support.IntegrationTest;

class FriendRequestApiTest extends IntegrationTest {

    private static final String USER_ID_HEADER = "X-User-Id";

    @Autowired
    MockMvc mvc;

    @Autowired
    ObjectMapper objectMapper;

    @Autowired
    JdbcTemplate jdbcTemplate;

    @Test
    @DisplayName("요청을 보내면 PENDING 이고 201")
    void sendsRequest() throws Exception {
        String me = createUser();
        String other = createUser();

        send(me, other)
                .andExpect(status().isCreated())
                .andExpect(jsonPath("$.status").value("PENDING"));
    }

    @Test
    @DisplayName("보낸 요청이 상대의 받은 목록에 나타난다")
    void requestAppearsInIncomingList() throws Exception {
        String me = createUser();
        String other = createUser();
        send(me, other).andExpect(status().isCreated());

        mvc.perform(get("/api/v1/friend-requests").header(USER_ID_HEADER, other))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.requests.length()").value(1))
                .andExpect(jsonPath("$.requests[0].userId").value(me))
                .andExpect(jsonPath("$.requests[0].nickname").isNotEmpty())
                .andExpect(jsonPath("$.requests[0].requestedAt").isNotEmpty());
    }

    @Test
    @DisplayName("보낸 요청이 내 보낸 목록에 나타난다")
    void requestAppearsInOutgoingList() throws Exception {
        String me = createUser();
        String other = createUser();
        send(me, other).andExpect(status().isCreated());

        mvc.perform(get("/api/v1/friend-requests")
                        .param("direction", "outgoing")
                        .header(USER_ID_HEADER, me))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.requests.length()").value(1))
                .andExpect(jsonPath("$.requests[0].userId").value(other));
    }

    @Test
    @DisplayName("받은 요청을 수락하면 양쪽 목록에서 사라지고 친구가 된다")
    void acceptMakesThemFriends() throws Exception {
        String me = createUser();
        String other = createUser();
        send(me, other).andExpect(status().isCreated());

        mvc.perform(post("/api/v1/friend-requests/{userId}/accept", me).header(USER_ID_HEADER, other))
                .andExpect(status().isNoContent());

        // 친구 목록 조회는 S15P21D205-432 라서 아직 없습니다. 관찰할 수 있는 것으로
        // 확인합니다. 요청이 사라졌고, 다시 보내면 이미 친구라고 하고, 친구 끊기가
        // 성공합니다. 셋이 모두 성립하면 상태가 ACCEPTED 입니다.
        mvc.perform(get("/api/v1/friend-requests").header(USER_ID_HEADER, other))
                .andExpect(jsonPath("$.requests.length()").value(0));
        mvc.perform(get("/api/v1/friend-requests").param("direction", "outgoing").header(USER_ID_HEADER, me))
                .andExpect(jsonPath("$.requests.length()").value(0));

        send(me, other)
                .andExpect(status().isConflict())
                .andExpect(jsonPath("$.code").value("ALREADY_FRIENDS"));

        mvc.perform(delete("/api/v1/friends/{userId}", other).header(USER_ID_HEADER, me))
                .andExpect(status().isNoContent());
    }

    @Test
    @DisplayName("상대가 이미 보낸 요청이 있으면 바로 친구가 된다")
    void reverseRequestAutoAccepts() throws Exception {
        String first = createUser();
        String second = createUser();
        send(first, second).andExpect(status().isCreated());

        // 서로 원한다는 것이 명확하므로 "받은 요청을 수락하세요"로 되돌리지 않습니다.
        //
        // 201 이 아니라 200 입니다. 새 요청을 만든 것이 아니라 상대가 보내둔 요청을
        // 성립시킨 것이므로 Created 가 아닙니다. 클라이언트는 상태 코드만으로도
        // "요청을 보냈다"와 "친구가 됐다"를 가를 수 있습니다.
        send(second, first)
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.status").value("ACCEPTED"));

        send(first, second)
                .andExpect(status().isConflict())
                .andExpect(jsonPath("$.code").value("ALREADY_FRIENDS"));
    }

    @Test
    @DisplayName("같은 상대에게 두 번 보내면 409")
    void duplicateRequestIsConflict() throws Exception {
        String me = createUser();
        String other = createUser();
        send(me, other).andExpect(status().isCreated());

        send(me, other)
                .andExpect(status().isConflict())
                .andExpect(jsonPath("$.code").value("REQUEST_ALREADY_SENT"));
    }

    @Test
    @DisplayName("자기 자신에게 보내면 400")
    void selfRequestIsBadRequest() throws Exception {
        String me = createUser();

        send(me, me)
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("SELF_FRIEND_REQUEST"));
    }

    @Test
    @DisplayName("없는 상대에게 보내면 404")
    void unknownTargetIsNotFound() throws Exception {
        String me = createUser();

        send(me, UUID.randomUUID().toString())
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.code").value("TARGET_NOT_FOUND"));
    }

    @Test
    @DisplayName("차단 관계면 없는 상대와 같은 404")
    void blockedTargetLooksMissing() throws Exception {
        // 차단당했다는 사실을 알려주면 차단한 사람이 드러납니다. 없는 사용자와
        // 구분되지 않아야 합니다. 코드가 TARGET_NOT_FOUND 로 같은지 확인합니다.
        String me = createUser();
        String other = createUser();
        block(other, me);

        send(me, other)
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.code").value("TARGET_NOT_FOUND"));
    }

    @Test
    @DisplayName("내가 차단한 상대에게도 보낼 수 없다")
    void cannotSendToUserIBlocked() throws Exception {
        // 차단 검사는 방향을 보지 않습니다. 위 테스트가 상대가 나를 차단한 경우이고
        // 이쪽이 내가 상대를 차단한 경우입니다. 한 방향만 시험하면 쿼리의 OR 한쪽이
        // 빠져도 통과합니다.
        String me = createUser();
        String other = createUser();
        block(me, other);

        send(me, other)
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.code").value("TARGET_NOT_FOUND"));
    }

    @Test
    @DisplayName("차단이 걸린 뒤에는 남아 있던 요청을 수락할 수 없다")
    void cannotAcceptAfterBlock() throws Exception {
        // send 에만 차단 검사를 두면 이 경로로 친구가 됩니다. 요청을 먼저 보내고
        // 그다음 차단이 걸리는 순서입니다.
        String requester = createUser();
        String me = createUser();
        send(requester, me).andExpect(status().isCreated());

        block(me, requester);

        mvc.perform(post("/api/v1/friend-requests/{userId}/accept", requester).header(USER_ID_HEADER, me))
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.code").value("TARGET_NOT_FOUND"));
    }

    @Test
    @DisplayName("거절하면 요청이 사라지고 다시 요청할 수 있다")
    void rejectAllowsRequestingAgain() throws Exception {
        // V2 주석이 REJECTED 상태를 남기지 않는 이유로 든 시나리오입니다. 상태로
        // 남기면 uk_friendships_pair 때문에 다시 요청할 수 없게 됩니다.
        String me = createUser();
        String other = createUser();
        send(me, other).andExpect(status().isCreated());

        mvc.perform(delete("/api/v1/friend-requests/{userId}", me).header(USER_ID_HEADER, other))
                .andExpect(status().isNoContent());

        mvc.perform(get("/api/v1/friend-requests").header(USER_ID_HEADER, other))
                .andExpect(jsonPath("$.requests.length()").value(0));

        send(me, other)
                .andExpect(status().isCreated())
                .andExpect(jsonPath("$.status").value("PENDING"));
    }

    @Test
    @DisplayName("보낸 쪽이 같은 엔드포인트로 취소할 수 있다")
    void senderCanCancelWithSameEndpoint() throws Exception {
        String me = createUser();
        String other = createUser();
        send(me, other).andExpect(status().isCreated());

        mvc.perform(delete("/api/v1/friend-requests/{userId}", other).header(USER_ID_HEADER, me))
                .andExpect(status().isNoContent());

        mvc.perform(get("/api/v1/friend-requests").param("direction", "outgoing").header(USER_ID_HEADER, me))
                .andExpect(jsonPath("$.requests.length()").value(0));
    }

    @Test
    @DisplayName("내가 보낸 요청을 내가 수락할 수 없다")
    void cannotAcceptOwnRequest() throws Exception {
        String me = createUser();
        String other = createUser();
        send(me, other).andExpect(status().isCreated());

        mvc.perform(post("/api/v1/friend-requests/{userId}/accept", other).header(USER_ID_HEADER, me))
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.code").value("FRIEND_REQUEST_NOT_FOUND"));
    }

    @Test
    @DisplayName("없는 요청을 수락하면 404")
    void acceptingMissingRequestIsNotFound() throws Exception {
        String me = createUser();
        String other = createUser();

        mvc.perform(post("/api/v1/friend-requests/{userId}/accept", other).header(USER_ID_HEADER, me))
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.code").value("FRIEND_REQUEST_NOT_FOUND"));
    }

    @Test
    @DisplayName("친구가 아닌 상대를 끊으면 404")
    void unfriendingNonFriendIsNotFound() throws Exception {
        String me = createUser();
        String other = createUser();
        send(me, other).andExpect(status().isCreated());

        // 대기 중인 요청은 친구가 아닙니다. 취소는 friend-requests 쪽입니다.
        mvc.perform(delete("/api/v1/friends/{userId}", other).header(USER_ID_HEADER, me))
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.code").value("NOT_FRIENDS"));
    }

    @Test
    @DisplayName("친구를 끊은 뒤 다시 요청할 수 있다")
    void canRequestAgainAfterUnfriend() throws Exception {
        String me = createUser();
        String other = createUser();
        send(me, other).andExpect(status().isCreated());
        mvc.perform(post("/api/v1/friend-requests/{userId}/accept", me).header(USER_ID_HEADER, other))
                .andExpect(status().isNoContent());
        mvc.perform(delete("/api/v1/friends/{userId}", other).header(USER_ID_HEADER, me))
                .andExpect(status().isNoContent());

        send(me, other)
                .andExpect(status().isCreated())
                .andExpect(jsonPath("$.status").value("PENDING"));
    }

    @ParameterizedTest
    @DisplayName("direction 이 잘못되면 400")
    @ValueSource(strings = {"INCOMING", "both", "sent"})
    void invalidDirectionIsBadRequest(String direction) throws Exception {
        // 대문자 INCOMING 도 거부합니다. 스프링의 기본 문자열 바인딩은 대소문자를
        // 구분하므로, 받아주려면 별도 변환기가 필요합니다. 소문자로 고정합니다.
        String me = createUser();

        mvc.perform(get("/api/v1/friend-requests")
                        .param("direction", direction)
                        .header(USER_ID_HEADER, me))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("INVALID_REQUEST"));
    }

    @Test
    @DisplayName("direction 이 비면 받은 요청으로 처리한다")
    void emptyDirectionFallsBackToIncoming() throws Exception {
        // 스프링은 파라미터가 있지만 비어 있으면 defaultValue 를 적용합니다. 그래서
        // direction= 는 400 이 아니라 incoming 입니다. 처음에 400 을 기대하는 테스트를
        // 썼다가 여기서 걸렸습니다. 실제 동작을 기록해 둡니다.
        String me = createUser();
        String other = createUser();
        send(other, me).andExpect(status().isCreated());

        mvc.perform(get("/api/v1/friend-requests")
                        .param("direction", "")
                        .header(USER_ID_HEADER, me))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.requests.length()").value(1))
                .andExpect(jsonPath("$.requests[0].userId").value(other));
    }

    @Test
    @DisplayName("X-User-Id 헤더가 없으면 400")
    void missingHeaderIsBadRequest() throws Exception {
        mvc.perform(get("/api/v1/friend-requests"))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("MISSING_HEADER"));
    }

    @Test
    @DisplayName("없는 계정으로 부르면 404 ACCOUNT_NOT_FOUND")
    void unknownCallerIsNotFound() throws Exception {
        // 상대가 없을 때(TARGET_NOT_FOUND)와 코드가 달라야 합니다. 클라이언트는
        // 이 경우 계정 발급을 다시 불러야 합니다.
        mvc.perform(get("/api/v1/friend-requests").header(USER_ID_HEADER, UUID.randomUUID().toString()))
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.code").value("ACCOUNT_NOT_FOUND"));
    }

    @Test
    @DisplayName("userId 가 비면 400")
    void blankTargetIsBadRequest() throws Exception {
        String me = createUser();

        send(me, "")
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("INVALID_REQUEST"));
    }

    private String createUser() throws Exception {
        String body = mvc.perform(post("/api/v1/accounts")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"deviceId\":\"" + UUID.randomUUID() + "\"}"))
                .andExpect(status().isCreated())
                .andReturn().getResponse().getContentAsString();
        return objectMapper.readTree(body).get("userId").asText();
    }

    private ResultActions send(String callerUserId, String targetUserId) throws Exception {
        return mvc.perform(post("/api/v1/friend-requests")
                .header(USER_ID_HEADER, callerUserId)
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"userId\":\"" + targetUserId + "\"}"));
    }

    /** 차단 API가 아직 없어서 직접 넣습니다. */
    private void block(String blockerUserId, String blockedUserId) {
        jdbcTemplate.update("""
                INSERT INTO user_blocks (blocker_seq, blocked_seq, created_at)
                SELECT b.users_seq, t.users_seq, '20260101000000'
                  FROM users b, users t
                 WHERE b.public_id = ? AND t.public_id = ?
                """, blockerUserId, blockedUserId);
    }
}
