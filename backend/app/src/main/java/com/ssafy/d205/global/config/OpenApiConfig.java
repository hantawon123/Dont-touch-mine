package com.ssafy.d205.global.config;

import io.swagger.v3.oas.models.OpenAPI;
import io.swagger.v3.oas.models.info.Info;
import io.swagger.v3.oas.models.servers.Server;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

import java.util.List;

/**
 * API 명세의 제목과 대상 서버를 채웁니다.
 *
 * <p>기본값을 그대로 두면 제목이 "OpenAPI definition", 버전이 "v0" 이고 servers 에는
 * 명세를 요청한 주소가 하나 들어갑니다. 명세를 브라우저에서만 본다면 그래도 상관없지만,
 * 이 프로젝트는 <b>명세를 docs/openapi.json 으로 커밋해 클라이언트에 전달합니다.</b>
 * 그 파일은 MockMvc 로 뽑히므로 servers 가 http://localhost 하나로 굳고, 담당자가
 * Postman 같은 도구로 불러오면 운영에 붙을 방법이 없습니다.
 *
 * <p>그래서 두 주소를 명시합니다. 운영을 먼저 두는 것은 도구들이 목록의 첫 항목을 기본값으로
 * 잡기 때문입니다. 로컬을 기본으로 하고 싶으면 순서를 바꾸면 됩니다.
 *
 * <p>운영 서버는 Swagger 를 열지 않습니다(application.yml). 여기 운영 주소가 있다고 해서
 * 운영에서 명세를 볼 수 있는 것은 아닙니다 — 이 값은 커밋된 파일에 담기는 정보입니다.
 */
@Configuration
public class OpenApiConfig {

    @Bean
    public OpenAPI openApi() {
        return new OpenAPI()
                .info(new Info()
                        .title("D205 API")
                        .version("v1")
                        .description("""
                                기기 식별자 기반 계정, 유저 검색, 친구, 차단, 접속 상태 API.

                                X-User-Id 는 인증이 아니라 식별입니다. 되돌릴 수 없는 연산만 \
                                자격증명(X-Device-Id)을 함께 요구합니다. 오류 코드 표와 \
                                하트비트 규칙은 docs/client-guide.md 를 보세요."""))
                .servers(List.of(
                        new Server().url("https://j15d205.p.ssafy.io").description("운영"),
                        new Server().url("http://localhost:8080").description("로컬")));
    }
}
