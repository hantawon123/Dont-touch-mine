package com.ssafy.d205.domain.report.dto;

/**
 * 신고한 사람 하나. 신고자 관점 목록의 한 줄입니다 (S15P21D205-1004).
 *
 * @param userId           신고자의 공개 식별자. <b>탈퇴한 신고자들은 한 줄로 묶이고 이 값이 null</b> 입니다.
 *                         reporter_seq 가 전부 NULL 이라 몇 명이었는지 알 수 없어 나눌 수 없습니다.
 * @param nickname         지금 닉네임. 탈퇴했으면 null. 표시에만 씁니다.
 * @param reportCount      이 사람이 한 신고 건수
 * @param pendingCount     아직 검토하지 않은 건수
 * @param actionedCount    문제 있다고 본 건수
 * @param dismissedCount   문제 없다고 본 건수
 * @param dismissedPercent 검토된 것 중 기각 비율(%). 소수 첫째 자리. <b>검토된 것이 없으면 null</b> 입니다 -
 *                         0 으로 두면 "전부 인정됐다"로 읽힙니다. 미검토를 분모에 넣지 않는 이유는
 *                         아직 판단하지 않은 것을 어느 쪽으로도 세지 않기 위해서입니다.
 * @param targetCount      신고한 상대의 수. "10건 1명"과 "10건 8명"은 다른 문제입니다.
 * @param lastReportedAt   가장 최근 신고 시각. yyyyMMddHHmmss, UTC.
 */
public record ReporterSummary(
        String userId,
        String nickname,
        int reportCount,
        int pendingCount,
        int actionedCount,
        int dismissedCount,
        Double dismissedPercent,
        int targetCount,
        String lastReportedAt
) {

    /** 검토된 것 중 기각 비율. 검토된 것이 없으면 null. */
    public static Double dismissedPercentOf(int actioned, int dismissed) {
        int reviewed = actioned + dismissed;
        if (reviewed == 0) {
            return null;
        }
        return Math.round(dismissed * 1000.0 / reviewed) / 10.0;
    }
}
