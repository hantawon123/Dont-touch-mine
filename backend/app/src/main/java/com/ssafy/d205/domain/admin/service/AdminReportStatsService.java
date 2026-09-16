package com.ssafy.d205.domain.admin.service;

import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

import com.ssafy.d205.domain.admin.dto.AdminCountTable;
import com.ssafy.d205.domain.admin.dto.AdminReportStats;
import com.ssafy.d205.domain.report.entity.ReportStatus;
import com.ssafy.d205.domain.report.repository.LabelCountRow;
import com.ssafy.d205.domain.report.repository.UserReportRepository;
import com.ssafy.d205.global.common.TimeProvider;

/**
 * 신고 탭의 분포 셋 - 사유, 처리 상태, 날짜 (S15P21D205-1004).
 *
 * <p>{@code report} 패키지가 아니라 여기 있는 이유는 이것이 신고 도메인의 규칙이 아니라 관리 화면의
 * 그림이기 때문입니다. 무엇을 어떤 기간으로 묶어 보여줄지는 화면이 정하고, 저장소는 세는 것만 합니다.
 * 피드백 쪽 {@link AdminFeedbackStatsService} 와 짝입니다.
 */
@Service
@RequiredArgsConstructor
public class AdminReportStatsService {

    private final UserReportRepository userReportRepository;
    private final TimeProvider timeProvider;

    /**
     * 최근 days 일의 신고 분포. days 는 {@link StatsWindow} 가 1~365 로 깎습니다.
     *
     * <p>상태 분포는 값이 없어도 세 상태를 모두 0 으로 채워 순서대로 줍니다. 화면이 막대 세 개를
     * 늘 같은 자리에 그려야 "이번 주는 기각이 없다"가 빈 차트가 아니라 0 짜리 막대로 보입니다.
     */
    @Transactional(readOnly = true)
    public AdminReportStats stats(Integer days) {
        StatsWindow window = StatsWindow.lastDays(days, timeProvider.now());

        Map<String, Integer> byReason = countsOf(userReportRepository.countByReasonSince(window.since()));

        Map<String, Integer> byStatus = new LinkedHashMap<>();
        for (ReportStatus status : ReportStatus.values()) {
            byStatus.put(status.name(), 0);
        }
        byStatus.putAll(countsOf(userReportRepository.countByStatusSince(window.since())));

        Map<String, Integer> daily = countsOf(userReportRepository.countByDaySince(window.since()));

        return new AdminReportStats(
                window.days(),
                AdminCountTable.of("사유", byReason, "(없음)"),
                AdminCountTable.of("처리 상태", byStatus, "(없음)"),
                AdminCountTable.daily(daily, window.firstDay(), window.lastDay()));
    }

    private static Map<String, Integer> countsOf(List<LabelCountRow> rows) {
        Map<String, Integer> counts = new LinkedHashMap<>();
        for (LabelCountRow row : rows) {
            counts.put(row.getLabel(), row.getCount());
        }
        return counts;
    }
}
