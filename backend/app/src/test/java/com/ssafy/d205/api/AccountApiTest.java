package com.ssafy.d205.api;

import tools.jackson.databind.ObjectMapper;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.RequestBuilder;

import java.util.List;
import java.util.UUID;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import java.util.concurrent.TimeUnit;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.patch;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.ssafy.d205.domain.user.dto.IssueAccountRequest;
import com.ssafy.d205.domain.user.dto.UpdateNicknameRequest;
import com.ssafy.d205.domain.user.entity.NicknamePolicy;
import com.ssafy.d205.support.IntegrationTest;

class AccountApiTest extends IntegrationTest {

    private static final String USER_ID_HEADER = "X-User-Id";

    @Autowired
    MockMvc mvc;

    @Autowired
    ObjectMapper objectMapper;

    @Autowired
    org.springframework.jdbc.core.JdbcTemplate jdbcTemplate;

    @Test
    @DisplayName("처음 보는 기기면 계정을 새로 만들고 201을 준다")
    void issuesNewAccount() throws Exception {
        mvc.perform(issueRequest(newDeviceId()))
                .andExpect(status().isCreated())
                .andExpect(jsonPath("$.userId").isNotEmpty())
                .andExpect(jsonPath("$.nickname").isNotEmpty())
                .andExpect(jsonPath("$.appearanceSet").value(false))
                .andExpect(jsonPath("$.createdAt").isNotEmpty());
    }

    @Test
    @DisplayName("같은 기기가 다시 부르면 새로 만들지 않고 같은 계정을 200으로 준다")
    void issueIsIdempotent() throws Exception {
        String deviceId = newDeviceId();

        String first = mvc.perform(issueRequest(deviceId))
                .andExpect(status().isCreated())
                .andReturn().getResponse().getContentAsString();

        String second = mvc.perform(issueRequest(deviceId))
                .andExpect(status().isOk())
                .andReturn().getResponse().getContentAsString();

        assertThat(userIdOf(second)).isEqualTo(userIdOf(first));
    }

    @Test
    @DisplayName("응답에 자격증명인 기기 식별자가 담기지 않는다")
    void responseNeverLeaksCredential() throws Exception {
        String deviceId = newDeviceId();

        String body = mvc.perform(issueRequest(deviceId))
                .andReturn().getResponse().getContentAsString();

        assertThat(body).doesNotContain(deviceId);
    }

    @Test
    @DisplayName("서버가 만든 닉네임은 닉네임 규칙을 통과한다")
    void generatedNicknameSatisfiesPolicy() throws Exception {
        // 단어 목록에 긴 단어를 추가하면 여기서 걸립니다. 서버가 만든 닉네임이
        // 변경 API에서는 거부되는 상태를 막기 위한 검사입니다.
        for (int i = 0; i < 30; i++) {
            String body = mvc.perform(issueRequest(newDeviceId()))
                    .andExpect(status().isCreated())
                    .andReturn().getResponse().getContentAsString();

            String nickname = objectMapper.readTree(body).get("nickname").asText();
            assertThat(NicknamePolicy.isValid(nickname))
                    .withFailMessage("규칙을 어긴 자동 닉네임: %s", nickname)
                    .isTrue();
        }
    }

    @Test
    @DisplayName("발급받은 계정을 조회한다")
    void getsAccount() throws Exception {
        String userId = issueAndGetUserId();

        mvc.perform(get("/api/v1/accounts/me").header(USER_ID_HEADER, userId))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.userId").value(userId));
    }

    @Test
    @DisplayName("없는 계정을 조회하면 404")
    void unknownAccountIsNotFound() throws Exception {
        mvc.perform(get("/api/v1/accounts/me").header(USER_ID_HEADER, UUID.randomUUID().toString()))
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.code").value("ACCOUNT_NOT_FOUND"));
    }

