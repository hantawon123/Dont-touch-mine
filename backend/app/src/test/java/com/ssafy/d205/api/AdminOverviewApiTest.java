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

import java.time.Duration;
import java.time.Instant;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.ssafy.d205.domain.admin.service.AdminOverviewService;
import com.ssafy.d205.domain.ops.entity.OpsSample;
import com.ssafy.d205.domain.ops.repository.OpsSampleRepository;
import com.ssafy.d205.domain.ops.service.OpsSampler;
import com.ssafy.d205.global.common.Timestamps;
import com.ssafy.d205.support.IntegrationTest;

/**
 * 관리 화면 개요 탭 (S15P21D205-1003).
 */
class AdminOverviewApiTest extends IntegrationTest {

    private static final String USER_ID_HEADER = "X-User-Id";
    private static final String LOGIN = "/api/v1/admin/session";
    private static final String OVERVIEW = "/api/v1/admin/overview";

    @Autowired
    MockMvc mvc;

    @Autowired
    ObjectMapper objectMapper;

    @Autowired
    OpsSampler sampler;

    @Autowired
    OpsSampleRepository samples;

    @Test
    @DisplayName("로그인 없이 부르면 401")
    void needsAnAdminSession() throws Exception {
        mvc.perform(get(OVERVIEW)).andExpect(status().isUnauthorized());
    }

    @Test
    @DisplayName("지금 값 카드가 DB 와 맞는다")
    void nowCardsMatchTheDatabase() throws Exception {
        Admin admin = login();
        String me = createUser();
        String other = createUser();
        mvc.perform(put("/api/v1/presence")
                        .header(USER_ID_HEADER, me)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"sessionId\":\"7K2M9P\",\"sessionKind\":\"LOBBY\"}"))
                .andExpect(status().isNoContent());
        mvc.perform(post("/api/v1/reports")
                        .header(USER_ID_HEADER, me)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"userId\":\"" + other + "\",\"reason\":\"SPAM\"}"))
                .andExpect(status().isCreated());
        mvc.perform(post("/api/v1/feedback")
                        .header(USER_ID_HEADER, me)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"message\":\"개요 탭 테스트\"}"))
                .andExpect(status().isCreated());
        suspend(admin, other);

        JsonNode now = overview(admin, "24h").get("now");

        assertThat(now.get("inLobby").asInt()).isGreaterThanOrEqualTo(1);
        assertThat(now.get("totalUsers").asLong()).isGreaterThanOrEqualTo(2);
        assertThat(now.get("signupsToday").asLong()).isGreaterThanOrEqualTo(2);
        assertThat(now.get("pendingReports").asLong()).isGreaterThanOrEqualTo(1);
        assertThat(now.get("feedbackToday").asLong()).isGreaterThanOrEqualTo(1);
        assertThat(now.get("suspendedUsers").asLong()).isGreaterThanOrEqualTo(1);
        assertThat(now.get("heapMaxMb").asInt()).isPositive();
        assertThat(now.get("dbPoolMax").asInt()).isPositive();
        // 분석 서비스가 분리되기 전까지는 비어 있는 자리입니다(1002).
        assertThat(overview(admin, "24h").get("matches").isNull()).isTrue();
    }

    @Test
    @DisplayName("24시간 범위는 1분 샘플을 그대로 준다")
    void dayRangeReturnsRawSamples() throws Exception {
        Admin admin = login();
        OpsSample sample = sampler.sample();

        JsonNode body = overview(admin, "24h");

        assertThat(body.get("range").asText()).isEqualTo("24h");
        boolean found = false;
        for (JsonNode point : body.get("series")) {
            if (point.get("at").asText().equals(sample.getSampledAt())) {
                found = true;
                assertThat(point.get("inLobby").asInt()).isEqualTo(sample.getInLobby());
            }
        }
        assertThat(found).as("방금 남긴 샘플이 24h 시계열에 있어야 합니다").isTrue();
    }

    @Test
    @DisplayName("7일 범위는 15분 버킷으로 묶고, 인원은 평균 가입은 합이다")
    void weekRangeBucketsByFifteenMinutes() throws Exception {
        Admin admin = login();
        // 24시간 밖, 7일 안의 같은 15분 창에 두 행을 둡니다. 그러면 24h 응답에는 없고 7d 에만 묶여 나옵니다.
        Instant base = Instant.now().minus(Duration.ofDays(2)).truncatedTo(java.time.temporal.ChronoUnit.HOURS);
        String first = Timestamps.format(base.plusSeconds(30));
        String second = Timestamps.format(base.plusSeconds(7 * 60 + 10));
        samples.save(new OpsSample(first, 10, 0, 0, 0, 3, 1, 20.0, 100, 500, 1, 10));
        samples.save(new OpsSample(second, 20, 0, 0, 0, 5, 0, 40.0, 300, 500, 3, 10));

        String bucket = AdminOverviewService.bucketKey(first, 15);
        JsonNode week = overview(admin, "7d");
        assertThat(week.get("range").asText()).isEqualTo("7d");

        JsonNode folded = null;
        for (JsonNode point : week.get("series")) {
            assertThat(point.get("at").asText()).endsWith("00");
            assertThat(Integer.parseInt(point.get("at").asText().substring(10, 12)) % 15).isZero();
            if (point.get("at").asText().equals(bucket)) {
                folded = point;
            }
        }
        assertThat(folded).as("두 행이 한 버킷으로 묶여야 합니다").isNotNull();
        assertThat(folded.get("online").asInt()).isEqualTo(15);
        assertThat(folded.get("signups").asInt()).isEqualTo(8);
        assertThat(folded.get("deletions").asInt()).isEqualTo(1);
        assertThat(folded.get("cpuPct").asDouble()).isEqualTo(30.0);
        assertThat(folded.get("heapUsedMb").asInt()).isEqualTo(200);

        for (JsonNode point : overview(admin, "24h").get("series")) {
            assertThat(point.get("at").asText()).isNotEqualTo(first);
        }
    }

    @Test
    @DisplayName("모르는 범위는 24h 로 읽는다")
    void unknownRangeFallsBackToDay() throws Exception {
        Admin admin = login();

        assertThat(overview(admin, "1y").get("range").asText()).isEqualTo("24h");
    }

    private JsonNode overview(Admin admin, String range) throws Exception {
        String body = mvc.perform(get(OVERVIEW).param("range", range).session(admin.session()))
                .andExpect(status().isOk())
                .andReturn().getResponse().getContentAsString();
        return objectMapper.readTree(body);
    }

    private void suspend(Admin admin, String userId) throws Exception {
        mvc.perform(put("/api/v1/admin/users/{userId}/suspension", userId)
                        .session(admin.session())
                        .cookie(admin.csrf())
                        .header("X-XSRF-TOKEN", admin.csrf().getValue())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"reason\":\"개요 탭 테스트\"}"))
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
        String body = mvc.perform(post("/api/v1/accounts")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"deviceId\":\"" + UUID.randomUUID() + "\"}"))
                .andExpect(status().isCreated())
                .andReturn().getResponse().getContentAsString();
        return objectMapper.readTree(body).get("userId").asText();
    }
}
