package com.ssafy.d205.domain.analytics.query;

import java.util.List;

/**
 * 질문 하나의 결과. 문서의 SQL 이 붙인 별칭이 그대로 컬럼 이름이라 화면은 이 이름을 헤더로 씁니다.
 *
 * <p>행을 객체가 아니라 값 목록으로 두는 이유는 컬럼이 질문마다 다르고, 문서를 고치면 바뀌기 때문입니다.
 * 질문별 DTO 를 두면 문서와 코드가 다시 두 곳이 됩니다.
 *
 * @param columns SQL 의 컬럼 별칭. 문서에 적힌 순서
 * @param rows    행마다 columns 와 같은 길이의 값. 시각은 UTC yyyyMMddHHmmss 문자열, 나머지는 DB 값 그대로
 */
public record AnalyticsTable(List<String> columns, List<List<Object>> rows) {
}
