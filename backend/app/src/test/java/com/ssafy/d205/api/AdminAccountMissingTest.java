package com.ssafy.d205.api;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.test.context.TestPropertySource;
import org.springframework.test.web.servlet.MockMvc;

import java.util.UUID;

import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.ssafy.d205.support.IntegrationTest;

/**
 * 관리자 계정이 설정되지 않은 서버.
 *
 * <p>운영에서 실제로 일어날 상황입니다. .env 에 ADMIN_USERNAME 과 ADMIN_PASSWORD 를
 * 넣지 않은 채로 배포하면 이렇게 됩니다.
 *
 * <p>그때 <b>게임이 멈추면 안 됩니다.</b> 관리 화면은 운영자 편의이고 게임은 서비스
 * 자체라, 관리자 계정 하나 때문에 서버가 뜨지 않거나 계정 발급이 막히면 훨씬 나쁜
 * 고장입니다. SecurityConfig 는 계정을 등록하지 않고 경고만 남기는데, 그것이 실제로
 * 그렇게 동작하는지는 아무도 확인하지 않고 있었습니다.
 *
 * <p>&#64;TestPropertySource 로 값을 비웁니다. 스프링 컨텍스트는 따로 뜨지만 컨테이너는
 * IntegrationTest 의 것을 그대로 씁니다. 여기서 컨테이너를 새로 선언하면 한 번의 테스트
 * 실행에 MySQL 이 둘 뜹니다.
 */
@TestPropertySource(properties = {"admin.username=", "admin.password="})
class AdminAccountMissingTest extends IntegrationTest {

    @Autowired
    MockMvc mvc;

    @Test
    @DisplayName("계정이 없어도 게임 API 는 그대로 동작한다")
    void theGameKeepsWorking() throws Exception {
        mvc.perform(post("/api/v1/accounts")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"deviceId\":\"" + UUID.randomUUID() + "\"}"))
                .andExpect(status().isCreated());

        mvc.perform(get("/actuator/health"))
                .andExpect(status().isOk());
    }

    @Test
    @DisplayName("아무 비밀번호로도 로그인할 수 없다")
    void nobodyCanLogIn() throws Exception {
        // 등록된 계정이 없을 때 스프링이 예외를 던져 500 이 나거나, 반대로 통과해
        // 버리는 일이 없어야 한다. 둘 다 실제로 있는 실수다.
        mvc.perform(post("/api/v1/admin/session")
                        .param("username", "admin")
                        .param("password", "admin"))
                .andExpect(status().isUnauthorized())
                .andExpect(jsonPath("$.code").value("BAD_CREDENTIALS"));
    }

    @Test
    @DisplayName("관리자 경로는 여전히 막혀 있다")
    void adminPathsStayClosed() throws Exception {
        mvc.perform(get("/api/v1/admin/session"))
                .andExpect(status().isUnauthorized());
    }
}
