package com.ssafy.d205.global.common;

import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Component;

import java.time.Clock;
import java.time.Duration;

/**
 * 시계를 읽는 유일한 곳입니다.
 *
 * <p>서비스가 연산이 시작될 때 한 번 읽어서 엔티티에 넘깁니다. 엔티티가 각자 읽으면 한
 * 연산 안의 시각들이 서로 어긋납니다. 계정 발급이 그 예였습니다 — users 와
 * user_identities 가 각각 시계를 읽어서 두 행의 시각이 1초 다를 수 있었습니다.
 *
 * <p>조회에서도 같습니다. 친구 목록에서 접속 상태를 판정할 때 행마다 시계를 읽으면
 * 판정 기준이 행마다 미세하게 달라집니다. 한 번 읽어 모든 행에 같은 기준을 씁니다.
 *
 * <p>Clock 을 주입받는 이유는 테스트에서 시각을 고정할 수 있게 하려는 것입니다. 지금
 * 그렇게 하는 테스트는 없지만, 시각에 의존하는 로직이 늘면 필요해집니다.
 */
@Component
@RequiredArgsConstructor
public class TimeProvider {

    private final Clock clock;

    /** 지금 시각. CHAR(14) UTC 문자열입니다. */
    public String now() {
        return Timestamps.format(clock.instant());
    }

    /** 지금보다 duration 만큼 과거. 타임아웃 기준 시각을 만드는 데 씁니다. */
    public String minus(Duration duration) {
        return Timestamps.format(clock.instant().minus(duration));
    }
}
