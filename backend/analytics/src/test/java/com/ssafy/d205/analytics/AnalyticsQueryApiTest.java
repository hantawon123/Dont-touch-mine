package com.ssafy.d205.analytics;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.test.context.TestPropertySource;
import org.springframework.test.web.servlet.MockMvc;
import tools.jackson.databind.JsonNode;
import tools.jackson.databind.ObjectMapper;

import java.time.Instant;
import java.time.LocalDateTime;
import java.time.ZoneOffset;
import java.time.temporal.ChronoUnit;
import java.util.List;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.ssafy.d205.domain.analytics.internalapi.InternalKeyFilter;
import com.ssafy.d205.global.common.Timestamps;
import com.ssafy.d205.support.AnalyticsIntegrationTest;

/**
 * 관리 화면 분석 탭의 조회 API (S15P21D205-976). 문서의 SQL 이 실제 MySQL 에서 돌고, 필터가 표식 자리에
 * 들어가 결과를 좁히는지 봅니다.
 *
 * <p>한글 컬럼 이름을 비교하므로 응답을 바이트로 받아 UTF-8 로 읽습니다. MockMvc 의 getContentAsString 은
 * 응답에 charset 이 없으면 ISO-8859-1 로 읽어 한글이 깨집니다.
 */
@TestPropertySource(properties = "internal.key=test-internal-key")
class AnalyticsQueryApiTest extends AnalyticsIntegrationTest {

    private static final String KEY = "test-internal-key";
    private static final List<String> QUESTIONS =
            List.of("matches", "hiding-time", "hideouts", "dwell", "seeking-time", "combat", "item-life", "dropout");

    @Autowired
    MockMvc mvc;

    @Autowired
    JdbcTemplate analytics;

    @Autowired
    ObjectMapper objectMapper;

    @Test
    @DisplayName("키가 없거나 틀리면 404 다")
    void wrongKeyLooksLikeNothingIsThere() throws Exception {
        mvc.perform(get("/internal/admin/analytics/matches")).andExpect(status().isNotFound());
        mvc.perform(get("/internal/admin/analytics/matches").header(InternalKeyFilter.HEADER, "wrong"))
                .andExpect(status().isNotFound());
        mvc.perform(get("/internal/admin/analytics/positions").param("matchId", "x"))
                .andExpect(status().isNotFound());
    }

    @Test
    @DisplayName("문서에 없는 질문 이름은 404 QUESTION_NOT_FOUND 다")
    void unknownQuestionIsNotFound() throws Exception {
        mvc.perform(get("/internal/admin/analytics/no-such-question").header(InternalKeyFilter.HEADER, KEY))
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.code").value("QUESTION_NOT_FOUND"));
    }

    @Test
    @DisplayName("문서의 질문 여덟 개가 필터 없이도, 필터 셋을 다 걸어도 실제 MySQL 에서 돈다")
    void everyQuestionRunsWithAndWithoutFilters() throws Exception {
        for (String question : QUESTIONS) {
            JsonNode plain = table(get("/internal/admin/analytics/" + question));
            assertThat(plain.get("columns").size()).as(question + " 의 컬럼").isPositive();
            assertThat(plain.get("rows").isArray()).as(question + " 의 rows").isTrue();

            JsonNode filtered = table(get("/internal/admin/analytics/" + question)
                    .param("from", "20260901000000")
                    .param("to", "20261001000000")
                    .param("matchId", UUID.randomUUID().toString()));
            assertThat(filtered.get("columns")).as(question + " 의 필터 결과 컬럼").isEqualTo(plain.get("columns"));
            assertThat(filtered.get("rows").size()).as("없는 경기로 좁히면 빈 표").isZero();
        }
    }

    @Test
    @DisplayName("경기 목록은 matchId 와 기간으로 좁혀지고, 시각은 UTC 14자 문자열로 나온다")
    void matchesAreNarrowedByFilters() throws Exception {
        String recent = UUID.randomUUID().toString();
        String old = UUID.randomUUID().toString();
        Instant recentStart = Instant.now().minus(1, ChronoUnit.HOURS).truncatedTo(ChronoUnit.SECONDS);
        Instant oldStart = Instant.now().minus(10, ChronoUnit.DAYS).truncatedTo(ChronoUnit.SECONDS);
        insertHostEvent(recent, "match_start", recentStart, 0, 1.0f, 2.0f, 0,
                "{\"player_count\":4,\"hide_sec\":30,\"seek_sec\":300,\"stun_hits\":3,\"partial\":false}");
        insertHostEvent(old, "match_start", oldStart, 0, 1.0f, 2.0f, 0,
                "{\"player_count\":2,\"hide_sec\":30,\"seek_sec\":300,\"stun_hits\":3,\"partial\":false}");

        JsonNode one = table(get("/internal/admin/analytics/matches").param("matchId", recent));
        assertThat(one.get("columns").get(0).asText()).isEqualTo("경기");
        assertThat(one.get("columns").get(3).asText()).isEqualTo("시작(UTC)");
        assertThat(one.get("rows").size()).isEqualTo(1);
        assertThat(one.get("rows").get(0).get(0).asText()).isEqualTo(recent);
        assertThat(one.get("rows").get(0).get(3).asText()).isEqualTo(Timestamps.format(recentStart));
        assertThat(one.get("rows").get(0).get(4).asInt()).isEqualTo(4);

        JsonNode window = table(get("/internal/admin/analytics/matches")
                .param("from", Timestamps.format(Instant.now().minus(2, ChronoUnit.HOURS)))
                .param("to", Timestamps.format(Instant.now())));
        List<String> ids = firstColumn(window);
        assertThat(ids).contains(recent).doesNotContain(old);

        JsonNode none = table(get("/internal/admin/analytics/matches").param("matchId", UUID.randomUUID().toString()));
        assertThat(none.get("rows").size()).isZero();
    }

