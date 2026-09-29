package com.ssafy.d205.domain.admin.dto;

import java.time.LocalDate;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;

/**
 * 관리 화면의 분포 표 하나 (S15P21D205-1004). 사유별 건수, 날짜별 건수 같은 것입니다.
 *
 * <p>모양은 분석 탭의 {@link AdminAnalyticsTable} 과 같은 {@code columns}·{@code rows} 라서 화면이
 * 같은 표·차트 그리기 코드를 씁니다. {@code unavailable} 이 없는 이유는 이 표는 이 서버의 DB 에서
 * 바로 나오는 것이라 "답하지 않았다"는 상태가 없기 때문입니다.
 *
 * @param columns 컬럼 이름(한글). 화면이 헤더로 씁니다
 * @param rows    행마다 columns 와 같은 길이의 값
 */
public record AdminCountTable(List<String> columns, List<List<Object>> rows) {

    /** 이름·건수 두 컬럼 표. 이름이 null 이면 {@code blank} 로 바꿔 씁니다. */
    public static AdminCountTable of(String labelColumn, Map<String, Integer> counts, String blank) {
        List<List<Object>> rows = new ArrayList<>();
        counts.forEach((label, count) -> rows.add(List.of(label == null ? blank : label, count)));
        return new AdminCountTable(List.of(labelColumn, "건수"), rows);
    }

    /**
     * 날짜별 건수 표. <b>기간 안의 모든 날짜가 들어가고 없는 날은 0</b> 입니다.
     *
     * <p>있는 날만 돌려주면 선 차트가 빈 날을 건너뛰어 이어 그리고, 그러면 "그날 0건"이 "그날은
     * 없었다"처럼 보이지 않고 두 점 사이 어딘가로 읽힙니다. 없는 날이 0 인 것이 정보입니다.
     *
     * @param counts 날짜(yyyy-MM-dd) → 건수. 기간 밖의 키는 무시합니다
     * @param from   첫 날(포함)
     * @param to     마지막 날(포함)
     */
    public static AdminCountTable daily(Map<String, Integer> counts, LocalDate from, LocalDate to) {
        List<List<Object>> rows = new ArrayList<>();
        for (LocalDate day = from; !day.isAfter(to); day = day.plusDays(1)) {
            String key = day.toString();
            rows.add(List.of(key, counts.getOrDefault(key, 0)));
        }
        return new AdminCountTable(List.of("날짜(KST)", "건수"), rows);
    }
}
