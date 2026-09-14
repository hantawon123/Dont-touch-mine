package com.ssafy.d205.api;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.mock.web.MockHttpSession;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.MvcResult;
import tools.jackson.databind.JsonNode;
import tools.jackson.databind.ObjectMapper;

import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.ssafy.d205.support.IntegrationTest;

/**
 * 운영자가 피드백을 읽는 API.
 *
 * <p>두 가지를 봅니다. <b>로그인하지 않으면 볼 수 없다</b>는 것과, <b>탈퇴한 사람의
 * 피드백도 목록에 남는다</b>는 것입니다.
 *
 * <p>후자를 굳이 고정하는 이유는 조회가 LEFT JOIN 이라는 사실에 기대기 때문입니다.
 * INNER JOIN 으로 바꾸면 작성자가 NULL 인 행이 목록에서 조용히 사라집니다. 저장은
 * 그대로 되고 있어서 아무도 알아채지 못합니다.
 */
class AdminFeedbackApiTest extends IntegrationTest {

    private static final String USER_ID_HEADER = "X-User-Id";
    private static final String DEVICE_ID_HEADER = "X-Device-Id";
    private static final String LOGIN = "/api/v1/admin/session";
    private static final String ADMIN_FEEDBACK = "/api/v1/admin/feedback";

    @Autowired
    MockMvc mvc;

    @Autowired
    ObjectMapper objectMapper;

    @Test
    @DisplayName("로그인하지 않으면 조회할 수 없다")
    void readingNeedsLogin() throws Exception {
        // 이 경로가 SecurityConfig 의 첫 체인에 걸려 있다는 것을 확인합니다. 여기가
        // 열리면 아무나 플레이어가 쓴 글을 들여다볼 수 있습니다.
        mvc.perform(get(ADMIN_FEEDBACK)).andExpect(status().isUnauthorized());
    }

    @Test
    @DisplayName("최근에 보낸 것이 먼저 나온다")
    void recentFirst() throws Exception {
        String me = createUser();
        String marker = UUID.randomUUID().toString().substring(0, 8);
        send(me, "먼저 보낸 " + marker);
        send(me, "나중에 보낸 " + marker);

        JsonNode list = list(login(), null);

        // 같은 초에 두 건이 들어가면 created_at 만으로는 순서가 정해지지 않습니다.
        // 조회가 seq 를 보조 정렬로 쓰는 덕분에 이 단정이 성립합니다.
        JsonNode mine = onlyWith(list, marker);
        assertThat(mine.get(0).get("message").asText()).startsWith("나중에 보낸");
        assertThat(mine.get(1).get("message").asText()).startsWith("먼저 보낸");
    }

    @Test
    @DisplayName("본문과 작성자와 빌드가 함께 나온다")
    void listCarriesWhatIsNeededToAct() throws Exception {
        String me = createUser();
        String marker = UUID.randomUUID().toString().substring(0, 8);

        mvc.perform(post("/api/v1/feedback")
                        .header(USER_ID_HEADER, me)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"message\":\"튕깁니다 " + marker
                                + "\",\"buildVer\":\"1.4.2\",\"platform\":\"WebGL\"}"))
                .andExpect(status().isCreated());

        JsonNode row = onlyWith(list(login(), null), marker).get(0);
        assertThat(row.get("userId").asText()).isEqualTo(me);
        assertThat(row.get("nickname").isNull()).isFalse();
        assertThat(row.get("buildVer").asText()).isEqualTo("1.4.2");
        assertThat(row.get("platform").asText()).isEqualTo("WebGL");
        assertThat(row.get("createdAt").asText()).hasSize(14);
    }

    @Test
    @DisplayName("탈퇴한 사람의 피드백도 남고 작성자만 비어 나온다")
    void feedbackFromDeletedAccountsStays() throws Exception {
        // 탈퇴는 기기 식별자를 자격증명으로 요구합니다.
        String deviceId = UUID.randomUUID().toString();
        String me = createUser(deviceId);
        String marker = UUID.randomUUID().toString().substring(0, 8);
        send(me, "탈퇴할 사람의 말 " + marker);

        mvc.perform(delete("/api/v1/accounts/me")
                        .header(USER_ID_HEADER, me)
                        .header(DEVICE_ID_HEADER, deviceId))
                .andExpect(status().isNoContent());

        JsonNode row = onlyWith(list(login(), null), marker).get(0);
        assertThat(row.get("userId").isNull()).isTrue();
        assertThat(row.get("nickname").isNull()).isTrue();
    }

    @Test
    @DisplayName("개수를 크게 요청하면 거절하지 않고 상한으로 깎는다")
    void oversizedLimitIsClampedNotRejected() throws Exception {
        // 운영자가 큰 값을 넣는 상황은 "다 보고 싶다" 이고, 거기에 400 을 주면 답이
        // 되지 않습니다. 상한 자체는 응답이 무한히 자라지 않게 하려고 둡니다.
        String me = createUser();
        send(me, "상한 확인 " + UUID.randomUUID().toString().substring(0, 8));

        MockHttpSession admin = login();
        assertThat(list(admin, 9999).size()).isLessThanOrEqualTo(200);
        assertThat(list(admin, 1).size()).isEqualTo(1);
    }

    /** 이 테스트가 만든 것만 골라 냅니다. 컨텍스트를 공유하므로 다른 테스트의 행이 섞입니다. */
    private JsonNode onlyWith(JsonNode list, String marker) {
        var filtered = objectMapper.createArrayNode();
        for (JsonNode row : list) {
            if (row.get("message").asText().contains(marker)) {
                filtered.add(row);
            }
        }

        assertThat(filtered.isEmpty()).as("목록에서 이 테스트의 피드백을 찾지 못했습니다").isFalse();
        return filtered;
    }

    private JsonNode list(MockHttpSession admin, Integer limit) throws Exception {
        var request = get(ADMIN_FEEDBACK).session(admin);
        if (limit != null) {
            request = request.param("limit", String.valueOf(limit));
        }

        String body = mvc.perform(request)
                .andExpect(status().isOk())
                .andReturn().getResponse().getContentAsString();

        return objectMapper.readTree(body).get("feedback");
    }

    private void send(String caller, String message) throws Exception {
        mvc.perform(post("/api/v1/feedback")
                        .header(USER_ID_HEADER, caller)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"message\":\"" + message + "\"}"))
                .andExpect(status().isCreated());
    }

    private MockHttpSession login() throws Exception {
        MockHttpSession session = new MockHttpSession();

        MvcResult result = mvc.perform(post(LOGIN)
                        .session(session)
                        .param("username", "test-admin")
                        .param("password", "test-password"))
                .andExpect(status().isNoContent())
                .andReturn();

        assertThat(result.getResponse().getCookie("XSRF-TOKEN"))
                .as("로그인 응답에 CSRF 토큰 쿠키가 없습니다").isNotNull();

        return session;
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
