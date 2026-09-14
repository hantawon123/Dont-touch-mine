package com.ssafy.d205.domain.admin.service;

import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.time.ZoneId;
import java.util.ArrayList;
import java.util.List;

import com.ssafy.d205.domain.admin.dto.AdminOverview;
import com.ssafy.d205.domain.feedback.repository.UserFeedbackRepository;
import com.ssafy.d205.domain.notification.service.NotificationSessionRegistry;
import com.ssafy.d205.domain.ops.entity.OpsSample;
import com.ssafy.d205.domain.ops.repository.OpsSampleRepository;
import com.ssafy.d205.domain.ops.service.AccountDeletionCounter;
import com.ssafy.d205.domain.ops.service.AnalyticsInternalClient;
import com.ssafy.d205.domain.ops.service.SystemGauges;
import com.ssafy.d205.domain.presence.entity.PresenceStatus;
import com.ssafy.d205.domain.presence.repository.UserPresenceRepository;
import com.ssafy.d205.domain.report.entity.ReportStatus;
import com.ssafy.d205.domain.report.repository.UserReportRepository;
import com.ssafy.d205.domain.user.repository.UserRepository;
import com.ssafy.d205.global.common.Timestamps;

/**
 * 개요 탭의 카드와 시계열을 만듭니다 (S15P21D205-1003).
 *
 * <p>"지금 값"은 요청 순간에 다시 잽니다. 샘플은 1분 전 것일 수 있고, 운영자가 새로고침을 누르는
 * 이유는 지금을 보려는 것입니다. 시계열만 샘플에서 읽습니다.
 *
 * <p>"오늘"은 Asia/Seoul 기준입니다. 저장은 전부 UTC 지만 운영자가 사는 하루는 한국 시간이고,
 * UTC 자정(오전 9시)에 "오늘 가입"이 0 으로 돌아가면 아무도 그 숫자를 믿지 않습니다.
 *
 * <p>7일 범위는 15분 버킷으로 줄여서 보냅니다. 1분 행 그대로면 10,080점이라 브라우저가 그리기에
 * 무겁고 눈으로 구분되지도 않습니다. 24시간은 1,440점이라 그대로 둡니다.
 */
@Service
@RequiredArgsConstructor
public class AdminOverviewService {

    static final ZoneId OPERATOR_ZONE = ZoneId.of("Asia/Seoul");
    static final int WEEK_BUCKET_MINUTES = 15;

    private final UserPresenceRepository presence;
    private final NotificationSessionRegistry registry;
    private final UserRepository users;
    private final UserReportRepository reports;
    private final UserFeedbackRepository feedback;
    private final OpsSampleRepository samples;
    private final AccountDeletionCounter deletions;
    private final SystemGauges gauges;
    private final AnalyticsInternalClient analytics;
    private final Clock clock;

    @Transactional(readOnly = true)
    public AdminOverview overview(String range) {
        boolean week = "7d".equals(range);
        Instant now = clock.instant();
        String todayStart = Timestamps.format(
                now.atZone(OPERATOR_ZONE).toLocalDate().atStartOfDay(OPERATOR_ZONE).toInstant());
        String from = Timestamps.format(now.minus(week ? Duration.ofDays(7) : Duration.ofHours(24)));

        List<OpsSample> rows = samples.findBySampledAtGreaterThanEqualOrderBySampledAtAsc(from);

        // 오늘 자정은 24시간 전보다 늘 뒤이므로, 어느 범위로 읽었든 오늘 행은 rows 안에 있습니다.
        long deletionsToday = rows.stream()
                .filter(s -> s.getSampledAt().compareTo(todayStart) >= 0)
                .mapToInt(OpsSample::getDeletions).sum()
                + deletions.pending();

        AdminOverview.Now current = new AdminOverview.Now(
                (int) presence.countByStatus(PresenceStatus.ONLINE),
                (int) presence.countByStatus(PresenceStatus.IN_LOBBY),
                (int) presence.countByStatus(PresenceStatus.IN_GAME),
                registry.connectionCount(),
                users.count(),
                users.countByCreatedAtGreaterThan(todayStart),
                deletionsToday,
                reports.countByStatusAndDeletedAtIsNull(ReportStatus.PENDING),
                feedback.countByCreatedAtGreaterThanEqualAndDeletedAtIsNull(todayStart),
                users.countBySuspendedAtIsNotNull(),
                gauges.cpuPct(),
                gauges.heapUsedMb(),
                gauges.heapMaxMb(),
                gauges.dbPoolActive(),
                gauges.dbPoolMax());

        List<AdminOverview.Point> series = week
                ? bucket(rows, WEEK_BUCKET_MINUTES)
                : rows.stream().map(AdminOverviewService::point).toList();

        // 경기 통계는 분석 서비스의 것입니다. 죽어 있으면 비어 오고 화면은 그 카드만 비웁니다.
        return new AdminOverview(current, week ? "7d" : "24h", series, analytics.fetchSummary().orElse(null));
    }

    private static AdminOverview.Point point(OpsSample s) {
        return new AdminOverview.Point(s.getSampledAt(), s.getOnline(), s.getInLobby(), s.getInGame(),
                s.getSocketConnections(), s.getSignups(), s.getDeletions(), s.getCpuPct().doubleValue(),
                s.getHeapUsedMb(), s.getDbPoolActive());
    }

    /**
     * 시각순 샘플을 minutes 분 버킷으로 묶습니다. 인원·자원은 평균, 가입·탈퇴는 합입니다.
     *
     * <p>증분(가입·탈퇴)을 평균하면 버킷 안에서 일어난 일이 15분의 1 로 보입니다. 그 둘만 더합니다.
     * 버킷 시각은 경계(분을 minutes 로 내림, 초 00)입니다.
     */
    static List<AdminOverview.Point> bucket(List<OpsSample> rows, int minutes) {
        List<AdminOverview.Point> out = new ArrayList<>();
        String key = null;
        List<OpsSample> group = new ArrayList<>();

        for (OpsSample row : rows) {
            String rowKey = bucketKey(row.getSampledAt(), minutes);
            if (key != null && !rowKey.equals(key)) {
                out.add(fold(key, group));
                group.clear();
            }
            key = rowKey;
            group.add(row);
        }
        if (key != null) {
            out.add(fold(key, group));
        }
        return out;
    }

    public static String bucketKey(String at, int minutes) {
        int minute = Integer.parseInt(at.substring(10, 12));
        int floored = (minute / minutes) * minutes;
        return at.substring(0, 10) + String.format("%02d", floored) + "00";
    }

    private static AdminOverview.Point fold(String at, List<OpsSample> group) {
        int n = group.size();
        return new AdminOverview.Point(
                at,
                avg(group.stream().mapToInt(OpsSample::getOnline).sum(), n),
                avg(group.stream().mapToInt(OpsSample::getInLobby).sum(), n),
                avg(group.stream().mapToInt(OpsSample::getInGame).sum(), n),
                avg(group.stream().mapToInt(OpsSample::getSocketConnections).sum(), n),
                group.stream().mapToInt(OpsSample::getSignups).sum(),
                group.stream().mapToInt(OpsSample::getDeletions).sum(),
                Math.round(group.stream().mapToDouble(s -> s.getCpuPct().doubleValue()).sum() / n * 10) / 10.0,
                avg(group.stream().mapToInt(OpsSample::getHeapUsedMb).sum(), n),
                avg(group.stream().mapToInt(OpsSample::getDbPoolActive).sum(), n));
    }

    private static int avg(int sum, int n) {
        return (int) Math.round((double) sum / n);
    }
}
