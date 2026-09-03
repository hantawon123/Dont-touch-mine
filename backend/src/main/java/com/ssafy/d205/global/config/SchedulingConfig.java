package com.ssafy.d205.global.config;

import org.springframework.context.annotation.Configuration;
import org.springframework.scheduling.annotation.EnableScheduling;

/**
 * 주기 작업을 켭니다.
 *
 * <p>애너테이션 하나짜리 클래스를 따로 두는 이유는, 이걸 메인 클래스에 붙이면 "이
 * 애플리케이션에 주기 작업이 있다"는 사실이 눈에 띄지 않기 때문입니다. 여기 있으면
 * global/config 를 열어보는 사람이 바로 봅니다.
 *
 * <p>지금 도는 것은 PresenceSweeper 하나입니다. 앱 인스턴스가 여러 개로 늘어나면 같은
 * 작업이 동시에 돌게 되므로 그때 검토가 필요합니다.
 */
@Configuration
@EnableScheduling
public class SchedulingConfig {
}
