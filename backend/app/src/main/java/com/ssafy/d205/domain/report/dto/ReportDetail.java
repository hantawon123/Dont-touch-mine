package com.ssafy.d205.domain.report.dto;

import java.util.List;

/**
 * @param id        신고 한 건의 식별자. 건별 숨김·삭제가 이 값을 씁니다
 * @param reason    ReportReason 의 이름
 * @param memo      신고자가 적은 한 줄. 없으면 null
 * @param createdAt yyyyMMddHHmmss, UTC
 * @param status    ReportStatus 의 이름
 * @param contextKey 어느 경기에서 한 신고인지(예: 7K2M9P#2). 옛 클라이언트가 보내지 않았으면 null.
 *                   '#' 앞이 방 코드이고, 그 값과 createdAt 으로 그 방 그 시간대의 대화를
 *                   엽니다(S15P21D205-1030). 이 값이 없으면 운영자는 무슨 말이 오갔는지
 *                   확인할 방법이 없습니다
 */
public record ReportDetail(
        Integer id,
        String reason,
        String memo,
        String createdAt,
        String status,
        String contextKey
) {
    /** @param reports 그 사람에 대한 신고. 최근 순입니다. */
    public record ListResponse(List<ReportDetail> reports) {
    }
}
