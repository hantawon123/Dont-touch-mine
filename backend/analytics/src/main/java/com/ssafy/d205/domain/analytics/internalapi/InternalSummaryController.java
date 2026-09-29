package com.ssafy.d205.domain.analytics.internalapi;

import lombok.RequiredArgsConstructor;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.time.LocalDateTime;
import java.time.ZoneId;
import java.time.ZoneOffset;

/**
 * 관리 화면 개요 탭의 경기 통계 카드. 계정 서비스가 대신 불러 화면에 끼웁니다 (S15P21D205-1002).
 *
 * <p>브라우저가 이 서비스를 직접 부르지 않는 이유는 관리자 세션이 계정 서비스에만 있어서입니다. 여기는
 * 공유 키만 봅니다.
 *
 * <p>"오늘"은 Asia/Seoul 기준입니다. 개요 탭의 다른 카드와 같은 기준이어야 합니다. 저장은 UTC DATETIME 이라
 * 경계 시각을 UTC 로 바꿔 비교합니다.
 *
 * <p>집계는 match_analysis_summary 뷰를 씁니다. 그 뷰가 "호스트가 보낸 v2 이벤트"만 세는 규칙을 이미
 * 갖고 있어 여기서 다시 적지 않습니다. 진행 중 경기는 match_end 가 아직 없고 시작 후 30분이 안 지난 것입니다.
 * 30분은 숨기기 120초 + 찾기 15분의 상한을 넉넉히 덮는 값이고, 그 뒤에도 끝 이벤트가 없으면 호스트가
 * 끊긴 것으로 봅니다.
 */
@RestController
@RequestMapping("/internal/admin")
@RequiredArgsConstructor
public class InternalSummaryController {

    static final ZoneId OPERATOR_ZONE = ZoneId.of("Asia/Seoul");
    static final Duration IN_PROGRESS_WINDOW = Duration.ofMinutes(30);

    private final JdbcTemplate jdbcTemplate;
    private final Clock clock;

    @GetMapping("/summary")
    public MatchSummary summary() {
        Instant now = clock.instant();
        LocalDateTime todayStartUtc = now.atZone(OPERATOR_ZONE).toLocalDate()
                .atStartOfDay(OPERATOR_ZONE).toInstant().atOffset(ZoneOffset.UTC).toLocalDateTime();
        LocalDateTime inProgressSince = now.minus(IN_PROGRESS_WINDOW).atOffset(ZoneOffset.UTC).toLocalDateTime();

        Long matchesToday = jdbcTemplate.queryForObject("""
                SELECT COUNT(*) FROM match_analysis_summary WHERE started_at_utc >= ?
                """, Long.class, todayStartUtc);

        Long inProgress = jdbcTemplate.queryForObject("""
                SELECT COUNT(*) FROM match_analysis_summary
                 WHERE end_reason IS NULL AND started_at_utc >= ?
                """, Long.class, inProgressSince);

        Double avgDurationSec = jdbcTemplate.queryForObject("""
                SELECT AVG(duration_seconds) FROM match_analysis_summary
                 WHERE end_reason IS NOT NULL AND started_at_utc >= ?
                """, Double.class, todayStartUtc);

        // 이탈률: 시작 인원 대비 결과 이벤트가 없는 인원. 끝난 경기만 셉니다.
        Double dropoutRate = jdbcTemplate.queryForObject("""
                SELECT CASE WHEN SUM(s.player_count) IS NULL OR SUM(s.player_count) = 0 THEN NULL
                            ELSE 1 - SUM(r.results) / SUM(s.player_count) END
                  FROM match_analysis_summary s
                  LEFT JOIN (SELECT match_id, COUNT(*) AS results
                               FROM game_event
                              WHERE event_name = 'player_result' AND from_host = 1
                              GROUP BY match_id) r ON r.match_id = s.match_id
                 WHERE s.end_reason IS NOT NULL AND s.started_at_utc >= ?
                """, Double.class, todayStartUtc);

        return new MatchSummary(matchesToday, inProgress,
                avgDurationSec == null ? null : Math.round(avgDurationSec * 10) / 10.0,
                dropoutRate == null ? null : Math.round(dropoutRate * 1000) / 1000.0);
    }

    /**
     * @param matchesToday   오늘 시작한 경기 수
     * @param inProgress     시작 후 30분 안이고 아직 끝 이벤트가 없는 경기 수
     * @param avgDurationSec 오늘 끝난 경기의 평균 길이(초). 끝난 경기가 없으면 null
     * @param dropoutRate    오늘 끝난 경기에서 시작 인원 대비 결과 없이 사라진 비율 0~1. 없으면 null
     */
    public record MatchSummary(long matchesToday, long inProgress, Double avgDurationSec, Double dropoutRate) {
    }
}
