package com.ssafy.d205.domain.user.repository;

/**
 * 검색 네이티브 쿼리의 결과를 받는 투영입니다.
 *
 * <p>getter 이름이 쿼리의 컬럼 별칭과 같아야 스프링 데이터가 채워줍니다.
 * userId 와 nickname 별칭을 바꾸면 여기도 함께 바꿔야 합니다.
 */
public interface UserSummaryRow {

    String getUserId();

    String getNickname();
}
