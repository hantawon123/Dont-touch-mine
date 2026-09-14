package com.ssafy.d205.ops;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;
import tools.jackson.databind.ObjectMapper;

import java.time.Duration;
import java.time.Instant;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.ssafy.d205.domain.ops.entity.OpsSample;
import com.ssafy.d205.domain.ops.repository.OpsSampleRepository;
import com.ssafy.d205.domain.ops.service.OpsSampler;
import com.ssafy.d205.domain.ops.service.OpsSweeper;
import com.ssafy.d205.global.common.Timestamps;
import com.ssafy.d205.support.IntegrationTest;

/**
 * 운영 지표 샘플러와 스윕 (S15P21D205-1003).
 *
 * <p>스케줄러는 테스트 프로필에서 사실상 꺼져 있고(application-test.yml) 여기서 직접 부릅니다.
 * 소켓 연결 수는 진짜 소켓이 없어 0 이고, 그 값은 NotificationSessionRegistry 의 connectionCount 가
 * 다른 테스트에서 이미 고정하므로 여기서 다시 보지 않습니다.
 */
class OpsSamplerTest extends IntegrationTest {

    private static final String USER_ID_HEADER = "X-User-Id";
    private static final String DEVICE_ID_HEADER = "X-Device-Id";

    @Autowired
    MockMvc mvc;

    @Autowired
    ObjectMapper objectMapper;

    @Autowired
    OpsSampler sampler;

    @Autowired
    OpsSweeper sweeper;

    @Autowired
    OpsSampleRepository samples;

    @Test
    @DisplayName("한 번 재면 접속 상태별 인원과 가입 수가 한 행에 남는다")
    void aSampleCapturesPresenceAndSignups() throws Exception {
        // 직전 샘플(다른 테스트가 남긴 것)과 같은 초에 만든 계정은 "직전 샘플 이후"에 들지 않습니다.
        // 시각이 초 단위라 한 초를 넘기고 시작합니다.
        Thread.sleep(1100);
        String lobby = createUser();
        String match = createUser();
        presence(lobby, "7K2M9P", "LOBBY");
        presence(match, "7K2M9P", "MATCH");

        OpsSample sample = sampler.sample();

        assertThat(sample.getSampledAt()).hasSize(14);
        assertThat(sample.getInLobby()).isGreaterThanOrEqualTo(1);
        assertThat(sample.getInGame()).isGreaterThanOrEqualTo(1);
        // 이 테스트가 만든 두 명은 직전 샘플(또는 간격만큼 전) 이후 가입입니다.
        assertThat(sample.getSignups()).isGreaterThanOrEqualTo(2);
        assertThat(samples.findById(sample.getSampledAt())).isPresent();
    }

    @Test
    @DisplayName("가입 수는 직전 샘플 이후 증분이다")
    void signupsAreIncrementsSinceTheLastSample() throws Exception {
        sampler.sample();
        // 같은 초에 다시 재면 PK 가 겹치므로 시각이 넘어가길 기다립니다. 실제 운영은 1분 간격입니다.
        Thread.sleep(1100);

        OpsSample quiet = sampler.sample();
        assertThat(quiet.getSignups()).isZero();
    }

    @Test
    @DisplayName("탈퇴는 이벤트로 세어 다음 샘플에 실리고, 실린 뒤에는 0 으로 돌아간다")
    void deletionsAreCountedOnceViaTheEvent() throws Exception {
        sampler.sample();
        Thread.sleep(1100);
        String device = UUID.randomUUID().toString();
        String leaver = createUser(device);

        mvc.perform(delete("/api/v1/accounts/me")
                        .header(USER_ID_HEADER, leaver)
                        .header(DEVICE_ID_HEADER, device))
                .andExpect(status().isNoContent());

        OpsSample after = sampler.sample();
        assertThat(after.getDeletions()).isEqualTo(1);

        Thread.sleep(1100);
        assertThat(sampler.sample().getDeletions()).isZero();
    }

    @Test
    @DisplayName("서버 자원 값은 비어 있지 않다")
    void gaugesAreRead() {
        OpsSample sample = sampler.sample();

        // 힙 상한은 JVM 이 늘 알고, 커넥션 풀은 두 데이터소스가 떠 있으니 0 일 수 없습니다.
        // CPU 는 첫 측정에서 0 이 나올 수 있어(측정 구간이 없음) 범위만 봅니다.
        assertThat(sample.getHeapMaxMb()).isPositive();
        assertThat(sample.getHeapUsedMb()).isPositive();
        assertThat(sample.getDbPoolMax()).isPositive();
        assertThat(sample.getCpuPct().doubleValue()).isBetween(0.0, 100.0);
    }

    @Test
    @DisplayName("보관 기간을 넘긴 행만 지운다")
    void sweepRemovesOnlyOldRows() {
        String old = Timestamps.format(Instant.now().minus(Duration.ofDays(31)));
        String recent = Timestamps.format(Instant.now().minus(Duration.ofDays(29)));
        samples.save(new OpsSample(old, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        samples.save(new OpsSample(recent, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        int swept = sweeper.sweep();

        assertThat(swept).isGreaterThanOrEqualTo(1);
        assertThat(samples.findById(old)).isEmpty();
        assertThat(samples.findById(recent)).isPresent();
    }

    private void presence(String userId, String sessionId, String kind) throws Exception {
        mvc.perform(put("/api/v1/presence")
                        .header(USER_ID_HEADER, userId)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"sessionId\":\"" + sessionId + "\",\"sessionKind\":\"" + kind + "\"}"))
                .andExpect(status().isNoContent());
    }

    private String createUser() throws Exception {
        return createUser(UUID.randomUUID().toString());
    }

    private String createUser(String deviceId) throws Exception {
        String body = mvc.perform(post("/api/v1/accounts")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"deviceId\":\"" + deviceId + "\"}"))
                .andExpect(status().isCreated())
                .andReturn().getResponse().getContentAsString();
        return objectMapper.readTree(body).get("userId").asText();
    }
}
