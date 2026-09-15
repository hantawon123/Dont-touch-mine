package com.ssafy.d205.domain.report.dto;

import jakarta.validation.constraints.AssertTrue;
import jakarta.validation.constraints.NotNull;

import com.ssafy.d205.domain.report.entity.ReportStatus;

/**
 * @param status 무엇으로 마무리할지. ACTIONED 또는 DISMISSED 입니다.
 *               <p>PENDING 으로 되돌리는 것은 서비스가 막습니다. 그것은 검토를
 *               취소하는 것인데, 그러면 누가 언제 봤는지가 사라져 "아직 아무도 안 본
 *               신고"와 구분되지 않습니다.
 */
public record ReviewReportRequest(
        @NotNull(message = "status는 필수입니다.")
        ReportStatus status
) {
    /**
     * PENDING 으로 되돌리는 것을 막습니다.
     *
     * <p>그것은 검토를 취소하는 것인데, 그러면 reviewed_at 과 reviewed_by 가 남은 채로
     * 상태만 미검토가 되거나 그 둘까지 지워야 합니다. 어느 쪽이든 "아직 아무도 안 본
     * 신고"와 구분되지 않습니다. 판단을 바꾸고 싶으면 ACTIONED 와 DISMISSED 사이에서
     * 바꾸면 됩니다.
     */
    @AssertTrue(message = "status는 ACTIONED 또는 DISMISSED여야 합니다.")
    public boolean isDecision() {
        return status != ReportStatus.PENDING;
    }
}
