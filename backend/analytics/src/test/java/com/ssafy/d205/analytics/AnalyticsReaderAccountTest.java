package com.ssafy.d205.analytics;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.sql.Connection;
import java.sql.DriverManager;
import java.sql.ResultSet;
import java.sql.SQLException;
import java.sql.Statement;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.ssafy.d205.support.AnalyticsIntegrationTest;

/**
 * 분석 DB 컨테이너의 초기화 스크립트(deploy/mysql-analytics/init/01-accounts.sh)가 만드는 계정들의 권한.
 *
 * <p>Metabase 가 붙는 읽기 계정이 쓰기를 못 한다는 것이 핵심입니다. 대시보드 SQL 을 잘못 써도 로그가
 * 지워지지 않아야 합니다. 운영·로컬·테스트가 같은 스크립트 파일을 쓰므로 여기서 통과하면 운영도 같습니다.
 */
class AnalyticsReaderAccountTest extends AnalyticsIntegrationTest {

    @Test
    @DisplayName("읽기 계정은 분석 스키마를 읽을 수 있다")
    void readerCanSelect() throws SQLException {
        try (Connection c = readerConnection(); Statement s = c.createStatement();
             ResultSet rs = s.executeQuery("SELECT COUNT(*) FROM game_event")) {
            assertThat(rs.next()).isTrue();
        }
    }

    @Test
    @DisplayName("읽기 계정은 분석 스키마에 쓸 수 없고 테이블도 못 지운다")
    void readerCannotWrite() throws SQLException {
        try (Connection c = readerConnection(); Statement s = c.createStatement()) {
            assertThatThrownBy(() -> s.executeUpdate("DELETE FROM game_event WHERE 1 = 1"))
                    .isInstanceOf(SQLException.class)
                    .hasMessageContaining("denied");
            assertThatThrownBy(() -> s.executeUpdate("DROP TABLE game_event"))
                    .isInstanceOf(SQLException.class)
                    .hasMessageContaining("denied");
        }
    }

    @Test
    @DisplayName("Metabase 계정은 자기 스키마만 갖는다")
    void metabaseAccountIsConfinedToItsSchema() throws SQLException {
        String url = "jdbc:mysql://" + MYSQL.getHost() + ":" + MYSQL.getFirstMappedPort() + "/metabase";
        try (Connection c = DriverManager.getConnection(url, "metabase", METABASE_PASSWORD);
             Statement s = c.createStatement()) {
            s.executeUpdate("CREATE TABLE IF NOT EXISTS probe (id INT)");
            s.executeUpdate("DROP TABLE probe");
            assertThatThrownBy(() -> s.executeQuery("SELECT COUNT(*) FROM " + ANALYTICS_SCHEMA + ".game_event"))
                    .isInstanceOf(SQLException.class)
                    .hasMessageContaining("denied");
        }
    }

    private static Connection readerConnection() throws SQLException {
        String url = "jdbc:mysql://" + MYSQL.getHost() + ":" + MYSQL.getFirstMappedPort() + "/" + ANALYTICS_SCHEMA;
        return DriverManager.getConnection(url, "d205_reader", READER_PASSWORD);
    }
}
