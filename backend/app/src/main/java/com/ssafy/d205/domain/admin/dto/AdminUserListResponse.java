package com.ssafy.d205.domain.admin.dto;

import java.util.List;

/**
 * @param users 찾은 사용자들. 없으면 빈 배열입니다.
 *              <p>배열을 그대로 본문으로 주지 않고 감쌌습니다. 신고 목록과 같은 이유로, 나중에
 *              페이징이나 전체 건수를 붙일 때 필드만 늘리면 됩니다.
 */
public record AdminUserListResponse(
        List<AdminUserSummary> users
) {
}
