package com.ssafy.d205.domain.admin.dto;

import com.ssafy.d205.domain.admin.repository.NicknameAuditRow;

/**
 * 운영자가 닉네임을 바꾼 기록 한 줄. 사용자 상세의 개명 이력이 씁니다.
 *
 * @param userId         대상의 공개 식별자. 탈퇴해도 남습니다
 * @param beforeNickname 바꾸기 전 이름
 * @param afterNickname  바꾼 뒤 이름. 지금 닉네임이 아닙니다
 * @param reason         운영자가 적은 사유
 * @param adminUsername  누른 운영자
 * @param actedAt        누른 시각. yyyyMMddHHmmss UTC
 * @param accountDeleted 대상이 탈퇴했으면 true
 */
public record AdminNicknameEntry(
        String userId,
        String beforeNickname,
        String afterNickname,
        String reason,
        String adminUsername,
        String actedAt,
        boolean accountDeleted
) {

    public static AdminNicknameEntry from(NicknameAuditRow row) {
        return new AdminNicknameEntry(
                row.getUserId(),
                row.getBeforeNickname(),
                row.getAfterNickname(),
                row.getReason(),
                row.getAdminUsername(),
                row.getActedAt(),
                // 내부 키는 여기서 버리고 "탈퇴했는가"만 남깁니다.
                row.getUserSeq() == null);
    }
}
