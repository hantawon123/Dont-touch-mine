package com.ssafy.d205.domain.analytics.query;

import java.time.LocalDateTime;
import java.time.format.DateTimeFormatter;
import java.time.format.DateTimeParseException;
import java.util.ArrayList;
import java.util.List;

/**
 * 질문 SQL 의 {@code /* @filter *&#47;} 자리에 들어가는 기간·경기 조건.
 *
 * <p>조건은 전부 {@code match_analysis_summary} 의 컬럼({@code started_at_utc}, {@code match_id},
 * {@code map_id})입니다. 문서의 각 절이 그 뷰에서 경기를 고르는 {@code WHERE} 에 표식을 두므로, 여기서
 * 만드는 조각은 그 컬럼만 쓸 수 있습니다. 시각은 저장과 같은 UTC 이고 {@code to} 는 미포함입니다.
 *
 * <p>맵 조건이 있는 이유는 맵이 둘이 되었기 때문입니다 (S15P21D205-1085). 맵마다 크기도 동선도 다르므로
 * 두 맵을 합친 평균은 어느 맵의 값도 아닙니다. 히트맵은 한 번에 한 맵의 평면도 위에만 그릴 수 있으니
 * 특히 그렇습니다.
 *
 * @param fromUtc 이 시각 이후(포함)에 시작한 경기. null 이면 제한 없음
 * @param toUtc   이 시각 전(미포함)에 시작한 경기. null 이면 제한 없음
 * @param matchId 이 경기 하나. null 이면 전체
 * @param mapId   이 맵에서 한 경기만. null 이면 전체
 */
public record AnalyticsQueryFilter(LocalDateTime fromUtc, LocalDateTime toUtc, String matchId, String mapId) {

    public static final AnalyticsQueryFilter NONE = new AnalyticsQueryFilter(null, null, null, null);

    /** 다른 API 와 같은 UTC 14자(yyyyMMddHHmmss). 컨트롤러가 자릿수는 먼저 걸러 주고 여기서는 날짜 자체를 봅니다. */
    private static final DateTimeFormatter COMPACT_UTC = DateTimeFormatter.ofPattern("yyyyMMddHHmmss");

    public static AnalyticsQueryFilter parse(String from, String to, String matchId, String mapId) {
        return new AnalyticsQueryFilter(parseUtc("from", from), parseUtc("to", to),
                blankToNull(matchId), blankToNull(mapId));
    }

    /** 표식 자리에 들어갈 SQL 조각. 앞에 {@code AND} 가 붙어 있어 기존 WHERE 뒤에 그대로 이어집니다. */
    public String conditions() {
        StringBuilder sql = new StringBuilder();
        if (fromUtc != null) {
            sql.append(" AND started_at_utc >= ?");
        }
        if (toUtc != null) {
            sql.append(" AND started_at_utc < ?");
        }
        if (matchId != null) {
            sql.append(" AND match_id = ?");
        }
        if (mapId != null) {
            sql.append(" AND map_id = ?");
        }
        return sql.toString();
    }

    /** {@link #conditions()} 의 물음표와 같은 순서의 값. */
    public Object[] arguments() {
        List<Object> args = new ArrayList<>(4);
        if (fromUtc != null) {
            args.add(fromUtc);
        }
        if (toUtc != null) {
            args.add(toUtc);
        }
        if (matchId != null) {
            args.add(matchId);
        }
        if (mapId != null) {
            args.add(mapId);
        }
        return args.toArray();
    }

    private static LocalDateTime parseUtc(String name, String value) {
        if (value == null || value.isBlank()) {
            return null;
        }
        try {
            return LocalDateTime.parse(value, COMPACT_UTC);
        } catch (DateTimeParseException e) {
            throw new InvalidAnalyticsFilterException(name + " 은 UTC yyyyMMddHHmmss 14자여야 합니다: " + value);
        }
    }

    private static String blankToNull(String value) {
        return value == null || value.isBlank() ? null : value;
    }
}
