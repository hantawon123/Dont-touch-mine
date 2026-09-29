package com.ssafy.d205.domain.analytics.service;

import io.micrometer.core.instrument.Counter;
import io.micrometer.core.instrument.MeterRegistry;
import jakarta.annotation.PreDestroy;
import lombok.extern.slf4j.Slf4j;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Component;

import java.time.Duration;
import java.util.ArrayList;
import java.util.List;

import com.ssafy.d205.domain.analytics.config.AnalyticsProperties;
import com.ssafy.d205.domain.analytics.entity.GameEventRow;
import com.ssafy.d205.domain.analytics.repository.GameEventWriter;

/**
 * 큐를 주기적으로 비워 DB 에 씁니다. 수집 요청과 DB 쓰기를 떼어 놓는 반쪽입니다.
 *
 * <p><b>insert 실패는 삼킵니다.</b> 예외를 올리면 스케줄러 스레드가 죽거나 다음 실행이 밀리고,
 * 어느 쪽이든 게임 API 와는 무관한 이유로 로그가 소란스러워집니다. 실패한 배치는 버리고
 * 카운터만 올립니다. 다시 큐에 넣지 않는 이유는 한 행이 늘 실패하는 배치(독이 든 배치)가 큐를
 * 영원히 막기 때문입니다.
 *
 * <p>분석 DB 가 준비되지 않았으면 큐에서 꺼내지 않습니다. 그동안 큐가 차면 GameEventBuffer 가
 * 버립니다. 짧은 장애는 큐 용량만큼 버텨 주고, 긴 장애는 잃습니다. 그 사이에 30초마다
 * 마이그레이션을 다시 시도합니다.
 *
 * <p>fixedDelay 입니다. 한 번의 flush 가 오래 걸려도 다음 실행이 겹쳐 들어오지 않습니다.
 * PresenceSweeper 와 같은 판단입니다.
 */
@Component
@Slf4j
public class GameEventFlusher {

    /** 요약 로그 주기. 드롭이나 실패가 없으면 조용합니다. */
    private static final long SUMMARY_INTERVAL_MS = 60_000;

    /** 종료 시 마지막 flush 에 쓸 최대 시간. 이걸 넘기면 남은 것은 포기합니다. */
    private static final Duration SHUTDOWN_BUDGET = Duration.ofSeconds(5);

    private final AnalyticsProperties properties;
    private final GameEventBuffer buffer;
    private final GameEventWriter writer;
    private final Counter stored;
    private final Counter failed;

    private long lastReportedDropped;
    private long lastReportedFailed;

    public GameEventFlusher(AnalyticsProperties properties,
                            GameEventBuffer buffer,
                            GameEventWriter writer,
                            MeterRegistry meterRegistry) {
        this.properties = properties;
        this.buffer = buffer;
        this.writer = writer;
        this.stored = meterRegistry.counter("analytics.events.stored");
        this.failed = meterRegistry.counter("analytics.events.failed");
    }

    /**
     * 큐가 빌 때까지, 단 한 주기에 flushMaxBatchesPerTick 번까지 배치를 연달아 넣습니다.
     *
     * <p>처음에는 한 주기에 한 배치만 넣었습니다. 그러면 처리량 상한이 flushBatchSize / 주기,
     * 즉 초당 5,000행으로 고정됩니다. 부하 테스트(863)에서 정확히 그 지점에서 큐가 넘쳐 이벤트를
     * 버렸는데 MySQL 은 CPU 13%로 놀고 있었습니다. 상한이 자원이 아니라 이 루프 구조였던 것입니다.
     *
     * <p>상한을 두는 이유는 폭주 시 이 스레드가 영원히 flush 만 하지 않게 하려는 것입니다. 배치가
     * 가득 차지 않고 돌아오면(= 큐가 비었으면) 바로 멈추므로 평소에는 한 번만 돕니다.
     */
    @Scheduled(fixedDelayString = "${analytics.flush-interval-ms:1000}")
    public void flush() {
        for (int i = 0; i < properties.flushMaxBatchesPerTick(); i++) {
            if (flushOnce() < properties.flushBatchSize()) {
                return;
            }
        }
    }

    /**
     * 한 번 비웁니다. 최대 flushBatchSize 행.
     *
     * @return 이번에 DB 에 넘긴 행 수. 큐가 비었으면 0
     */
    public int flushOnce() {
        List<GameEventRow> batch = new ArrayList<>(properties.flushBatchSize());
        buffer.drainTo(batch, properties.flushBatchSize());
        if (batch.isEmpty()) {
            return 0;
        }

        try {
            writer.insertAll(batch);
            stored.increment(batch.size());
        } catch (Exception e) {
            // 스택을 넣지 않습니다. 1초마다 같은 원인으로 실패하면 스택이 로그를 뒤덮습니다.
            failed.increment(batch.size());
            log.warn("플레이 로그 {}건 insert 실패. 이 배치는 버립니다. 원인: {}", batch.size(), e.getMessage());
        }
        return batch.size();
    }


    /**
     * 1분마다 버린 것과 실패한 것을 요약합니다. 둘 다 늘지 않았으면 아무것도 찍지 않습니다.
     *
     * <p>actuator 는 health 만 열려 있어 metrics 로 볼 수 없습니다. 로그가 유일한 창입니다.
     * 드롭이 보이면 큐 용량이나 flush 주기가 부하에 못 미친 것이고, 실패가 보이면 DB 쪽입니다.
     */
    @Scheduled(fixedDelay = SUMMARY_INTERVAL_MS, initialDelay = SUMMARY_INTERVAL_MS)
    public void summarize() {
        long dropped = buffer.droppedCount();
        long failedNow = (long) failed.count();
        if (dropped == lastReportedDropped && failedNow == lastReportedFailed) {
            return;
        }
        log.warn("플레이 로그 지난 1분: 큐 초과로 버림 {}건, insert 실패 {}건 (누적 저장 {}건, 큐 잔량 {}건)",
                dropped - lastReportedDropped, failedNow - lastReportedFailed,
                (long) stored.count(), buffer.size());
        lastReportedDropped = dropped;
        lastReportedFailed = failedNow;
    }

    /**
     * 종료 전에 남은 것을 씁니다. graceful shutdown 이라 요청은 이미 안 들어옵니다.
     *
     * <p>시간 상한을 둡니다. DB 가 죽어 있으면 배치마다 커넥션 타임아웃(2초)을 기다리게 되는데,
     * 그걸 큐가 빌 때까지 반복하면 종료가 무한정 늦어집니다. 5초 안에 못 쓴 것은 포기하고
     * 남은 개수를 로그로 남깁니다.
     */
    @PreDestroy
    void flushOnShutdown() {
        long deadline = System.nanoTime() + SHUTDOWN_BUDGET.toNanos();
        while (!buffer.isEmpty() && System.nanoTime() < deadline) {
            if (flushOnce() == 0) {
                break;
            }
        }
        if (!buffer.isEmpty()) {
            log.warn("종료 시점에 플레이 로그 {}건을 쓰지 못하고 버립니다.", buffer.size());
        }
    }

    public long storedCount() {
        return (long) stored.count();
    }

    public long failedCount() {
        return (long) failed.count();
    }
}
