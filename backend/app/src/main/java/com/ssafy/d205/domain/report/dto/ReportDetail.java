package com.ssafy.d205.domain.report.dto;

import java.util.List;

/**
 * @param id        신고 한 건의 식별자. 건별 숨김·삭제가 이 값을 씁니다
 * @param reason    ReportReason 의 이름
 * @param memo      신고자가 적은 한 줄. 없으면 null
 * @param createdAt yyyyMMddHHmmss, UTC
 * @param status    ReportStatus 의 이름
 */
public record ReportDetail(
        Integer id,
        String reason,
        String memo,
        String createdAt,
        String status
) {
    /** @param reports 그 사람에 대한 신고. 최근 순입니다. */
    public record ListResponse(List<ReportDetail> reports) {
    }
}
