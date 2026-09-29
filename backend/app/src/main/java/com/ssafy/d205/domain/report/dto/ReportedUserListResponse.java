package com.ssafy.d205.domain.report.dto;

import java.util.List;

/**
 * @param users 신고당한 사람들. 없으면 빈 배열입니다.
 *              <p>배열을 그대로 본문으로 주지 않고 감쌌습니다. 나중에 페이징을 붙일 때
 *              응답 형태가 바뀌면 화면이 깨지는데, 감싸두면 필드만 늘리면 됩니다.
 */
public record ReportedUserListResponse(
        List<ReportedUserSummary> users
) {
}