    @Test
    @DisplayName("필터 값이 14자가 아니거나 날짜가 아니면 400 INVALID_REQUEST 다")
    void badFiltersAreRejected() throws Exception {
        mvc.perform(get("/internal/admin/analytics/matches").header(InternalKeyFilter.HEADER, KEY).param("from", "abc"))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("INVALID_REQUEST"));
        mvc.perform(get("/internal/admin/analytics/matches").header(InternalKeyFilter.HEADER, KEY).param("to", "20261399000000"))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("INVALID_REQUEST"));
    }

    @Test
    @DisplayName("좌표는 한 경기의 위치 샘플만 시간순으로 준다")
    void positionsBelongToOneMatch() throws Exception {
        String match = UUID.randomUUID().toString();
        String other = UUID.randomUUID().toString();
        Instant start = Instant.now().minusSeconds(300);
        insertHostEvent(match, "position_sample", start.plusSeconds(2), 2_000, 10.5f, -3.25f, 1, "{\"seat\":1}");
        insertHostEvent(match, "position_sample", start.plusSeconds(1), 1_000, 1.0f, 2.0f, 0, "{\"seat\":0}");
        insertHostEvent(match, "position_sample", start.plusSeconds(1), 1_000, 4.0f, 5.0f, 1, "{\"seat\":1}");
        insertHostEvent(other, "position_sample", start.plusSeconds(1), 1_000, 9.0f, 9.0f, 0, "{\"seat\":0}");

        JsonNode table = table(get("/internal/admin/analytics/positions").param("matchId", match));

        assertThat(table.get("columns")).extracting(JsonNode::asText)
                .containsExactly("map_id", "player_seat", "phase", "elapsed_seconds", "pos_x", "pos_z");
        assertThat(table.get("rows").size()).isEqualTo(3);
        // 1초의 자리 0, 1초의 자리 1, 2초의 자리 1 순서.
        assertThat(table.get("rows").get(0).get(1).asInt()).isZero();
        assertThat(table.get("rows").get(0).get(4).asDouble()).isEqualTo(1.0);
        assertThat(table.get("rows").get(2).get(1).asInt()).isEqualTo(1);
        assertThat(table.get("rows").get(2).get(5).asDouble()).isEqualTo(-3.25);
        assertThat(table.get("rows").get(0).get(2).asText()).isEqualTo("Searching");
    }

    @Test
    @DisplayName("좌표는 matchId 없이 부를 수 없다")
    void positionsRequireAMatch() throws Exception {
        mvc.perform(get("/internal/admin/analytics/positions").header(InternalKeyFilter.HEADER, KEY))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("INVALID_REQUEST"));
    }

    private JsonNode table(org.springframework.test.web.servlet.request.MockHttpServletRequestBuilder request) throws Exception {
        byte[] body = mvc.perform(request.header(InternalKeyFilter.HEADER, KEY))
                .andExpect(status().isOk())
                .andReturn().getResponse().getContentAsByteArray();
        return objectMapper.readTree(body);
    }

    private static List<String> firstColumn(JsonNode table) {
        List<String> values = new java.util.ArrayList<>();
        table.get("rows").forEach(row -> values.add(row.get(0).asText()));
        return values;
    }

    private void insertHostEvent(String matchId, String name, Instant at, long matchTimeMs,
                                 float posX, float posZ, int seat, String params) {
        analytics.update("""
                INSERT INTO game_event (occurred_at, received_at, client_session_id, client_seq, room_code, match_id,
                    match_time_ms, user_public_id, event_name, phase, map_id, pos_x, pos_y, pos_z, from_host, schema_ver, params)
                VALUES (?, ?, ?, ?, 'ABC234', ?, ?, ?, ?, 'Searching', 'basement', ?, 0, ?, 1, 2, ?)
                """, utc(at), utc(at), UUID.randomUUID().toString(), (long) seat, matchId, matchTimeMs,
                UUID.randomUUID().toString(), name, posX, posZ, params);
    }

    private static LocalDateTime utc(Instant at) {
        return at.atOffset(ZoneOffset.UTC).toLocalDateTime();
    }
}
