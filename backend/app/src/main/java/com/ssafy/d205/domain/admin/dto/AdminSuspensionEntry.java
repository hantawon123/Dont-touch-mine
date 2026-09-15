package com.ssafy.d205.domain.admin.dto;

import com.ssafy.d205.domain.admin.repository.SuspensionAuditRow;

/**
 * 감사 로그 한 줄. 사용자 상세의 정지 이력과 정지 탭의 최근 해제 이력이 같은 모양을 씁니다.
 *
 * @param userId         대상의 공개 식별자. 탈퇴해도 남습니다
 * @param nickname       누른 시점의 닉네임. 지금 닉네임이 아닙니다
 * @param action         SUSPEND 또는 LIFT
 * @param reason         정지 사유. 해제는 null
 * @param adminUsername  누른 운영자
 * @param actedAt        누른 시각. yyyyMMddHHmmss UTC
 * @param accountDeleted 대상이 탈퇴했으면 true
 */
public record AdminSuspensionEntry(
        String userId,
        String nickname,
        String action,
        String reason,
        String adminUsername,
        String actedAt,
        boolean accountDeleted
) {

    public static AdminSuspensionEntry from(SuspensionAuditRow row) {
        return new AdminSuspensionEntry(
                row.getUserId(),
                row.getNickname(),
                row.getAction(),
                row.getReason(),
                row.getAdminUsername(),
                row.getActedAt(),
                // 내부 키는 여기서 버리고 "탈퇴했는가"만 남깁니다.
                row.getUserSeq() == null);
    }
}
