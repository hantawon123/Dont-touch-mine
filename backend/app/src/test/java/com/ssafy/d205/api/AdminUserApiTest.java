package com.ssafy.d205.api;

import jakarta.servlet.http.Cookie;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.mock.web.MockHttpSession;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.MvcResult;
import tools.jackson.databind.JsonNode;
import tools.jackson.databind.ObjectMapper;

import java.util.ArrayList;
import java.util.List;
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

/**
 * 관리 화면 사용자 검색과 상세 (S15P21D205-972, 973).
 *
 * <p>그 전에는 정지 버튼이 신고 목록 안에만 있어서 신고가 없는 사람은 운영자가 찾을 길이
 * 없었습니다. 이 테스트는 운영자가 아무 사용자나 찾고, 그 사람이 받은 신고·한 신고·피드백을
 * 한 번에 보는 것을 고정합니다.
 */
class AdminUserApiTest extends IntegrationTest {

    private static final String USER_ID_HEADER = "X-User-Id";
    private static final String DEVICE_ID_HEADER = "X-Device-Id";
    private static final String LOGIN = "/api/v1/admin/session";
    private static final String USERS = "/api/v1/admin/users";

    @Autowired
    MockMvc mvc;

    @Autowired
    ObjectMapper objectMapper;

    @Test
    @DisplayName("로그인 없이 부르면 401")
    void needsAnAdminSession() throws Exception {
        mvc.perform(get(USERS)).andExpect(status().isUnauthorized());
        mvc.perform(get(USERS + "/{userId}", UUID.randomUUID())).andExpect(status().isUnauthorized());
    }

    @Test
    @DisplayName("닉네임 일부로 찾고, 대소문자를 구분한다")
    void findsByPartOfTheNicknameCaseSensitively() throws Exception {
        Admin admin = login();
        // 닉네임은 2~12자라 접두사를 짧게 둡니다. 접미사 6자는 다른 테스트의 사람과 겹치지 않게 합니다.
        String suffix = unique();
        String me = createUser();
        rename(me, "Srch" + suffix);

        List<String> found = idsOf(search(admin, "rch" + suffix));
        assertThat(found).containsExactly(me);

        // nickname 이 as_cs 콜레이션이라 게임 쪽 검색과 같은 규칙입니다. 구분하지 않으면 한
        // 검색이 서로 다른 두 사람을 함께 내놓습니다.
        assertThat(idsOf(search(admin, "srch" + suffix))).isEmpty();
    }

    @Test
    @DisplayName("검색을 꺼 둔 사람도 userId 로 찾는다")
    void findsByExactUserIdEvenWhenUnsearchable() throws Exception {
        Admin admin = login();
        String me = createUser();

        // 게임 클라이언트 검색에서는 빠지는 사람입니다. 운영자는 그 사람도 찾아야 합니다.
        mvc.perform(put("/api/v1/accounts/me/searchable")
                        .header(USER_ID_HEADER, me)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"searchable\":false}"))
                .andExpect(status().isOk());

