package com.ssafy.d205.analytics;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.jdbc.core.JdbcTemplate;

import static org.assertj.core.api.Assertions.assertThat;

import com.ssafy.d205.support.AnalyticsIntegrationTest;

/**
 * 분석 스키마가 이 서비스의 Flyway 로 준비되는지 봅니다.
 *
 * <p>서비스가 나뉘기 전에는 "게임 스키마에 분석 마이그레이션이 섞이지 않는가"를 보는 테스트가 있었습니다.
 * 이제 두 DB 는 다른 컨테이너라 섞일 방법이 없고, 그 검증은 계정 서비스 쪽에 game_event 가 없다는 것으로
 * 충분합니다. 여기서는 이 서비스가 자기 스키마를 스스로 만든다는 것만 봅니다.
 */
class AnalyticsSchemaTest extends AnalyticsIntegrationTest {

    @Autowired
    JdbcTemplate analytics;

    @Test
    @DisplayName("game_event 와 분석 뷰가 기동 시점에 준비된다")
    void schemaIsReadyAtStartup() {
        Integer tables = analytics.queryForObject("""
                SELECT COUNT(*) FROM information_schema.tables
                 WHERE table_schema = ? AND table_name = 'game_event'
                """, Integer.class, ANALYTICS_SCHEMA);
        Integer views = analytics.queryForObject("""
                SELECT COUNT(*) FROM information_schema.views
                 WHERE table_schema = ? AND table_name LIKE 'match_analysis_%'
                """, Integer.class, ANALYTICS_SCHEMA);

        assertThat(tables).isOne();
        assertThat(views).as("V2~V4 의 집계 뷰가 만들어져야 합니다").isGreaterThanOrEqualTo(3);
    }

    @Test
    @DisplayName("Flyway 이력이 db/migration-analytics 의 것만 담는다")
    void flywayHistoryIsAnalyticsOnly() {
        Integer analyticsScripts = analytics.queryForObject(
                "SELECT COUNT(*) FROM flyway_schema_history WHERE script LIKE '%game_event%'", Integer.class);
        Integer gameScripts = analytics.queryForObject(
                "SELECT COUNT(*) FROM flyway_schema_history WHERE script LIKE '%create_users%'", Integer.class);

        assertThat(analyticsScripts).isOne();
        assertThat(gameScripts)
                .as("계정 서비스의 db/migration 이 이 모듈의 클래스패스에 들어왔습니다. 의존 방향을 확인하세요.")
                .isZero();
    }
}
