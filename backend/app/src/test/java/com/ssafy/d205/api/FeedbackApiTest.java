package com.ssafy.d205.api;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.ResultActions;
import tools.jackson.databind.ObjectMapper;

import java.util.List;
import java.util.Map;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.ssafy.d205.support.IntegrationTest;

/**
 * 설정 화면의 피드백 보내기.
 *
 * <p>신고와 같은 어려움이 있습니다. <b>피드백의 효과는 없음입니다.</b> 보낸 사람에게
 * 아무 변화가 없고 클라이언트가 다시 읽을 API 도 없어서, 대부분의 테스트가 DB 를 직접
 * 읽습니다.
 *
 * <p>여기서 고정하려는 것은 세 가지입니다. 화면 상한(500자)과 서버 상한이 <b>같다</b>는
 * 것, 공백만 보낸 것은 피드백이 아니라는 것, 그리고 쓴 사람이 탈퇴해도 내용은 남는다는
 * 것입니다. 첫 번째가 어긋나면 화면에서 다 채운 글이 400 으로 버려지고, 그 글은
 * 복구할 방법이 없습니다.
 */
class FeedbackApiTest extends IntegrationTest {

    private static final String USER_ID_HEADER = "X-User-Id";
    private static final String DEVICE_ID_HEADER = "X-Device-Id";
    private static final String FEEDBACK = "/api/v1/feedback";

    /** 클라이언트 화면의 상한과 같은 값입니다(SettingsStyle.Feedback.MaxLength). */
    private static final int MAX_MESSAGE = 500;

    @Autowired
    MockMvc mvc;

    @Autowired
    ObjectMapper objectMapper;

    @Autowired
    JdbcTemplate jdbcTemplate;

    @Test
    @DisplayName("보낸 피드백은 본문과 빌드 정보가 그대로 남는다")
    void feedbackIsStored() throws Exception {
        String me = createUser();

        send(me, "숨는 시간이 너무 짧아요", "1.4.2", "WebGL").andExpect(status().isCreated());

        Map<String, Object> row = onlyFeedbackOf(me);
        assertThat(row.get("message")).isEqualTo("숨는 시간이 너무 짧아요");
        assertThat(row.get("build_ver")).isEqualTo("1.4.2");
        assertThat(row.get("platform")).isEqualTo("WebGL");
        // INT UNSIGNED 는 드라이버가 Long 으로 줍니다. 값이 같아도 타입이 달라
        // isEqualTo 가 실패하므로 intValue 로 맞춥니다.
        assertThat(((Number) row.get("author_seq")).intValue()).isEqualTo(seqOf(me));
        assertThat((String) row.get("created_at")).hasSize(14);
    }

    @Test
    @DisplayName("빌드와 플랫폼은 없어도 받는다")
    void clientContextIsOptional() throws Exception {
        // 이 값이 빠졌다고 피드백을 버리는 것이 더 큰 손해라는 판단을 고정합니다.
        String me = createUser();

        mvc.perform(post(FEEDBACK)
                        .header(USER_ID_HEADER, me)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"message\":\"소리가 너무 커요\"}"))
                .andExpect(status().isCreated());

