package com.ssafy.d205.domain.user.repository;

/**
 * 관리 화면 사용자 목록 네이티브 쿼리의 결과 투영입니다.
 *
 * <p>getter 이름이 쿼리의 컬럼 별칭과 같아야 스프링 데이터가 채워줍니다.
 *
 * <p>기기 식별자(user_identities.provider_user_id)는 여기 없습니다. 그 값은 자격증명이라
 * 운영자 화면에도 나가면 안 됩니다. 이 투영에 그 컬럼을 더하는 변경은 하지 마세요.
 */
public interface AdminUserRow {

    String getUserId();

    String getNickname();

    String getCreatedAt();

    /** 접속 상태 행이 없으면 NULL. 한 번도 붙은 적이 없다는 뜻이고, 화면은 OFFLINE 으로 읽습니다. */
    String getPresence();

    String getLastSeenAt();

    /** 받은 신고 중 운영자가 숨기지 않은 것의 수. */
    int getReportCount();

    /** 수락된 친구 관계 수. 보낸·받은 요청은 세지 않습니다. */
    int getFriendCount();

    String getSuspendedAt();

    String getSuspendedReason();
}
