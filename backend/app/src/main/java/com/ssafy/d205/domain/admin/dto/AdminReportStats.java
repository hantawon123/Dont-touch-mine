package com.ssafy.d205.domain.admin.dto;

/**
 * 신고 탭의 통계 (S15P21D205-1004). 최근 {@code days} 일 동안 들어온 신고를 셋으로 나눠 셉니다.
 *
 * <p>기간은 <b>신고가 들어온 시각</b> 기준입니다. 검토 시각이 아닙니다. 그래서 상태 분포의 PENDING 은
 * "이 기간에 들어와서 아직 안 본 것"이고, 오래된 미검토 신고는 여기 없습니다 - 그건 목록에서 봅니다.
 *
 * <p>운영자가 숨긴 신고는 셋 모두에서 빠집니다.
 *
 * @param days     센 기간(일). 오늘을 포함해 뒤로 이만큼
 * @param byReason 사유별 건수. 많은 순
 * @param byStatus 처리 상태별 건수. PENDING·ACTIONED·DISMISSED 순
 * @param daily    날짜별(KST) 건수. 없는 날은 0
 */
public record AdminReportStats(
        int days,
        AdminCountTable byReason,
        AdminCountTable byStatus,
        AdminCountTable daily
) {
}
