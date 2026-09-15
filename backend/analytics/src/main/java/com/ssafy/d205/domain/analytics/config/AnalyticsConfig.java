package com.ssafy.d205.domain.analytics.config;

import org.springframework.boot.context.properties.EnableConfigurationProperties;
import org.springframework.context.annotation.Configuration;

/**
 * {@link AnalyticsProperties} 를 바인딩합니다.
 *
 * <p>이 프로젝트는 &#64;ConfigurationPropertiesScan 을 쓰지 않아서 레코드에 애너테이션만
 * 붙이면 빈이 되지 않습니다. 메인 클래스에 스캔을 켜는 대신 여기서 명시하는 것은, 설정
 * 클래스가 하나 늘 때마다 "이게 왜 빈이 됐지"를 추적할 곳을 한 군데로 두기 위해서입니다.
 */
@Configuration
@EnableConfigurationProperties(AnalyticsProperties.class)
public class AnalyticsConfig {
}
