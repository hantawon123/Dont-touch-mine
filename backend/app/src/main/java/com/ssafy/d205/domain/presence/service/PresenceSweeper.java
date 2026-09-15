package com.ssafy.d205.domain.presence.service;

import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Component;
import org.springframework.transaction.annotation.Transactional;

import com.ssafy.d205.domain.presence.entity.PresenceTimeout;
import com.ssafy.d205.domain.presence.repository.UserPresenceRepository;
import com.ssafy.d205.global.common.TimeProvider;

/**
 * 하트비트가 끊긴 행을 주기적으로 OFFLINE 으로 내립니다.
 *
 * <p><b>이것이 없어도 API 응답은 정확합니다.</b> 조회할 때 PresenceTimeout 이 실제 상태를
 * 계산하기 때문입니다. 스윕이 하는 일은 저장된 값을 실제와 맞추는 것뿐입니다.
 *
 * <p>그래도 두는 이유가 둘입니다. 스윕이 없으면 크래시로 죽은 사용자의 status 가 영원히
 * ONLINE 으로 남습니다. 나중에 "지금 몇 명 온라인" 같은 집계를 그 컬럼으로 하면 틀립니다.
 * 그리고 V3 가 만든 ix_user_presence_sweep 인덱스가 쓰이지 않는 죽은 인덱스가 됩니다.
 *
 * <p><b>앱 인스턴스가 하나일 때만 안전합니다.</b> 여러 개로 늘리면 같은 스윕이 동시에
 * 돌면서 서로의 UPDATE 를 덮어씁니다. 결과가 같은 연산이라 데이터가 깨지지는 않지만
 * 불필요한 잠금 경합이 생깁니다. 그때는 분산 잠금이나 단일 인스턴스 지정이 필요합니다.
 */
@Component
@RequiredArgsConstructor
@Slf4j
public class PresenceSweeper {

    private final UserPresenceRepository userPresenceRepository;
    private final TimeProvider timeProvider;

    /**
     * 기본값은 하트비트 주기와 같은 30초입니다.
     *
     * <p>fixedRate 가 아니라 fixedDelay 입니다. 이전 실행이 끝난 뒤 간격을 세므로 실행이
     * 오래 걸릴 때 다음 실행이 겹쳐 들어오지 않습니다.
     *
     * <p>간격을 설정으로 뺀 이유는 테스트입니다. 스케줄러가 테스트 중에 돌면 하트비트를
     * 과거로 밀어 크래시를 재현하는 테스트와 경합해 간헐적으로 실패합니다.
     * application-test.yml 이 한 시간으로 늘려 사실상 끕니다.
     */
    @Scheduled(fixedDelayString = "${presence.sweep-interval-ms:30000}")
    @Transactional
    public void sweep() {
        int swept = userPresenceRepository.markStaleOffline(timeProvider.minus(PresenceTimeout.TIMEOUT),
                                                            timeProvider.now());
        if (swept > 0) {
            log.info("하트비트가 끊긴 접속 상태 {}건을 오프라인으로 내렸습니다.", swept);
        }
    }
}
