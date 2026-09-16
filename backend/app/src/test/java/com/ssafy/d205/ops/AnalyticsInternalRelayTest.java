package com.ssafy.d205.ops;

import com.sun.net.httpserver.HttpServer;
import jakarta.servlet.http.Cookie;
import org.junit.jupiter.api.AfterAll;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.mock.web.MockHttpSession;
import org.springframework.test.context.DynamicPropertyRegistry;
import org.springframework.test.context.DynamicPropertySource;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.MvcResult;
import tools.jackson.databind.JsonNode;
import tools.jackson.databind.ObjectMapper;

import java.io.IOException;
import java.io.OutputStream;
import java.net.InetSocketAddress;
import java.nio.charset.StandardCharsets;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.CopyOnWriteArrayList;
import java.util.concurrent.atomic.AtomicBoolean;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.ssafy.d205.domain.ops.service.AnalyticsErasureRelay;
import com.ssafy.d205.support.IntegrationTest;

/**
 * 계정 서비스가 분석 서비스의 내부 API 를 부르는 세 경로 (S15P21D205-1002, 분석 탭은 976).
 *
 * <p>분석 서비스 대신 JDK 내장 HttpServer 가 받습니다. 진짜 분석 서비스를 띄우면 테스트가 두 컨테이너에
 * 걸리고, 여기서 보려는 것은 "계정 서비스가 무엇을 언제 보내고 실패하면 어떻게 하는가"입니다. 분석
 * 서비스가 그 요청을 어떻게 처리하는지는 그쪽 모듈의 InternalApiTest 가 봅니다.
 *
 * <p>가짜 서버를 "죽인" 상태는 응답 대신 503 을 내는 것으로 흉내냅니다. 포트를 실제로 닫으면 재시작할 때
 * 같은 포트를 못 잡을 수 있어 컨텍스트가 가진 주소와 어긋납니다.
 */
class AnalyticsInternalRelayTest extends IntegrationTest {

    private static final String USER_ID_HEADER = "X-User-Id";
    private static final String DEVICE_ID_HEADER = "X-Device-Id";
    private static final String KEY = "test-internal-key";

    private static final HttpServer FAKE_ANALYTICS;
    private static final List<String> RECEIVED = new CopyOnWriteArrayList<>();
    private static final AtomicBoolean DOWN = new AtomicBoolean(false);

    static {
        try {
            FAKE_ANALYTICS = HttpServer.create(new InetSocketAddress("127.0.0.1", 0), 0);
        } catch (IOException e) {
            throw new IllegalStateException(e);
        }
        FAKE_ANALYTICS.createContext("/internal/", exchange -> {
            String key = exchange.getRequestHeaders().getFirst("X-Internal-Key");
            String query = exchange.getRequestURI().getRawQuery();
            RECEIVED.add(exchange.getRequestMethod() + " " + exchange.getRequestURI().getPath()
                    + (query == null ? "" : "?" + query) + " key=" + key);
            byte[] body;
            int status;
            if (DOWN.get()) {
                status = 503;
                body = new byte[0];
            } else if (!KEY.equals(key)) {
                status = 404;
                body = new byte[0];
            } else if (exchange.getRequestURI().getPath().startsWith("/internal/admin/analytics/")) {
                // 분석 탭의 표. 질문이 무엇이든 같은 표를 줍니다 - 여기서 보는 것은 전달이지 내용이 아닙니다.
                status = 200;
                body = "{\"columns\":[\"경기\",\"인원\"],\"rows\":[[\"m-1\",4]]}".getBytes(StandardCharsets.UTF_8);
            } else if (exchange.getRequestURI().getPath().endsWith("/summary")) {
                status = 200;
                body = "{\"matchesToday\":7,\"inProgress\":1,\"avgDurationSec\":412.5,\"dropoutRate\":0.125}"
                        .getBytes(StandardCharsets.UTF_8);
            } else {
                status = 200;
                body = "{\"erased\":3}".getBytes(StandardCharsets.UTF_8);
            }
            exchange.getResponseHeaders().add("Content-Type", "application/json");
            exchange.sendResponseHeaders(status, body.length == 0 ? -1 : body.length);
            try (OutputStream out = exchange.getResponseBody()) {
                out.write(body);
            }
        });
        FAKE_ANALYTICS.start();
    }