        assertThat(idsOf(search(admin, me))).containsExactly(me);
    }

    @Test
    @DisplayName("검색어의 % 와 _ 는 글자 그대로다")
    void wildcardsAreLiteral() throws Exception {
        Admin admin = login();
        createUser();

        // 이스케이프하지 않으면 % 하나로 전체 목록이 나옵니다. 그건 빈 검색어의 몫이지
        // % 를 친 사람의 몫이 아닙니다. 닉네임에 % 가 들어갈 수 없으므로 결과는 비어야 합니다.
        assertThat(idsOf(search(admin, "%"))).isEmpty();
        assertThat(idsOf(search(admin, "_"))).isEmpty();
    }

    @Test
    @DisplayName("검색어가 비면 최근 가입순이고, limit 은 50 으로 잘린다")
    void emptyQueryListsNewestFirstWithinTheCap() throws Exception {
        Admin admin = login();
        createUser();
        String newest = createUser();

        JsonNode users = search(admin, "").get("users");
        assertThat(users.get(0).get("userId").asText()).isEqualTo(newest);

        assertThat(searchWith(admin, "", 500).get("users").size()).isLessThanOrEqualTo(50);
        assertThat(searchWith(admin, "", 1).get("users").size()).isEqualTo(1);
    }

    @Test
    @DisplayName("줄마다 신고 수, 친구 수, 정지 여부, 접속 상태가 실린다")
    void rowsCarryCountsStateAndPresence() throws Exception {
        Admin admin = login();
        String me = createUser();
        String friend = createUser();
        String reporterA = createUser();
        String reporterB = createUser();

        befriend(friend, me);
        report(reporterA, me, "ABUSE", null);
        report(reporterB, me, "SPAM", "도배");
        // 보낸 요청은 친구가 아닙니다. 세면 안 됩니다.
        mvc.perform(post("/api/v1/friend-requests")
                        .header(USER_ID_HEADER, me)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"userId\":\"" + reporterA + "\"}"))
                .andExpect(status().isCreated());
        mvc.perform(put("/api/v1/presence")
                        .header(USER_ID_HEADER, me)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"sessionId\":\"7K2M9P\",\"sessionKind\":\"LOBBY\"}"))
                .andExpect(status().isNoContent());

        JsonNode before = rowOf(search(admin, me), me);
        assertThat(before.get("reportCount").asInt()).isEqualTo(2);
        assertThat(before.get("friendCount").asInt()).isEqualTo(1);
        assertThat(before.get("presence").asText()).isEqualTo("IN_LOBBY");
        assertThat(before.get("lastSeenAt").asText()).hasSize(14);
        assertThat(before.get("suspended").asBoolean()).isFalse();
        assertThat(before.get("suspendedAt").isNull()).isTrue();

        suspend(admin, me, "테스트 정지");

        JsonNode after = rowOf(search(admin, me), me);
        assertThat(after.get("suspended").asBoolean()).isTrue();
        assertThat(after.get("suspendedAt").asText()).hasSize(14);
        assertThat(after.get("suspendedReason").asText()).isEqualTo("테스트 정지");
    }

    @Test
    @DisplayName("한 번도 붙은 적 없는 사람은 OFFLINE 이다")
    void neverConnectedReadsOffline() throws Exception {
        Admin admin = login();
        String me = createUser();

        JsonNode row = rowOf(search(admin, me), me);
        assertThat(row.get("presence").asText()).isEqualTo("OFFLINE");
        assertThat(row.get("lastSeenAt").isNull()).isTrue();
    }

    @Test
    @DisplayName("상세에 받은 신고, 한 신고, 보낸 피드백이 나뉘어 실린다")
    void detailSplitsReceivedMadeAndFeedback() throws Exception {
        Admin admin = login();
        String me = createUser();
        String other = createUser();
        String third = createUser();
        rename(other, "Other" + unique());

        report(other, me, "ABUSE", "욕설");
        report(me, third, "CHEATING", null);
        feedback(me, "점프가 이상해요");

        JsonNode detail = detail(admin, me);

        assertThat(detail.get("user").get("userId").asText()).isEqualTo(me);
        assertThat(detail.get("user").get("reportCount").asInt()).isEqualTo(1);

        JsonNode received = detail.get("receivedReports");
        assertThat(received).hasSize(1);
        assertThat(received.get(0).get("reason").asText()).isEqualTo("ABUSE");
        assertThat(received.get(0).get("memo").asText()).isEqualTo("욕설");
        assertThat(received.get(0).get("status").asText()).isEqualTo("PENDING");
        // 운영자 자리라 신고자를 보여줍니다. 게임 쪽 신고 상세는 이 값을 일부러 뺍니다.
        assertThat(received.get(0).get("counterpartUserId").asText()).isEqualTo(other);
        assertThat(received.get(0).get("counterpartNickname").asText()).startsWith("Other");

        JsonNode made = detail.get("madeReports");
        assertThat(made).hasSize(1);
        assertThat(made.get(0).get("reason").asText()).isEqualTo("CHEATING");
        assertThat(made.get(0).get("counterpartUserId").asText()).isEqualTo(third);

        JsonNode feedback = detail.get("feedback");
        assertThat(feedback).hasSize(1);
        assertThat(feedback.get(0).get("message").asText()).isEqualTo("점프가 이상해요");
    }

    @Test
    @DisplayName("탈퇴한 신고자는 이름 없이 남는다")
    void aDeletedReporterShowsAsGone() throws Exception {
        Admin admin = login();
        String me = createUser();
        String reporterDevice = UUID.randomUUID().toString();
        String reporter = createUser(reporterDevice);

        report(reporter, me, "SPAM", null);
        mvc.perform(delete("/api/v1/accounts/me")
                        .header(USER_ID_HEADER, reporter)
                        .header(DEVICE_ID_HEADER, reporterDevice))
                .andExpect(status().isNoContent());

        JsonNode received = detail(admin, me).get("receivedReports");
        // 신고는 신고당한 사람에 대한 기록이라 신고자가 떠나도 남습니다(V9). 누구였는지만 비웁니다.
        assertThat(received).hasSize(1);
        assertThat(received.get(0).get("counterpartUserId").isNull()).isTrue();
        assertThat(received.get(0).get("counterpartNickname").isNull()).isTrue();
    }

    @Test
    @DisplayName("숨긴 신고는 상세와 건수에서 빠진다")
    void hiddenReportsAreLeftOut() throws Exception {
        Admin admin = login();
        String me = createUser();
        String reporter = createUser();
        report(reporter, me, "ABUSE", null);

        mvc.perform(patch("/api/v1/admin/reports/{userId}/hidden", me)
                        .param("status", "PENDING")
                        .session(admin.session())
                        .cookie(admin.csrf())
                        .header("X-XSRF-TOKEN", admin.csrf().getValue()))
                .andExpect(status().isOk());

        JsonNode detail = detail(admin, me);
        assertThat(detail.get("receivedReports")).isEmpty();
        assertThat(detail.get("user").get("reportCount").asInt()).isZero();
    }

    @Test
    @DisplayName("없는 계정은 404 TARGET_NOT_FOUND")
    void unknownUserIs404() throws Exception {
        Admin admin = login();

        mvc.perform(get(USERS + "/{userId}", UUID.randomUUID()).session(admin.session()))
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.code").value("TARGET_NOT_FOUND"));
    }

    @Test
    @DisplayName("응답 어디에도 기기 식별자가 없다")
    void theDeviceIdNeverAppears() throws Exception {
        Admin admin = login();
        String device = UUID.randomUUID().toString();
        String me = createUser(device);

        String list = search(admin, me).toString();
        String detail = detail(admin, me).toString();

        // 기기 식별자는 그 계정의 비밀번호입니다. 운영자 화면에도 나가면 안 됩니다.
        assertThat(list).doesNotContain(device);
        assertThat(detail).doesNotContain(device);
    }

    private JsonNode search(Admin admin, String q) throws Exception {
        return searchWith(admin, q, null);
    }

    private JsonNode searchWith(Admin admin, String q, Integer limit) throws Exception {
        var request = get(USERS).session(admin.session()).param("q", q);
        if (limit != null) {
            request = request.param("limit", String.valueOf(limit));
        }
        String body = mvc.perform(request)
                .andExpect(status().isOk())
                .andReturn().getResponse().getContentAsString();
        return objectMapper.readTree(body);
    }

    private JsonNode detail(Admin admin, String userId) throws Exception {
        String body = mvc.perform(get(USERS + "/{userId}", userId).session(admin.session()))
                .andExpect(status().isOk())
                .andReturn().getResponse().getContentAsString();
        return objectMapper.readTree(body);
    }

    private static List<String> idsOf(JsonNode response) {
        List<String> ids = new ArrayList<>();
        for (JsonNode user : response.get("users")) {
            ids.add(user.get("userId").asText());
        }
        return ids;
    }

    private static JsonNode rowOf(JsonNode response, String userId) {
        for (JsonNode user : response.get("users")) {
            if (user.get("userId").asText().equals(userId)) {
                return user;
            }
        }
        throw new AssertionError("목록에 " + userId + " 가 없습니다: " + response);
    }

    /** 영문 소문자·숫자 6자. 닉네임 규칙(한/영/숫자, 12자 이하) 안에 들어가는 꼬리표입니다. */
    private static String unique() {
        String tail = Long.toString(System.nanoTime(), 36);
        return tail.substring(tail.length() - 6);
    }

    private void rename(String userId, String nickname) throws Exception {
        mvc.perform(patch("/api/v1/accounts/me")
                        .header(USER_ID_HEADER, userId)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"nickname\":\"" + nickname + "\"}"))
                .andExpect(status().isOk());
    }

    private void befriend(String from, String to) throws Exception {
        mvc.perform(post("/api/v1/friend-requests")
                        .header(USER_ID_HEADER, from)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"userId\":\"" + to + "\"}"))
                .andExpect(status().isCreated());
        mvc.perform(post("/api/v1/friend-requests/{userId}/accept", from)
                        .header(USER_ID_HEADER, to))
                .andExpect(status().isNoContent());
    }

    private void report(String reporter, String reported, String reason, String memo) throws Exception {
        String body = memo == null
                ? "{\"userId\":\"" + reported + "\",\"reason\":\"" + reason + "\"}"
                : "{\"userId\":\"" + reported + "\",\"reason\":\"" + reason + "\",\"memo\":\"" + memo + "\"}";
        mvc.perform(post("/api/v1/reports")
                        .header(USER_ID_HEADER, reporter)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(body))
                .andExpect(status().isCreated());
    }

    private void feedback(String author, String message) throws Exception {
        mvc.perform(post("/api/v1/feedback")
                        .header(USER_ID_HEADER, author)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"message\":\"" + message + "\"}"))
                .andExpect(status().isCreated());
    }

    private void suspend(Admin admin, String userId, String reason) throws Exception {
        mvc.perform(put(USERS + "/{userId}/suspension", userId)
                        .session(admin.session())
                        .cookie(admin.csrf())
                        .header("X-XSRF-TOKEN", admin.csrf().getValue())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"reason\":\"" + reason + "\"}"))
                .andExpect(status().isOk());
    }

    private Admin login() throws Exception {
        MockHttpSession session = new MockHttpSession();

        MvcResult result = mvc.perform(post(LOGIN)
                        .session(session)
                        .param("username", "test-admin")
                        .param("password", "test-password"))
                .andExpect(status().isNoContent())
                .andReturn();

        Cookie token = result.getResponse().getCookie("XSRF-TOKEN");
        assertThat(token).as("로그인 응답에 CSRF 토큰 쿠키가 없습니다").isNotNull();
        return new Admin(session, token);
    }

    private record Admin(MockHttpSession session, Cookie csrf) {
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
}
