package com.ssafy.d205.domain.admin.dto;

import com.ssafy.d205.domain.admin.repository.SuspendedAccountRow;

/**
 * 지금 정지된 계정 한 줄.
 *
 * <p>{@link AdminSuspensionEntry} 와 다릅니다. 저쪽은 "무슨 일이 있었다"는 기록이고 이쪽은
 * "지금 이 상태다"입니다. 그래서 닉네임도 이쪽은 지금 닉네임입니다 - 운영자가 지금 이 사람을
 * 찾을 때 쓰는 이름이어야 합니다.
 *
 * @param adminUsername 마지막으로 정지를 누른 운영자. 감사 로그 이전의 정지면 null
 */
public record AdminSuspendedAccount(
        String userId,
        String nickname,
        String suspendedAt,
        String reason,
        String adminUsername
) {

    public static AdminSuspendedAccount from(SuspendedAccountRow row) {
        return new AdminSuspendedAccount(
                row.getUserId(),
                row.getNickname(),
                row.getSuspendedAt(),
                row.getReason(),
                row.getAdminUsername());
    }
}
