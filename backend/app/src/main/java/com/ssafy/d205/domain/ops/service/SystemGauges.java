package com.ssafy.d205.domain.ops.service;

import io.micrometer.core.instrument.Gauge;
import io.micrometer.core.instrument.MeterRegistry;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Component;

/**
 * JVM 이 이미 재고 있는 서버 자원 값을 읽습니다. Actuator 를 밖에 열지 않고 코드에서 직접 봅니다.
 *
 * <p>Actuator 의 metrics 엔드포인트를 노출하면 그 경로에도 인증을 얹어야 하고, 노출해도 되는
 * 값을 하나씩 따져야 합니다(application.yml 의 management 주석). MeterRegistry 를 직접 읽으면
 * 그 고민이 없고, 관리자 세션 뒤의 API 로만 나갑니다.
 *
 * <p>이름은 Micrometer 가 Spring Boot 에서 자동으로 등록하는 것들입니다. 등록되지 않은 환경(일부
 * 테스트)에서는 0 으로 읽습니다. 없다고 기동을 막을 값이 아닙니다.
 */
@Component
@RequiredArgsConstructor
public class SystemGauges {

    private static final long MB = 1024L * 1024L;

    private final MeterRegistry registry;

    /** 시스템 CPU 사용률, 0~100. 컨테이너 안에서는 cgroup 한도 기준입니다. */
    public double cpuPct() {
        double usage = gauge("system.cpu.usage", null, null);
        if (Double.isNaN(usage) || usage < 0) {
            return 0;
        }
        return Math.round(usage * 1000) / 10.0;
    }

    /** 힙 사용량 MB. 영역별 게이지를 더합니다. */
    public int heapUsedMb() {
        return (int) (sum("jvm.memory.used", "area", "heap") / MB);
    }

    /** 힙 상한 MB. -1(상한 없음)인 풀은 건너뜁니다. */
    public int heapMaxMb() {
        return (int) (sum("jvm.memory.max", "area", "heap") / MB);
    }

    /** 게임 DB 커넥션 풀의 사용 중 커넥션 수. 분석 DB 는 별도 서비스라 여기 없습니다. */
    public int dbPoolActive() {
        return (int) sum("hikaricp.connections.active", null, null);
    }

    public int dbPoolMax() {
        return (int) sum("hikaricp.connections.max", null, null);
    }

    private double gauge(String name, String tagKey, String tagValue) {
        Gauge found = search(name, tagKey, tagValue).gauge();
        return found == null ? Double.NaN : found.value();
    }

    /** 같은 이름의 게이지를 전부 더합니다. 음수(-1 = 알 수 없음)와 NaN 은 빼고 셉니다. */
    private double sum(String name, String tagKey, String tagValue) {
        double total = 0;
        for (Gauge gauge : search(name, tagKey, tagValue).gauges()) {
            double value = gauge.value();
            if (!Double.isNaN(value) && value > 0) {
                total += value;
            }
        }
        return total;
    }

    private io.micrometer.core.instrument.search.Search search(String name, String tagKey, String tagValue) {
        io.micrometer.core.instrument.search.Search search = registry.find(name);
        return tagKey == null ? search : search.tag(tagKey, tagValue);
    }
}