    @DynamicPropertySource
    static void analyticsAddress(DynamicPropertyRegistry registry) {
        registry.add("analytics.internal-url", () -> "http://127.0.0.1:" + FAKE_ANALYTICS.getAddress().getPort());
        registry.add("analytics.internal-key", () -> KEY);
    }

    @AfterAll
    static void stopFakeAnalytics() {
        FAKE_ANALYTICS.stop(0);
    }

    @Autowired
    MockMvc mvc;

    @Autowired
    ObjectMapper objectMapper;

    @Autowired
    AnalyticsErasureRelay relay;

    @BeforeEach
    void reset() {
        RECEIVED.clear();
        DOWN.set(false);
    }

    @Test
    @DisplayName("탈퇴가 커밋되면 분석 서비스에 그 사람의 익명화를 키와 함께 요청한다")
    void deletionAsksAnalyticsToErase() throws Exception {
        String device = UUID.randomUUID().toString();
        String leaver = createUser(device);

        deleteAccount(leaver, device);

        assertThat(RECEIVED).contains("DELETE /internal/users/" + leaver + "/events key=" + KEY);
        assertThat(relay.pendingCount()).isZero();
    }

    @Test
    @DisplayName("분석 서비스가 죽어 있어도 탈퇴는 성공하고, 살아나면 재시도로 전달된다")
    void deletionSucceedsWhileAnalyticsIsDownAndIsRetriedLater() throws Exception {
        String device = UUID.randomUUID().toString();
        String leaver = createUser(device);
        DOWN.set(true);

        deleteAccount(leaver, device);
        assertThat(relay.pendingCount()).isGreaterThanOrEqualTo(1);

        // 살아났습니다. 스케줄러는 테스트 프로필에서 사실상 꺼져 있으므로 직접 부릅니다.
        DOWN.set(false);
        RECEIVED.clear();
        int delivered = relay.retry();

        assertThat(delivered).isGreaterThanOrEqualTo(1);
        assertThat(RECEIVED).contains("DELETE /internal/users/" + leaver + "/events key=" + KEY);
        assertThat(relay.pendingCount()).isZero();
    }

    @Test
    @DisplayName("개요 탭의 경기 통계는 분석 서비스에서 받아 채운다")
    void overviewCarriesMatchStatsFromAnalytics() throws Exception {
        Admin admin = login();

        JsonNode matches = overview(admin).get("matches");

        assertThat(matches.get("matchesToday").asLong()).isEqualTo(7);
        assertThat(matches.get("inProgress").asLong()).isEqualTo(1);
        assertThat(matches.get("avgDurationSec").asDouble()).isEqualTo(412.5);
        assertThat(matches.get("dropoutRate").asDouble()).isEqualTo(0.125);
        assertThat(RECEIVED).contains("GET /internal/admin/summary key=" + KEY);
    }

    @Test
    @DisplayName("분석 서비스가 죽어 있으면 경기 통계만 비고 개요는 열린다")
    void overviewStillOpensWithoutAnalytics() throws Exception {
        Admin admin = login();
        DOWN.set(true);

        JsonNode body = overview(admin);

        assertThat(body.get("matches").isNull()).isTrue();
        assertThat(body.get("now").get("totalUsers").asLong()).isGreaterThanOrEqualTo(0);
    }

    @Test
    @DisplayName("분석 탭의 표는 질문 이름과 필터를 그대로 넘겨 분석 서비스의 표를 받아 온다")
    void analyticsTableIsProxiedWithFilters() throws Exception {
        Admin admin = login();

        JsonNode table = analyticsTable(get("/api/v1/admin/analytics/hideouts")
                .param("from", "20260901000000")
                .param("to", "20260915000000")
                .param("matchId", "m-1")
                .session(admin.session()));

        assertThat(table.get("unavailable").asBoolean()).isFalse();
        assertThat(table.get("columns").get(0).asText()).isEqualTo("경기");
        assertThat(table.get("rows").get(0).get(0).asText()).isEqualTo("m-1");
        assertThat(table.get("rows").get(0).get(1).asInt()).isEqualTo(4);
        assertThat(RECEIVED).contains(
                "GET /internal/admin/analytics/hideouts?from=20260901000000&to=20260915000000&matchId=m-1 key=" + KEY);
    }

