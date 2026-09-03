package com.ssafy.d205.domain.friend.repository;

/**
 * 차단 목록 네이티브 쿼리의 결과를 받는 투영입니다.
 * getter 이름이 쿼리의 컬럼 별칭과 같아야 스프링 데이터가 채워줍니다.
 */
public interface BlockedUserRow {

    String getUserId();

    String getNickname();

    String getBlockedAt();
}
