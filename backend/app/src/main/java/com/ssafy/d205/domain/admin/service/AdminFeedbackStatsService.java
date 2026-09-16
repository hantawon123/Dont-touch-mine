package com.ssafy.d205.domain.admin.service;

import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

import com.ssafy.d205.domain.admin.dto.AdminCountTable;
import com.ssafy.d205.domain.admin.dto.AdminFeedbackStats;
import com.ssafy.d205.domain.feedback.repository.LabelCountRow;
import com.ssafy.d205.domain.feedback.repository.UserFeedbackRepository;
import com.ssafy.d205.global.common.TimeProvider;

/**
 * 피드백 탭의 분포 셋 - 날짜, 플랫폼, 빌드 버전 (S15P21D205-1004).
 *
 * <p>{@link AdminReportStatsService} 와 짝이고 같은 기간 규칙({@link StatsWindow})을 씁니다. 두 탭의
 * "최근 30일" 이 다른 날을 가리키면 운영자가 두 표를 나란히 놓고 읽을 수 없습니다.
 */
@Service
@RequiredArgsConstructor
public class AdminFeedbackStatsService {

    /** 클라이언트가 플랫폼이나 빌드를 안 보낸 것. 빈 칸으로 두면 표에서 줄이 있는지도 안 보입니다. */
    static final String UNKNOWN = "(없음)";

    private final UserFeedbackRepository userFeedbackRepository;
    private final TimeProvider timeProvider;

    @Transactional(readOnly = true)
    public AdminFeedbackStats stats(Integer days) {
        StatsWindow window = StatsWindow.lastDays(days, timeProvider.now());

        return new AdminFeedbackStats(
                window.days(),
                AdminCountTable.daily(countsOf(userFeedbackRepository.countByDaySince(window.since())),
                        window.firstDay(), window.lastDay()),
                AdminCountTable.of("플랫폼", countsOf(userFeedbackRepository.countByPlatformSince(window.since())), UNKNOWN),
                AdminCountTable.of("빌드 버전", countsOf(userFeedbackRepository.countByBuildSince(window.since())), UNKNOWN));
    }

    private static Map<String, Integer> countsOf(List<LabelCountRow> rows) {
        Map<String, Integer> counts = new LinkedHashMap<>();
        for (LabelCountRow row : rows) {
            counts.put(row.getLabel(), row.getCount());
        }
        return counts;
    }
}
