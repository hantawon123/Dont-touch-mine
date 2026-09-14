package com.ssafy.d205.domain.friend.repository;

/**
 * 친구 목록 네이티브 쿼리의 결과를 받는 투영입니다.
 *
 * <p>status 와 heartbeatAt 이 문자열이고 null 일 수 있습니다. 친구가 한 번도 접속하지
 * 않았으면 user_presence 에 행이 없어 LEFT JOIN 이 null 을 줍니다. 실제 상태 계산은
 * PresenceTimeout 이 담당합니다.
 */
public interface FriendSummaryRow {

    String getUserId();

    String getNickname();

    String getStatus();

    String getHeartbeatAt();
}
