package com.ssafy.d205.domain.analytics.service;

import io.micrometer.core.instrument.Counter;
import io.micrometer.core.instrument.MeterRegistry;
import org.springframework.stereotype.Component;

import java.util.Collection;
import java.util.List;
import java.util.concurrent.ArrayBlockingQueue;

import com.ssafy.d205.domain.analytics.config.AnalyticsProperties;
import com.ssafy.d205.domain.analytics.entity.GameEventRow;

/**
 * 상한 큐. 별도 서비스로 나누지 않고도 게임 API 를 지키는 장치입니다.
 *
 * <p>무제한 큐로 두면 클라이언트 버그로 이벤트가 폭주할 때 이 앱이 OOM 으로 죽고 게임 API 도
 * 함께 죽습니다. 상한을 두면 이벤트만 잃고 게임은 삽니다. 별도 서비스로 나눠서 얻는 격리
 * 이득의 대부분이 이 클래스에서 나옵니다.
 *
 * <p><b>절대 블로킹하지 않습니다.</b> {@code offer} 만 쓰고 {@code put} 을 쓰지 않습니다. 가득
 * 차면 그 자리에서 버리고 카운터만 올립니다. 수집 요청 스레드가 큐 때문에 멈추는 일은 없어야
 * 합니다.
 *
 * <p>카운터는 Micrometer 로 두어 나중에 metrics 를 열면 그대로 보이고, 지금은
 * {@link GameEventFlusher} 가 1분마다 로그로 요약합니다.
 */
@Component
public class GameEventBuffer {

    private final ArrayBlockingQueue<GameEventRow> queue;
    private final Counter accepted;
    private final Counter dropped;

    public GameEventBuffer(AnalyticsProperties properties, MeterRegistry meterRegistry) {
        this.queue = new ArrayBlockingQueue<>(properties.queueCapacity());
        this.accepted = meterRegistry.counter("analytics.events.accepted");
        this.dropped = meterRegistry.counter("analytics.events.dropped");
    }

    /**
     * 넣습니다. 자리가 없으면 <b>그 행부터 나머지를 전부 버립니다.</b>
     *
     * <p>배치 중간에 큐가 차면 앞부분은 들어가고 뒷부분은 버려집니다. 배치를 통째로 되돌리지
     * 않는 이유는 그러려면 먼저 자리를 세고 넣어야 하는데, 그 사이에 다른 스레드가 채울 수
     * 있어 어차피 보장이 안 되기 때문입니다. 일부 유실은 드롭 카운터가 셉니다.
     *
     * @return 버린 개수. 0 이면 전부 들어갔습니다
     */
    public int offerAll(Collection<GameEventRow> rows) {
        int lost = 0;
        for (GameEventRow row : rows) {
            if (queue.offer(row)) {
                accepted.increment();
            } else {
                lost++;
            }
        }
        if (lost > 0) {
            dropped.increment(lost);
        }
        return lost;
    }

    /** 최대 max 개를 꺼내 담습니다. 비어 있으면 아무것도 담지 않습니다. */
    public void drainTo(List<GameEventRow> into, int max) {
        queue.drainTo(into, max);
    }

    public int size() {
        return queue.size();
    }

    public boolean isEmpty() {
        return queue.isEmpty();
    }

    /** 지금까지 버린 개수. 앱 시작 이후 누적입니다. */
    public long droppedCount() {
        return (long) dropped.count();
    }

    public long acceptedCount() {
        return (long) accepted.count();
    }
}