    @Test
    @DisplayName("X-User-Id 헤더가 없으면 400")
    void missingHeaderIsBadRequest() throws Exception {
        mvc.perform(get("/api/v1/accounts/me"))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("MISSING_HEADER"));
    }

    @Test
    @DisplayName("닉네임을 바꾼다")
    void renamesAccount() throws Exception {
        String userId = issueAndGetUserId();
        String nickname = uniqueNickname();

        mvc.perform(renameRequest(userId, nickname))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.nickname").value(nickname));

        mvc.perform(get("/api/v1/accounts/me").header(USER_ID_HEADER, userId))
                .andExpect(jsonPath("$.nickname").value(nickname));
    }

    @Test
    @DisplayName("발급 직후에는 nicknameSet이 false다")
    void generatedNicknameIsNotMarkedAsSet() throws Exception {
        mvc.perform(issueRequest(newDeviceId()))
                .andExpect(status().isCreated())
                .andExpect(jsonPath("$.nicknameSet").value(false));
    }

    @Test
    @DisplayName("닉네임을 바꾸면 nicknameSet이 true가 된다")
    void renameMarksNicknameAsSet() throws Exception {
        String userId = issueAndGetUserId();

        mvc.perform(renameRequest(userId, uniqueNickname()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.nicknameSet").value(true));

        mvc.perform(get("/api/v1/accounts/me").header(USER_ID_HEADER, userId))
                .andExpect(jsonPath("$.nicknameSet").value(true));
    }

    @Test
    @DisplayName("닉네임을 정한 뒤 발급을 다시 불러도 nicknameSet이 유지된다")
    void reissueKeepsNicknameSetFlag() throws Exception {
        // 이 컬럼을 만든 이유입니다. 입력 화면에서 껐다가 다시 켠 상황에서 발급은
        // 200을 돌려주는데, 그것만으로는 닉네임을 정했는지 알 수 없었습니다.
        String deviceId = newDeviceId();
        String userId = userIdOf(mvc.perform(issueRequest(deviceId))
                .andExpect(status().isCreated())
                .andReturn().getResponse().getContentAsString());

        mvc.perform(renameRequest(userId, uniqueNickname())).andExpect(status().isOk());

        mvc.perform(issueRequest(deviceId))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.nicknameSet").value(true));
    }

    @Test
    @DisplayName("닉네임을 두 번 바꿔도 nicknameSet은 true로 남는다")
    void secondRenameKeepsNicknameSetFlag() throws Exception {
        String userId = issueAndGetUserId();

        mvc.perform(renameRequest(userId, uniqueNickname())).andExpect(status().isOk());
        mvc.perform(renameRequest(userId, uniqueNickname()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.nicknameSet").value(true));
    }

    @ParameterizedTest
    @DisplayName("규칙에 맞지 않는 닉네임은 400")
    @ValueSource(strings = {
            "가",
            "열세글자짜리닉네임입니다요",
            "닉네임 사이공백",
            "닉네임!",
            "nick-name",
            "ㅋㅋㅋㅋ"
    })
    void rejectsInvalidNickname(String nickname) throws Exception {
        String userId = issueAndGetUserId();

        mvc.perform(renameRequest(userId, nickname))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("INVALID_REQUEST"));
    }

    @Test
    @DisplayName("한글, 영문, 숫자를 섞은 12글자는 통과한다")
    void acceptsBoundaryNickname() throws Exception {
        String userId = issueAndGetUserId();

        mvc.perform(renameRequest(userId, "가나다Abc123456"))
                .andExpect(status().isOk());
    }

    @Test
    @DisplayName("이미 쓰는 닉네임이면 409")
    void rejectsTakenNickname() throws Exception {
        String nickname = uniqueNickname();
        mvc.perform(renameRequest(issueAndGetUserId(), nickname)).andExpect(status().isOk());

        mvc.perform(renameRequest(issueAndGetUserId(), nickname))
                .andExpect(status().isConflict())
                .andExpect(jsonPath("$.code").value("NICKNAME_TAKEN"));
    }

    @Test
    @DisplayName("대소문자만 다른 닉네임은 서로 다른 것으로 취급된다")
    void nicknameUniquenessIsCaseSensitive() throws Exception {
        // V4가 nickname 컬럼 콜레이션을 utf8mb4_0900_as_cs로 바꿔 대소문자를 구분합니다.
        // 서버 기본값(ai_ci)으로 되돌아가면 두 번째 요청이 409가 되면서 여기서 걸립니다.
        String nickname = uniqueNickname();

        mvc.perform(renameRequest(issueAndGetUserId(), nickname.toLowerCase()))
                .andExpect(status().isOk());

        mvc.perform(renameRequest(issueAndGetUserId(), nickname.toUpperCase()))
                .andExpect(status().isOk());
    }

    @Test
    @DisplayName("남이 쓰는 닉네임과 대소문자만 달라도 내 것으로 바꿀 수 있다")
    void allowsCaseVariantOfOthersNickname() throws Exception {
        String nickname = uniqueNickname();
        mvc.perform(renameRequest(issueAndGetUserId(), nickname.toLowerCase()))
                .andExpect(status().isOk());

        // 자기 닉네임인지 판별할 때 대소문자를 무시하면 중복 검사를 건너뛰어
        // 제약 위반이 500으로 새어 나갑니다. 정확히 비교하는지 확인합니다.
        String userId = issueAndGetUserId();
        mvc.perform(renameRequest(userId, nickname.toUpperCase()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.nickname").value(nickname.toUpperCase()));
    }

    @Test
    @DisplayName("deviceId가 비면 400")
    void rejectsBlankDeviceId() throws Exception {
        mvc.perform(issueRequest(""))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("INVALID_REQUEST"));
    }

    @Test
    @DisplayName("둘이 동시에 같은 닉네임을 노리면 하나만 성공한다")
    void twoPeopleRacingForOneNicknameLeaveOneWinner() throws Exception {
        // 검사와 저장 사이에 창이 있습니다. existsByNickname 이 "비었다"고 답한 뒤
        // 실제로 쓰기까지 사이에 남이 먼저 쓸 수 있어서, 애플리케이션 검사만으로는
        // 막을 수 없습니다. 막는 것은 uk_users_nickname 제약입니다.
        //
        // 사용자에게 중요한 보장은 하나입니다 - 무슨 일이 있어도 닉네임은 겹치지 않는다.
        // 겹치면 검색 결과와 친구 요청에서 사람이 구분되지 않습니다.
        String wanted = uniqueNickname();
        String first = issueAndGetUserId();
        String second = issueAndGetUserId();

        CountDownLatch ready = new CountDownLatch(2);
        CountDownLatch go = new CountDownLatch(1);
        ExecutorService pool = Executors.newFixedThreadPool(2);

        try {
            Future<Integer> a = pool.submit(() -> raceToRename(first, wanted, ready, go));
            Future<Integer> b = pool.submit(() -> raceToRename(second, wanted, ready, go));

            ready.await(10, TimeUnit.SECONDS);
            go.countDown();

            List<Integer> statuses = List.of(a.get(20, TimeUnit.SECONDS), b.get(20, TimeUnit.SECONDS));

            // 겹치지 않고 순서대로 실행됐으면 진 쪽은 NICKNAME_TAKEN, 진짜로 겹쳤으면
            // 제약 위반이라 CONFLICT 입니다. 둘 다 409 이고 둘 다 맞습니다.
            assertThat(statuses).containsExactlyInAnyOrder(200, 409);
        } finally {
            pool.shutdownNow();
        }

        // 이긴 쪽만 그 이름을 갖습니다. 진 쪽은 트랜잭션이 통째로 되돌아가 이름이
        // 그대로입니다.
        assertThat(countWithNickname(wanted)).isOne();
    }

    /**
     * 두 스레드가 같은 순간에 이름 변경을 시도하게 합니다.
     *
     * <p>완전히 같은 순간을 보장할 수는 없습니다. 겹치면 제약 위반 경로를, 겹치지 않으면
     * 애플리케이션 검사 경로를 지나는데 <b>어느 쪽이든 결과는 같아야 합니다</b> -
     * 하나만 성공. 그래서 이 테스트는 타이밍에 따라 흔들리지 않습니다.
     */
    private int raceToRename(String userId, String nickname,
                             CountDownLatch ready, CountDownLatch go) throws Exception {
        ready.countDown();
        go.await(10, TimeUnit.SECONDS);

        return mvc.perform(renameRequest(userId, nickname))
                .andReturn().getResponse().getStatus();
    }

    private int countWithNickname(String nickname) {
        return jdbcTemplate.queryForObject(
                "SELECT COUNT(*) FROM users WHERE nickname = ?", Integer.class, nickname);
    }

    private static String newDeviceId() {
        return UUID.randomUUID().toString();
    }

    /**
     * 12글자 규칙 안에서 테스트마다 겹치지 않는 닉네임을 만듭니다.
     * "Nick" + 16진수 8자라 정확히 12글자이고 영숫자만 씁니다.
     */
    private static String uniqueNickname() {
        return "Nick" + UUID.randomUUID().toString().replace("-", "").substring(0, 8);
    }

    private RequestBuilder issueRequest(String deviceId) throws Exception {
        return post("/api/v1/accounts")
                .contentType(MediaType.APPLICATION_JSON)
                .content(objectMapper.writeValueAsString(new IssueAccountRequest(deviceId)));
    }

    private RequestBuilder renameRequest(String userId, String nickname) throws Exception {
        return patch("/api/v1/accounts/me")
                .header(USER_ID_HEADER, userId)
                .contentType(MediaType.APPLICATION_JSON)
                .content(objectMapper.writeValueAsString(new UpdateNicknameRequest(nickname)));
    }

    private String issueAndGetUserId() throws Exception {
        String body = mvc.perform(issueRequest(newDeviceId()))
                .andExpect(status().isCreated())
                .andReturn().getResponse().getContentAsString();
        return userIdOf(body);
    }

    private String userIdOf(String body) throws Exception {
        return objectMapper.readTree(body).get("userId").asText();
    }
}
