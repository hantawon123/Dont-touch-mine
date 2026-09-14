package com.ssafy.d205;

import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.scheduling.annotation.EnableScheduling;

/**
 * 플레이 로그 수집 서비스의 진입점.
 *
 * <p>패키지가 계정 서비스의 {@code D205Application} 과 같은 이유는 common 모듈의 빈(시각, 토큰 서명)이
 * {@code com.ssafy.d205} 아래에 있어서입니다. 여기서 스캔이 시작되어야 그것들이 잡힙니다. 두 진입점은
 * 서로 다른 jar 에 들어가므로 같은 패키지여도 만나지 않습니다.
 *
 * <p>스케줄링을 여기서 켭니다. flush 와 요약 로그가 스케줄러입니다.
 */
@SpringBootApplication
@EnableScheduling
public class AnalyticsApplication {

    public static void main(String[] args) {
        SpringApplication.run(AnalyticsApplication.class, args);
    }
}