    @Test
    @DisplayName("좌표는 matchId 를 그대로 넘긴다")
    void positionsPassTheMatchThrough() throws Exception {
        Admin admin = login();

        analyticsTable(get("/api/v1/admin/analytics/positions").param("matchId", "m-1").session(admin.session()));

        assertThat(RECEIVED).contains("GET /internal/admin/analytics/positions?matchId=m-1 key=" + KEY);
    }

    @Test
    @DisplayName("분석 탭은 관리자 세션이 없으면 401 이고 분석 서비스를 부르지 않는다")
    void analyticsTabNeedsAnAdminSession() throws Exception {
        mvc.perform(get("/api/v1/admin/analytics/matches")).andExpect(status().isUnauthorized());

        assertThat(RECEIVED).isEmpty();
    }

    @Test
    @DisplayName("분석 서비스가 죽어 있으면 200 에 unavailable 과 빈 표다")
    void analyticsTabShowsUnavailableWhenAnalyticsIsDown() throws Exception {
        Admin admin = login();
        DOWN.set(true);

        JsonNode table = analyticsTable(get("/api/v1/admin/analytics/matches").session(admin.session()));

        assertThat(table.get("unavailable").asBoolean()).isTrue();
        assertThat(table.get("columns").size()).isZero();
        assertThat(table.get("rows").size()).isZero();
    }

    @Test
    @DisplayName("필터 형식이 틀리면 분석 서비스에 묻지 않고 400 이다")
    void badFiltersAreRejectedBeforeAskingAnalytics() throws Exception {
        Admin admin = login();

        mvc.perform(get("/api/v1/admin/analytics/matches").param("from", "abc").session(admin.session()))
                .andExpect(status().isBadRequest());
        mvc.perform(get("/api/v1/admin/analytics/positions").session(admin.session()))
                .andExpect(status().isBadRequest());
        mvc.perform(get("/api/v1/admin/analytics/Not_A_Question").session(admin.session()))
                .andExpect(status().isBadRequest());

        assertThat(RECEIVED).isEmpty();
    }

    private JsonNode analyticsTable(org.springframework.test.web.servlet.request.MockHttpServletRequestBuilder request)
            throws Exception {
        // 한글 컬럼 이름이 있어 바이트로 받아 UTF-8 로 읽습니다. getContentAsString 은 charset 이 없으면 ISO-8859-1 입니다.
        byte[] body = mvc.perform(request).andExpect(status().isOk()).andReturn().getResponse().getContentAsByteArray();
        return objectMapper.readTree(body);
    }

    private JsonNode overview(Admin admin) throws Exception {
        String body = mvc.perform(get("/api/v1/admin/overview").session(admin.session()))
                .andExpect(status().isOk())
                .andReturn().getResponse().getContentAsString();
        return objectMapper.readTree(body);
    }

    private void deleteAccount(String userId, String device) throws Exception {
        mvc.perform(delete("/api/v1/accounts/me").header(USER_ID_HEADER, userId).header(DEVICE_ID_HEADER, device))
                .andExpect(status().isNoContent());
    }

    private Admin login() throws Exception {
        MockHttpSession session = new MockHttpSession();
        MvcResult result = mvc.perform(post("/api/v1/admin/session")
                        .session(session)
                        .param("username", "test-admin")
                        .param("password", "test-password"))
                .andExpect(status().isNoContent())
                .andReturn();
        Cookie token = result.getResponse().getCookie("XSRF-TOKEN");
        assertThat(token).isNotNull();
        return new Admin(session, token);
    }

    private record Admin(MockHttpSession session, Cookie csrf) {
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
