package com.ssafy.d205;

import org.junit.jupiter.api.Test;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.testcontainers.service.connection.ServiceConnection;
import org.springframework.test.context.ActiveProfiles;
import org.testcontainers.junit.jupiter.Container;
import org.testcontainers.junit.jupiter.Testcontainers;
import org.testcontainers.mysql.MySQLContainer;

// 테스트가 자기 DB를 직접 띄웁니다. 파이프라인이 DB를 주입하는 방식이면
// 로컬에서 ./gradlew test 가 돌지 않아 CI와 결과가 갈립니다.
@SpringBootTest
@Testcontainers
// local 프로필을 피합니다. application.yml이 default를 local로 두고 있어서
// 명시하지 않으면 application-local.yml의 localhost:3307을 집어옵니다.
// @ServiceConnection이 접속 정보를 덮어쓰지만, 의도를 드러내는 편이 낫습니다.
@ActiveProfiles("test")
class D205ApplicationTests {

	// 운영과 같은 8.4를 씁니다. 마이그레이션이 utf8mb4_0900_ai_ci 콜레이션과
	// CHECK 제약을 쓰므로 버전이 낮으면 여기서 통과해도 운영에서 깨집니다.
	@Container
	@ServiceConnection
	// Testcontainers 2.x에서 셀프 타이핑 제네릭이 제거되어 MySQLContainer는
	// 더 이상 타입 파라미터를 받지 않습니다. 1.x의 MySQLContainer<?> 는 컴파일되지 않습니다.
	static MySQLContainer mysql = new MySQLContainer("mysql:8.4");

	// 컨텍스트가 뜨면 Flyway가 V1~V3을 빈 DB에 적용하고 Hibernate가
	// validate를 통과했다는 뜻입니다. 마이그레이션 문법 오류가 여기서 잡힙니다.
	@Test
	void contextLoads() {
	}
}
