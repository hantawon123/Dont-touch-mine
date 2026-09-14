package com.ssafy.d205.support;

import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.testcontainers.service.connection.ServiceConnection;
import org.springframework.test.context.ActiveProfiles;
import org.testcontainers.mysql.MySQLContainer;
import org.testcontainers.utility.MountableFile;

/**
 * 실제 MySQL 에 대고 도는 수집 서비스 테스트의 공통 설정입니다.
 *
 * <p>계정 서비스의 IntegrationTest 와 같은 방식이고 컨테이너만 다릅니다. 데이터베이스 이름이
 * 운영과 같은 d205_analytics 이고, 읽기 계정과 Metabase 계정을 만드는 초기화 스크립트를 운영·로컬
 * compose 와 <b>같은 파일</b>로 넣습니다. 테스트용 사본을 두면 둘이 어긋나도 아무도 모릅니다.
 *
 * <p>컨테이너를 static 초기화로 직접 띄우는 이유는 IntegrationTest 주석과 같습니다. JUnit 에 맡기면
 * 클래스가 끝날 때 컨테이너가 죽는데 스프링 컨텍스트는 클래스 사이에 재사용됩니다.
 *
 * <p>작업 디렉터리는 backend/ 입니다(루트 build.gradle 이 test.workingDir 로 정합니다).
 */
@SpringBootTest
@AutoConfigureMockMvc
@ActiveProfiles("test")
public abstract class AnalyticsIntegrationTest {

    protected static final String ANALYTICS_SCHEMA = "d205_analytics";

    /** 읽기 전용 계정 비밀번호. 운영은 .env 에서, 여기서는 이 상수로 초기화 스크립트에 넘깁니다. */
    protected static final String READER_PASSWORD = "reader-test";

    protected static final String METABASE_PASSWORD = "metabase-test";

    @ServiceConnection
    protected static final MySQLContainer MYSQL = new MySQLContainer("mysql:8.4")
            .withDatabaseName(ANALYTICS_SCHEMA)
            .withCommand("--character-set-server=utf8mb4", "--collation-server=utf8mb4_0900_ai_ci")
            .withEnv("ANALYTICS_READER_PASSWORD", READER_PASSWORD)
            .withEnv("METABASE_DB_PASSWORD", METABASE_PASSWORD)
            .withCopyFileToContainer(
                    MountableFile.forHostPath("deploy/mysql-analytics/init/01-accounts.sh"),
                    "/docker-entrypoint-initdb.d/01-accounts.sh");

    static {
        MYSQL.start();
    }
}
