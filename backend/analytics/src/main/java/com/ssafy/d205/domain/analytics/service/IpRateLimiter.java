package com.ssafy.d205.domain.analytics.service;

import org.springframework.stereotype.Component;

import java.time.Clock;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.atomic.AtomicInteger;

import com.ssafy.d205.domain.analytics.config.AnalyticsProperties;

/**
 * IP 하나가 분당 보낼 수 있는 요청 수를 제한합니다.
 *
 * <p>수집 API 에는 인증이 없습니다. 아무나 부를 수 있는 엔드포인트라, 한 IP 가 무한히 쏘는
 * 것만은 막아야 큐 상한이 실제로 의미가 있습니다. 상한 큐는 "폭주해도 죽지 않는다"를 보장하고,
 * 이 클래스는 "한 명이 폭주시킬 수 없다"를 보장합니다.
 *
 * <p>고정 창(fixed window)입니다. 분 경계에서 두 배까지 새는 것을 알고 씁니다. 정밀한 제한이
 * 목적이 아니라 명백한 남용을 자르는 것이 목적이고, 슬라이딩 로그를 두면 IP 마다 타임스탬프
 * 목록을 들어야 합니다.
 *
 * <p>메모리 안에만 있습니다. 앱 인스턴스가 하나라 충분하고, 재시작하면 초기화되는 것도 문제가
 * 아닙니다. 항목은 창이 바뀔 때 갈아 끼우고, 지도가 커지면 오래된 것을 훑어 지웁니다.
 *
 * <p>nginx 뒤에 있으므로 IP 는 X-Forwarded-For 에서 옵니다. application-prod.yml 의
 * forward-headers-strategy 가 그것을 request.getRemoteAddr() 로 옮겨 줍니다.
 */
@Component
public class IpRateLimiter {

    /** 이 이상 IP 가 쌓이면 지난 창의 항목을 지웁니다. 훑는 비용을 매 요청마다 내지 않기 위한 문턱입니다. */
    private static final int SWEEP_THRESHOLD = 10_000;

    private final int limitPerMinute;
    private final Clock clock;
    private final Map<String, Window> windows = new ConcurrentHashMap<>();

    public IpRateLimiter(AnalyticsProperties properties, Clock clock) {
        this.limitPerMinute = properties.rateLimitPerMinute();
        this.clock = clock;
    }

    /** 이 요청을 받아도 되는지. 받으면 그 IP 의 이번 분 사용량이 하나 늘어납니다. */
    public boolean tryAcquire(String ip) {
        long minute = clock.millis() / 60_000;

        Window window = windows.compute(ip, (key, existing) ->
                existing == null || existing.minute != minute ? new Window(minute) : existing);

        boolean allowed = window.count.incrementAndGet() <= limitPerMinute;

        if (windows.size() > SWEEP_THRESHOLD) {
            windows.entrySet().removeIf(e -> e.getValue().minute != minute);
        }
        return allowed;
    }

    private static final class Window {
        private final long minute;
        private final AtomicInteger count = new AtomicInteger();

        private Window(long minute) {
            this.minute = minute;
        }
    }
}
