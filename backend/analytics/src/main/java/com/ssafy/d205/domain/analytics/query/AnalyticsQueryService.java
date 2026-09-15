package com.ssafy.d205.domain.analytics.query;

import lombok.RequiredArgsConstructor;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.jdbc.core.ResultSetExtractor;
import org.springframework.stereotype.Service;

import java.sql.ResultSetMetaData;
import java.sql.Timestamp;
import java.time.LocalDateTime;
import java.time.ZoneOffset;
import java.util.ArrayList;
import java.util.List;

import com.ssafy.d205.global.common.Timestamps;

/**
 * 관리 화면 분석 탭이 보는 표 둘 - 문서의 질문과 히트맵용 좌표 (S15P21D205-976).
 *
 * <p>질문은 {@link DashboardQueryDocument} 의 SQL 에 필터를 끼워 그대로 실행합니다. 이 클래스는 SQL 을
 * 알지 못하고 결과의 모양만 정합니다. 좌표 쿼리만 여기 있습니다. 그건 Metabase 화면이 아니라 브라우저의
 * canvas 가 그리는 것이라 문서의 "질문"이 아니기 때문입니다.
 */
@Service
@RequiredArgsConstructor
public class AnalyticsQueryService {

    /**
     * 경기 하나의 좌표 상한. 4 인 경기가 20 분이면 샘플이 4,800 개이고, 수집 쪽 상한(경기당 약 20,000 건)과
     * 같은 값입니다. 그 위로는 브라우저가 격자로 묶어 그려도 응답이 무거워집니다.
     */
    static final int POSITION_LIMIT = 20_000;

    private static final String POSITIONS_SQL = """
            SELECT map_id, player_seat, phase, elapsed_seconds, pos_x, pos_z
              FROM match_analysis_positions
             WHERE match_id = ?
             ORDER BY elapsed_seconds, player_seat
             LIMIT %d
            """.formatted(POSITION_LIMIT);

    private final DashboardQueryDocument document;
    private final JdbcTemplate jdbcTemplate;

    public AnalyticsTable question(String slug, AnalyticsQueryFilter filter) {
        DashboardQueryDocument.Question question = document.find(slug)
                .orElseThrow(() -> new UnknownAnalyticsQuestionException(slug));
        return jdbcTemplate.query(question.render(filter.conditions()), TABLE, filter.arguments());
    }

    public AnalyticsTable positions(String matchId) {
        return jdbcTemplate.query(POSITIONS_SQL, TABLE, matchId);
    }

    /** 결과 집합을 컬럼 이름 목록과 값 행으로 옮깁니다. 컬럼 이름은 SQL 의 별칭(getColumnLabel)입니다. */
    static final ResultSetExtractor<AnalyticsTable> TABLE = rs -> {
        ResultSetMetaData meta = rs.getMetaData();
        int width = meta.getColumnCount();
        List<String> columns = new ArrayList<>(width);
        for (int i = 1; i <= width; i++) {
            columns.add(meta.getColumnLabel(i));
        }
        List<List<Object>> rows = new ArrayList<>();
        while (rs.next()) {
            List<Object> row = new ArrayList<>(width);
            for (int i = 1; i <= width; i++) {
                row.add(cell(rs.getObject(i)));
            }
            rows.add(row);
        }
        return new AnalyticsTable(List.copyOf(columns), rows);
    };

    /**
     * DATETIME 만 손봅니다. 드라이버가 LocalDateTime 으로 주든 Timestamp 로 주든 다른 API 와 같은 UTC 14자
     * 문자열로 내보냅니다. 숫자는 그대로입니다 - ROUND 의 DECIMAL 도 JSON 숫자로 나갑니다.
     */
    private static Object cell(Object value) {
        if (value instanceof LocalDateTime at) {
            return Timestamps.format(at.toInstant(ZoneOffset.UTC));
        }
        if (value instanceof Timestamp at) {
            return Timestamps.format(at.toLocalDateTime().toInstant(ZoneOffset.UTC));
        }
        return value;
    }
}
