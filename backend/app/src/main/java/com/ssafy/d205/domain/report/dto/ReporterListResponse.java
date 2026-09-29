package com.ssafy.d205.domain.report.dto;

import java.util.List;

/**
 * @param reporters 신고한 사람들. 건수 많은 순이고, 탈퇴한 신고자들은 userId 가 null 인 한 줄입니다.
 *                  없으면 빈 배열. 감싼 이유는 {@link ReportedUserListResponse} 와 같습니다.
 */
public record ReporterListResponse(
        List<ReporterSummary> reporters
) {
}
