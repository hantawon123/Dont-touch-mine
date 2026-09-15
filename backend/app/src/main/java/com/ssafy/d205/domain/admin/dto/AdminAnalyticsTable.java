package com.ssafy.d205.domain.admin.dto;

import java.util.List;

/**
 * 관리 화면 분석 탭의 표 하나. 분석 서비스의 응답에 {@code unavailable} 만 덧붙인 모양입니다 (S15P21D205-976).
 *
 * <p>컬럼 이름은 {@code docs/analytics-dashboards.md} 의 SQL 별칭 그대로(한글)이고, 화면은 그것을 헤더로
 * 씁니다. 행은 값 목록입니다 - 질문마다 컬럼이 다르고 문서를 고치면 바뀌므로 질문별 필드를 두지 않습니다.
 *
 * @param columns     컬럼 이름. 분석 서비스가 없으면 빈 목록
 * @param rows        행마다 columns 와 같은 길이의 값. 시각은 UTC yyyyMMddHHmmss 문자열
 * @param unavailable 분석 서비스가 답하지 않아 비어 있는 것인지. false 인데 rows 가 비면 데이터가 정말 없는 것
 */
public record AdminAnalyticsTable(List<String> columns, List<List<Object>> rows, boolean unavailable) {

    /** 분석 서비스가 답하지 않았을 때의 빈 표. 이름이 컴포넌트와 겹치면 레코드가 거절해 접두어를 붙였습니다. */
    public static AdminAnalyticsTable whenUnavailable() {
        return new AdminAnalyticsTable(List.of(), List.of(), true);
    }
}
