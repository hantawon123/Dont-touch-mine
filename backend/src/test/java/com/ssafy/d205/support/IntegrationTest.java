package com.ssafy.d205.support;

import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.testcontainers.service.connection.ServiceConnection;
import org.springframework.test.context.ActiveProfiles;
import org.testcontainers.mysql.MySQLContainer;

/**
 * 실제 MySQL에 대고 도는 테스트의 공통 설정입니다.
 *
 * <p>H2로 대체하지 않는 이유가 있습니다. 이 프로젝트는 CHAR(14) 시각 문자열, 대소문자를
 * 구분하는 콜레이션, CHECK 제약처럼 <b>DB 동작에 기대는 설계</b>가 여러 곳에 있습니다.
 * 다른 DB로 시험하면 그 부분이 검증되지 않습니다.
 *
 * <p>profile을 test로 두는 것은 application.yml의 spring.profiles.default가 local이라
 * 그대로 두면 개발자 PC의 localhost:3307을 물기 때문입니다. 접속 정보는
 * &#64;ServiceConnection이 컨테이너에서 받아 넣습니다.
 */
@SpringBootTest
@AutoConfigureMockMvc
@ActiveProfiles("test")
public abstract class IntegrationTest {

    /**
     * 컨테이너를 static 초기화로 직접 띄우고 JUnit에 맡기지 않습니다.
     *
     * <p>&#64;Testcontainers + &#64;Container로 두면 <b>클래스가 끝날 때 컨테이너가
     * 정지합니다.</b> 그런데 스프링 컨텍스트는 클래스 사이에 재사용되므로, 두 번째
     * 테스트 클래스는 살아 있는 컨텍스트가 이미 죽은 컨테이너를 가리키는 상태로
     * 시작합니다. 증상이 고약합니다. 커넥션 획득이 30초씩 타임아웃되면서 DB를 쓰는
     * 테스트만 전부 실패하고, DB를 안 쓰는 테스트는 통과해서 원인이 잘 안 보입니다.
     *
     * <p>직접 띄우면 컨테이너가 JVM 수명 전체를 삽니다. 회수는 Testcontainers의 Ryuk이
     * JVM 종료 후에 처리하므로 남지 않습니다.
     *
     * <p>콜레이션을 명시하는 이유: 서버 기본값은 utf8mb4_0900_ai_ci라 대소문자를
     * 구분하지 않습니다. V4가 nickname 컬럼만 as_cs로 바꾸는데, 기본값이 무엇이냐에
     * 따라 <b>다른 컬럼들의 동작이 테스트와 운영에서 갈라질 수 있습니다.</b> 운영
     * compose와 같은 값을 박아 두면 그때 테스트가 먼저 알려줍니다.
     */
    @ServiceConnection
    static final MySQLContainer MYSQL = new MySQLContainer("mysql:8.4")
            .withCommand("--character-set-server=utf8mb4", "--collation-server=utf8mb4_0900_ai_ci");

    static {
        MYSQL.start();
    }
}
