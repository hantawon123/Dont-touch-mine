package com.ssafy.d205.domain.admin.service;

import java.time.LocalDate;
import java.time.LocalDateTime;
import java.time.ZoneOffset;
import java.time.format.DateTimeFormatter;

import com.ssafy.d205.global.common.Timestamps;

/**
 * "최근 n 일" 을 조회 조건으로 바꿉니다 (S15P21D205-1004).
 *
 * <p>날짜는 <b>한국 시간</b>입니다. 운영자가 "오늘" 이라고 부르는 것은 한국의 오늘이고, 저장소의
 * 날짜별 조회도 같은 기준(UTC + 9시간)으로 자릅니다. 두 곳이 다른 시간대를 쓰면 날짜별 표의 첫 줄과
 * 마지막 줄이 반쪽만 세어져 낮게 나오고, 그것은 숫자만 봐서는 알 수 없습니다.
 *
 * <p>서버가 한 곳에서만 운영되므로 시간대를 매개변수로 받지 않았습니다. 다른 시간대의 운영자가
 * 생기면 여기와 저장소의 {@code INTERVAL 9 HOUR} 두 곳을 함께 바꿔야 합니다.
 *
 * @param days     실제로 센 기간. 요청값을 1~365 로 깎은 것
 * @param firstDay 첫 날(한국 날짜, 포함)
 * @param lastDay  마지막 날 = 오늘(한국 날짜)
 * @param since    firstDay 0시(한국)를 UTC 14자로. 저장소 조회의 하한
 */
record StatsWindow(int days, LocalDate firstDay, LocalDate lastDay, String since) {

    /** 한국 표준시. 서머타임이 없어 고정 오프셋으로 충분합니다. */
    static final ZoneOffset KST = ZoneOffset.ofHours(9);

    static final int DEFAULT_DAYS = 30;
    static final int MAX_DAYS = 365;

    private static final DateTimeFormatter UTC_14 = DateTimeFormatter.ofPattern("yyyyMMddHHmmss");

    /**
     * @param requested 요청한 일수. null 이면 30. 1 보다 작으면 1, 365 보다 크면 365 로 깎습니다.
     *                  거절하지 않는 이유는 피드백 limit 과 같습니다 - 큰 값은 "다 보고 싶다"입니다
     * @param nowUtc    {@code TimeProvider.now()} 의 값
     */
    static StatsWindow lastDays(Integer requested, String nowUtc) {
        int days = requested == null ? DEFAULT_DAYS : Math.min(Math.max(requested, 1), MAX_DAYS);

        LocalDate today = LocalDateTime.parse(nowUtc, UTC_14).atOffset(ZoneOffset.UTC)
                .atZoneSameInstant(KST).toLocalDate();
        LocalDate first = today.minusDays(days - 1L);
        String since = Timestamps.format(first.atStartOfDay(KST).toInstant());

        return new StatsWindow(days, first, today, since);
    }
}
