package com.ssafy.d205.domain.admin.dto;

import com.ssafy.d205.domain.user.repository.AdminUserRow;

/**
 * 관리 화면 사용자 목록의 한 줄.
 *
 * @param userId          공개 식별자
 * @param nickname        지금 닉네임. 바뀔 수 있으니 표시에만 씁니다
 * @param createdAt       가입 시각. yyyyMMddHHmmss UTC
 * @param presence        OFFLINE, ONLINE, IN_LOBBY, IN_GAME 중 하나. 한 번도 붙은 적 없으면 OFFLINE
 * @param lastSeenAt      마지막으로 살아 있음을 확인한 시각. 접속 기록이 없으면 null
 * @param reportCount     받은 신고 중 숨기지 않은 것의 수
 * @param friendCount     수락된 친구 수
 * @param suspended       정지 여부
 * @param suspendedAt     정지 시각. 정지가 아니면 null
 * @param suspendedReason 정지 사유. 운영자만 보는 화면이라 그대로 담습니다. 게임 API 응답에는 절대 담지 않습니다
 */
public record AdminUserSummary(
        String userId,
        String nickname,
        String createdAt,
        String presence,
        String lastSeenAt,
        int reportCount,
        int friendCount,
        boolean suspended,
        String suspendedAt,
        String suspendedReason
) {

    public static AdminUserSummary from(AdminUserRow row) {
        return new AdminUserSummary(
                row.getUserId(),
                row.getNickname(),
                row.getCreatedAt(),
                row.getPresence() == null ? "OFFLINE" : row.getPresence(),
                row.getLastSeenAt(),
                row.getReportCount(),
                row.getFriendCount(),
                row.getSuspendedAt() != null,
                row.getSuspendedAt(),
                row.getSuspendedReason());
    }
}
