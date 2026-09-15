package com.ssafy.d205.domain.presence.service;

import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Component;

import java.time.Duration;
import java.util.Set;

import com.ssafy.d205.domain.notification.service.NotificationSessionRegistry;
import com.ssafy.d205.domain.presence.entity.PresenceTimeout;

/**
 * 알림 채널에 붙어 있는 사람들의 하트비트를 주기적으로 밉니다.
 *
 * <p><b>클라이언트의 30초 주기 PUT 을 대체하는 자리입니다</b>(S15P21D205-890). 접속자 천
 * 명이면 예전에는 30초마다 요청 천 개가 들어와 각각 트랜잭션을 열고 users 와
 * user_presence 를 조회한 뒤 UPDATE 를 냈습니다. 지금은 접속자가 몇 명이든 UPDATE 문장
 * 하나입니다.
 *
 * <p>그래도 하트비트를 아예 없애지 않은 이유는 {@link PresenceTimeout} 의 만료 판정을
 * 살리기 위해서입니다. 친구 목록과 초대 차단이 그 판정을 지나고, 무엇도 heartbeat_at 을
 * 밀지 않으면 <b>붙어 있는 사람이 90초 뒤 전부 오프라인으로 읽힙니다.</b>
 *
 * <p>만료 판정을 살려 둔 값은 <b>서버가 죽었다 살아났을 때</b>입니다. 그때 세션
 * 레지스트리는 비어 있으므로 죽은 서버가 남긴 ONLINE 행은 더 이상 갱신되지 않고, 90초
 * 뒤 PresenceSweeper 가 내립니다. 기동 시 일괄 정리를 하지 않는 이유는 그 방식이
 * 인스턴스를 늘리는 날 <b>다른 인스턴스의 살아 있는 사용자를 꺼버리기</b> 때문입니다.
 *
 * <p>간격은 타임아웃(90초)보다 <b>확실히 짧아야</b> 합니다. 길면 갱신 사이에 만료가 먼저
 * 와서 접속자가 주기적으로 오프라인으로 깜빡입니다. 그 실수는 로그에 아무것도 남기지
 * 않고 친구 목록만 이상하게 만들기 때문에, 생성자에서 검사해 <b>기동을 실패시킵니다</b>.
 * 기본값 30초는 타임아웃의 3분의 1이라 한 번 놓쳐도 여유가 있습니다.
 *
 * <p>ping 과 같은 30초이지만 스케줄러를 따로 둡니다. NotificationKeepAlive 는 죽은 연결을
 * 걷어내는 일만 하고, 이 클래스는 살아남은 사람들을 DB 에 반영합니다. 둘의 순서는
 * 중요하지 않습니다 — 방금 죽은 연결의 하트비트를 한 번 더 밀어도, 그 연결은 같은 틱에
 * unbind 되면서 오프라인으로 쓰이기 때문입니다.
 */
@Component
@Slf4j
public class PresenceHeartbeat {

    private final NotificationSessionRegistry registry;
    private final PresenceService presenceService;

    /**
     * 간격을 설정으로 뺀 이유는 테스트입니다. PresenceSweeper 와 같은 사정으로,
     * 스케줄러가 테스트 중에 돌면 하트비트를 과거로 밀어 크래시를 재현하는 테스트와
     * 경합합니다. application-test.yml 이 한 시간으로 늘려 사실상 끕니다.
     *
     * <p>그 한 시간은 타임아웃보다 길어 아래 검사에 걸립니다. 테스트 프로필만 예외로
     * 두지 않고, 검사는 <b>스케줄러가 실제로 도는 값</b>에만 적용합니다 — 한 시간 이상은
     * "끈 것"으로 읽습니다. 운영에서 실수로 두 자리 분 단위를 넣는 경우가 잡으려는
     * 대상이고, 그 값은 이 경계 아래에 있습니다.
     */
    public PresenceHeartbeat(
            NotificationSessionRegistry registry,
            PresenceService presenceService,
            @Value("${presence.heartbeat-refresh-ms:30000}") long refreshIntervalMs) {
        requireShorterThanTimeout(refreshIntervalMs);
        this.registry = registry;
        this.presenceService = presenceService;
    }

    /** 스케줄러를 사실상 끈 것으로 보는 경계. 이 이상이면 간격 검사를 하지 않습니다. */
    static final Duration DISABLED_AT = Duration.ofHours(1);

    /**
     * 갱신 간격이 만료 판정보다 짧은지 확인합니다.
     *
     * <p>규칙을 static 으로 둔 이유는 스프링 없이 시험하려는 것입니다. 잘못된 설정으로
     * 컨텍스트를 띄워 실패를 보는 테스트는 느리고, 이 규칙은 숫자 둘의 비교입니다.
     *
     * @throws IllegalStateException 간격이 {@link PresenceTimeout#TIMEOUT} 이상이면서
     *                               {@link #DISABLED_AT} 미만일 때
     */
    static void requireShorterThanTimeout(long refreshIntervalMs) {
        Duration interval = Duration.ofMillis(refreshIntervalMs);
        if (interval.compareTo(DISABLED_AT) >= 0) {
            return;
        }
        if (interval.compareTo(PresenceTimeout.TIMEOUT) >= 0) {
            throw new IllegalStateException(
                    "presence.heartbeat-refresh-ms=" + refreshIntervalMs + " 은 만료 판정 "
                    + PresenceTimeout.TIMEOUT.toMillis() + "ms 보다 짧아야 합니다. 이대로 두면 "
                    + "접속해 있는 사람이 갱신 사이마다 오프라인으로 읽힙니다.");
        }
    }

    @Scheduled(fixedDelayString = "${presence.heartbeat-refresh-ms:30000}")
    public void refresh() {
        Set<Integer> connected = registry.boundUserSeqs();
        if (connected.isEmpty()) {
            return;
        }
        int refreshed = presenceService.refreshHeartbeats(connected);
        log.debug("붙어 있는 {}명 중 {}개 행의 하트비트를 갱신했습니다.", connected.size(), refreshed);
    }
}