        Map<String, Object> row = onlyFeedbackOf(me);
        assertThat(row.get("build_ver")).isNull();
        assertThat(row.get("platform")).isNull();
    }

    @Test
    @DisplayName("Unity 가 실제로 보내는 모양을 그대로 받는다")
    void acceptsTheShapeTheClientSends() throws Exception {
        // 클라이언트는 JsonUtility 로 본문을 만듭니다. 그것은 <b>필드를 생략하지 않고
        // null 문자열을 "" 로 씁니다</b>(ReportGateway 의 memo 주석). 그래서 화면이 빌드
        // 정보를 채우지 않아도 빈 문자열이 옵니다. 빈 문자열을 없음과 같게 다루지 않으면
        // 그 시점의 모든 피드백이 400 이 됩니다.
        //
        // Content-Type 은 UnityWebRequestTransport 가 application/json 으로 넣습니다.
        String me = createUser();

        mvc.perform(post(FEEDBACK)
                        .header(USER_ID_HEADER, me)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"message\":\"보내기를 눌렀습니다\",\"buildVer\":\"\",\"platform\":\"\"}"))
                .andExpect(status().isCreated());

        Map<String, Object> row = onlyFeedbackOf(me);
        assertThat(row.get("message")).isEqualTo("보내기를 눌렀습니다");
        assertThat(row.get("build_ver")).isNull();
        assertThat(row.get("platform")).isNull();
    }

    @Test
    @DisplayName("500자는 받고 501자는 거절한다")
    void messageLimitMatchesTheScreen() throws Exception {
        String me = createUser();

        send(me, "가".repeat(MAX_MESSAGE), null, null).andExpect(status().isCreated());
        send(me, "가".repeat(MAX_MESSAGE + 1), null, null)
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("INVALID_REQUEST"));

        // 한글 500자는 utf8mb4 에서 1,500 바이트입니다. 컬럼이 글자 수로 500 이라는
        // 것을 여기서 확인합니다. 바이트로 세는 DB 라면 위 요청이 저장에서 깨집니다.
        assertThat(feedbackCountOf(me)).isEqualTo(1);
    }

    @Test
    @DisplayName("공백만 보낸 것은 피드백이 아니다")
    void blankIsNotFeedback() throws Exception {
        // 화면도 같은 규칙으로 보내기 버튼을 잠그지만, 화면을 믿고 검사를 빼면 화면이
        // 바뀔 때 빈 행이 조용히 쌓입니다.
        String me = createUser();

        send(me, "   \n  ", null, null)
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("INVALID_REQUEST"));

        assertThat(feedbackCountOf(me)).isZero();
    }

    @Test
    @DisplayName("앞뒤 공백은 떼고 저장한다")
    void surroundingWhitespaceIsTrimmed() throws Exception {
        String me = createUser();

        send(me, "  마지막 줄바꿈이 붙는 경우\n\n", null, null).andExpect(status().isCreated());

        assertThat(onlyFeedbackOf(me).get("message")).isEqualTo("마지막 줄바꿈이 붙는 경우");
    }

    @Test
    @DisplayName("누가 보냈는지 모르면 받지 않는다")
    void callerIsRequired() throws Exception {
        mvc.perform(post(FEEDBACK)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"message\":\"익명입니다\"}"))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("MISSING_HEADER"));

        send(UUID.randomUUID().toString(), "없는 계정입니다", null, null)
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.code").value("ACCOUNT_NOT_FOUND"));
    }

    @Test
    @DisplayName("같은 사람이 여러 번 보내면 여러 건이 쌓인다")
    void repeatedFeedbackIsKept() throws Exception {
        // 횟수 자체가 신호라서 중복을 막지 않습니다. 막으면 "이 부분이 계속 불편하다"는
        // 반복이 한 건으로 접혀 사라집니다.
        String me = createUser();

        send(me, "같은 말 1", null, null).andExpect(status().isCreated());
        send(me, "같은 말 2", null, null).andExpect(status().isCreated());

        assertThat(feedbackCountOf(me)).isEqualTo(2);
    }

    @Test
    @DisplayName("보낸 사람이 탈퇴해도 내용은 남고 작성자만 비워진다")
    void authorIsClearedOnDeletionButTextRemains() throws Exception {
        // V14 의 ON DELETE SET NULL 을 스키마를 믿지 않고 확인합니다. CASCADE 였다면
        // 떠난 사람의 지적이 통째로 사라집니다.
        // 탈퇴는 기기 식별자를 자격증명으로 요구하므로 발급에 쓴 값을 들고 있어야 합니다.
        String deviceId = UUID.randomUUID().toString();
        String me = createUser(deviceId);
        send(me, "탈퇴 전에 남기는 말", null, null).andExpect(status().isCreated());

        mvc.perform(delete("/api/v1/accounts/me")
                        .header(USER_ID_HEADER, me)
                        .header(DEVICE_ID_HEADER, deviceId))
                .andExpect(status().isNoContent());

        List<Map<String, Object>> orphans = jdbcTemplate.queryForList("""
                SELECT message, author_seq
                  FROM user_feedback
                 WHERE message = '탈퇴 전에 남기는 말'
                """);
        assertThat(orphans).hasSize(1);
        assertThat(orphans.get(0).get("author_seq")).isNull();
    }

    private ResultActions send(String caller, String message, String buildVer, String platform)
            throws Exception {
        String body = objectMapper.writeValueAsString(
                Map.of("message", message,
                        "buildVer", buildVer == null ? "" : buildVer,
                        "platform", platform == null ? "" : platform));

        return mvc.perform(post(FEEDBACK)
                .header(USER_ID_HEADER, caller)
                .contentType(MediaType.APPLICATION_JSON)
                .content(body));
    }

    private Map<String, Object> onlyFeedbackOf(String userId) {
        return jdbcTemplate.queryForMap("""
                SELECT f.message, f.build_ver, f.platform, f.author_seq, f.created_at
                  FROM user_feedback f
                  JOIN users u ON u.users_seq = f.author_seq
                 WHERE u.public_id = ?
                """, userId);
    }

    private int feedbackCountOf(String userId) {
        return jdbcTemplate.queryForObject("""
                SELECT COUNT(*)
                  FROM user_feedback f
                  JOIN users u ON u.users_seq = f.author_seq
                 WHERE u.public_id = ?
                """, Integer.class, userId);
    }

    private int seqOf(String userId) {
        return jdbcTemplate.queryForObject(
                "SELECT users_seq FROM users WHERE public_id = ?", Integer.class, userId);
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
