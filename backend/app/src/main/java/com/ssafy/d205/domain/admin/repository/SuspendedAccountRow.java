package com.ssafy.d205.domain.admin.repository;

/**
 * 지금 정지된 계정 한 줄의 투영입니다.
 *
 * <p>정지 시각과 사유는 users 에서, 처리자는 마지막 정지 감사 행에서 옵니다. 그래서 처리자만
 * null 일 수 있습니다 - 감사 로그가 생기기 전에 정지된 계정입니다.
 */
public interface SuspendedAccountRow {

    String getUserId();

    String getNickname();

    String getSuspendedAt();

    String getReason();

    /** 마지막으로 정지를 누른 운영자. 감사 로그 이전의 정지면 null 입니다. */
    String getAdminUsername();
}
