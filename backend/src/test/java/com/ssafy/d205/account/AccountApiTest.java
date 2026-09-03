package com.ssafy.d205.account;

import tools.jackson.databind.ObjectMapper;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.RequestBuilder;

import java.util.UUID;

import com.ssafy.d205.support.IntegrationTest;
import com.ssafy.d205.user.NicknamePolicy;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.patch;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

class AccountApiTest extends IntegrationTest {

    private static final String USER_ID_HEADER = "X-User-Id";

    @Autowired
    MockMvc mvc;

    @Autowired
    ObjectMapper objectMapper;

    @Test
    @DisplayName("처음 보는 기기면 계정을 새로 만들고 201을 준다")
    void issuesNewAccount() throws Exception {
        mvc.perform(issueRequest(newDeviceId()))
                .andExpect(status().isCreated())
                .andExpect(jsonPath("$.userId").isNotEmpty())
                .andExpect(jsonPath("$.nickname").isNotEmpty())
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
