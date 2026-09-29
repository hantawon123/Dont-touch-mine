package com.ssafy.d205.analytics;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.test.context.TestPropertySource;
import org.springframework.test.web.servlet.MockMvc;

import java.time.Instant;
import java.time.LocalDateTime;
import java.time.ZoneOffset;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.ssafy.d205.domain.analytics.internalapi.InternalKeyFilter;
import com.ssafy.d205.support.AnalyticsIntegrationTest;

/**
 * 계정 서비스만 부르는 내부 API (S15P21D205-1002). 공유 키, 탈퇴 익명화, 경기 통계.
 *
 * <p>키를 테스트 프로퍼티로 넣습니다. 비어 있으면 전부 404 라 설정 없이 돌리면 이 테스트가 통째로 의미가
 * 없어집니다. 그 동작 자체도 아래에서 봅니다.
 */
@TestPropertySource(properties = "internal.key=test-internal-key")
class InternalApiTest extends AnalyticsIntegrationTest {

    private static final String KEY = "test-internal-key";

    @Autowired
    MockMvc mvc;

    @Autowired
    JdbcTemplate analytics;

    @Test
    @DisplayName("키가 없거나 틀리면 404 다. 경로가 있다는 것도 알려주지 않는다")
    void wrongKeyLooksLikeNothingIsThere() throws Exception {
        mvc.perform(get("/internal/admin/summary")).andExpect(status().isNotFound());
        mvc.perform(get("/internal/admin/summary").header(InternalKeyFilter.HEADER, "wrong"))
                .andExpect(status().isNotFound());
        mvc.perform(delete("/internal/users/{id}/events", UUID.randomUUID()))
                .andExpect(status().isNotFound());
    }

    @Test
    @DisplayName("공개 경로는 키 없이 그대로다")
    void publicPathsAreUntouched() throws Exception {
        mvc.perform(get("/actuator/health")).andExpect(status().isOk());
    }

    @Test
    @DisplayName("탈퇴 익명화는 그 사람의 행에서 userId 만 비우고 행은 남긴다")
    void erasingBlanksTheUserButKeepsRows() throws Exception {
        String leaver = UUID.randomUUID().toString();
        String other = UUID.randomUUID().toString();
        String match = UUID.randomUUID().toString();
        insertEvent(match, leaver, "position_sample", Instant.now());
        insertEvent(match, leaver, "player_result", Instant.now());
        insertEvent(match, other, "position_sample", Instant.now());

        mvc.perform(delete("/internal/users/{id}/events", leaver).header(InternalKeyFilter.HEADER, KEY))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.erased").value(2));

        assertThat(count("SELECT COUNT(*) FROM game_event WHERE user_public_id = ?", leaver)).isZero();
        assertThat(count("SELECT COUNT(*) FROM game_event WHERE match_id = ?", match)).isEqualTo(3);
        assertThat(count("SELECT COUNT(*) FROM game_event WHERE user_public_id = ?", other)).isOne();

        // 멱등. 계정 서비스가 실패로 보고 다시 불러도 안전해야 합니다.
        mvc.perform(delete("/internal/users/{id}/events", leaver).header(InternalKeyFilter.HEADER, KEY))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.erased").value(0));
    }

    @Test
    @DisplayName("경기가 없으면 통계는 0 과 null 이다")
    void summaryWithNoMatchesIsZero() throws Exception {
        // 다른 테스트가 넣은 경기가 있을 수 있어 정확한 0 은 못 보지만, 모양과 타입은 고정합니다.
        mvc.perform(get("/internal/admin/summary").header(InternalKeyFilter.HEADER, KEY))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.matchesToday").isNumber())
                .andExpect(jsonPath("$.inProgress").isNumber());
    }

    @Test
    @DisplayName("오늘 시작해 끝난 경기는 오늘 경기 수와 평균 길이에 들고, 진행 중에는 들지 않는다")
    void aFinishedMatchCountsToday() throws Exception {
        String match = UUID.randomUUID().toString();
        String host = UUID.randomUUID().toString();
        Instant start = Instant.now().minusSeconds(600);
        insertHostEvent(match, host, "match_start", start, 0,
                "{\"player_count\":4,\"hide_sec\":30,\"seek_sec\":300,\"stun_hits\":3,\"partial\":false}");
        insertHostEvent(match, host, "player_result", start.plusSeconds(400), 400_000, "{\"result\":\"ESCAPED\"}");
        insertHostEvent(match, host, "player_result", start.plusSeconds(400), 400_000, "{\"result\":\"CAUGHT\"}");
        insertHostEvent(match, host, "match_end", start.plusSeconds(400), 400_000,
                "{\"end_reason\":\"TIME_UP\",\"expected_events\":4,\"partial\":false}");

        long before = matchesToday();
        String body = mvc.perform(get("/internal/admin/summary").header(InternalKeyFilter.HEADER, KEY))
                .andExpect(status().isOk())
                .andReturn().getResponse().getContentAsString();

        assertThat(body).contains("\"matchesToday\":" + before);
        // 시작 인원 4 중 결과 2 → 이탈률 0.5 가 이 경기의 몫입니다. 다른 경기가 섞이면 값은 달라질 수 있어
        // 0~1 범위만 봅니다. 길이는 400초짜리 경기가 하나 이상 있으니 양수입니다.
        assertThat(body).containsPattern("\"avgDurationSec\":[0-9]");
        assertThat(body).containsPattern("\"dropoutRate\":(0|1|0\\.[0-9]+)");
    }

    private long matchesToday() {
        LocalDateTime todayStartUtc = Instant.now().atZone(java.time.ZoneId.of("Asia/Seoul")).toLocalDate()
                .atStartOfDay(java.time.ZoneId.of("Asia/Seoul")).toInstant().atOffset(ZoneOffset.UTC).toLocalDateTime();
        return count("SELECT COUNT(*) FROM match_analysis_summary WHERE started_at_utc >= ?", todayStartUtc);
    }

    private long count(String sql, Object arg) {
        Long n = analytics.queryForObject(sql, Long.class, arg);
        return n == null ? 0 : n;
    }

    private void insertEvent(String matchId, String userId, String name, Instant at) {
        analytics.update("""
                INSERT INTO game_event (occurred_at, received_at, client_session_id, client_seq, room_code, match_id,
                    match_time_ms, user_public_id, event_name, phase, map_id, from_host, schema_ver, params)
                VALUES (?, ?, ?, ?, 'ABC234', ?, 0, ?, ?, 'Searching', 'basement', 0, 2, NULL)
                """, utc(at), utc(at), UUID.randomUUID().toString(), 0L, matchId, userId, name);
    }

    private void insertHostEvent(String matchId, String userId, String name, Instant at, long matchTimeMs, String params) {
        analytics.update("""
                INSERT INTO game_event (occurred_at, received_at, client_session_id, client_seq, room_code, match_id,
                    match_time_ms, user_public_id, event_name, phase, map_id, from_host, schema_ver, params)
                VALUES (?, ?, ?, ?, 'ABC234', ?, ?, ?, ?, 'Searching', 'basement', 1, 2, ?)
                """, utc(at), utc(at), UUID.randomUUID().toString(), 0L, matchId, matchTimeMs, userId, name, params);
    }

    private static LocalDateTime utc(Instant at) {
        return at.atOffset(ZoneOffset.UTC).toLocalDateTime();
    }
}
