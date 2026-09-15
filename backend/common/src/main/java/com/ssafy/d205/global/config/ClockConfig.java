package com.ssafy.d205.global.config;

import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

import java.time.Clock;

/**
 * 시계를 빈으로 둡니다.
 *
 * <p>Instant.now() 를 직접 부르는 코드는 테스트에서 시각을 고정할 수 없습니다. 빈으로
 * 두면 테스트가 Clock.fixed() 로 갈아끼울 수 있습니다.
 *
 * <p>systemUTC 인 이유는 저장 형식이 UTC 문자열이기 때문입니다. 기본 시계
 * (Clock.systemDefaultZone) 를 쓰면 서버 타임존이 섞여 들어갈 여지가 생깁니다.
 * 형식 변환도 UTC 로 고정되어 있어 이중으로 막습니다.
 */
@Configuration
public class ClockConfig {

    @Bean
    public Clock clock() {
        return Clock.systemUTC();
    }
}
