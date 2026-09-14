package com.ssafy.d205.domain.ops.service;

import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Component;
import org.springframework.transaction.annotation.Transactional;

import java.time.Duration;

import com.ssafy.d205.domain.notification.service.NotificationSessionRegistry;
import com.ssafy.d205.domain.ops.entity.OpsSample;
import com.ssafy.d205.domain.ops.repository.OpsSampleRepository;
import com.ssafy.d205.domain.presence.entity.PresenceStatus;
import com.ssafy.d205.domain.presence.repository.UserPresenceRepository;
import com.ssafy.d205.domain.user.repository.UserRepository;
import com.ssafy.d205.global.common.TimeProvider;

/**
 * 1분마다 접속자와 서버 자원을 한 행으로 남깁니다 (S15P21D205-1003).
 *
 * <p>비용은 집계 쿼리 넷(presence 상태별 셋, 가입 수 하나)과 INSERT 하나입니다. presence 는 행이
 * 접속자 수만큼이라 작고, 가입 수는 created_at 범위라 인덱스가 없어도 users 가 수만 행이 되기
 * 전까지는 문제없습니다. 게임 API 스레드가 아니라 스케줄러 스레드에서 돕니다.
 *
 * <p>fixedDelay 입니다. 한 번이 오래 걸려도 다음 실행이 겹치지 않습니다. 간격을 설정으로 뺀
 * 이유는 테스트입니다 - application-test.yml 이 한 시간으로 늘려 사실상 끄고, 테스트는
 * {@link #sample()} 을 직접 부릅니다. presence 스윕과 같은 방식입니다.
 *
 * <p>가입·탈퇴는 직전 샘플 이후 증분입니다. 직전 시각은 메모리에만 있어서 기동 직후 첫 샘플은
 * "간격만큼 전"을 기준으로 잡습니다. 재시작 사이에 든 가입은 그 첫 샘플에 얼마간 섞이고,
 * 탈퇴는 {@link AccountDeletionCounter} 의 사정대로 사라집니다.
 */
@Component
@Slf4j
public class OpsSampler {

    private final UserPresenceRepository presence;
    private final NotificationSessionRegistry registry;
    private final UserRepository users;
    private final AccountDeletionCounter deletions;
    private final SystemGauges gauges;
    private final OpsSampleRepository samples;
    private final TimeProvider timeProvider;
    private final Duration interval;

    /** 직전 샘플 시각. 기동 뒤 첫 샘플 전에는 null 입니다. */
    private volatile String lastSampledAt;

    public OpsSampler(UserPresenceRepository presence,
                      NotificationSessionRegistry registry,
                      UserRepository users,
                      AccountDeletionCounter deletions,
                      SystemGauges gauges,
                      OpsSampleRepository samples,
                      TimeProvider timeProvider,
                      @Value("${ops.sample-interval-ms:60000}") long intervalMs) {
        this.presence = presence;
        this.registry = registry;
        this.users = users;
        this.deletions = deletions;
        this.gauges = gauges;
        this.samples = samples;
        this.timeProvider = timeProvider;
        this.interval = Duration.ofMillis(intervalMs);
    }

    @Scheduled(fixedDelayString = "${ops.sample-interval-ms:60000}")
    @Transactional
    public OpsSample sample() {
        String now = timeProvider.now();
        String since = lastSampledAt != null ? lastSampledAt : timeProvider.minus(interval);

        OpsSample sample = new OpsSample(
                now,
                (int) presence.countByStatus(PresenceStatus.ONLINE),
                (int) presence.countByStatus(PresenceStatus.IN_LOBBY),
                (int) presence.countByStatus(PresenceStatus.IN_GAME),
                registry.connectionCount(),
                (int) users.countByCreatedAtGreaterThan(since),
                deletions.drain(),
                gauges.cpuPct(),
                gauges.heapUsedMb(),
                gauges.heapMaxMb(),
                gauges.dbPoolActive(),
                gauges.dbPoolMax());

        // 같은 초에 두 번 불리면(테스트) PK 가 겹칩니다. 나중 값으로 덮습니다 - 같은 순간의 두 측정은
        // 같은 것을 재려던 것이고, 둘 다 남길 이유가 없습니다.
        samples.save(sample);
        lastSampledAt = now;

        log.debug("운영 지표를 남겼습니다. 접속 {}/{}/{} 소켓 {} cpu {}%",
                sample.getOnline(), sample.getInLobby(), sample.getInGame(),
                sample.getSocketConnections(), sample.getCpuPct());
        return sample;
    }
}
