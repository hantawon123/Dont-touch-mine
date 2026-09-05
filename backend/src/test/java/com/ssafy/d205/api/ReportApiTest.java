package com.ssafy.d205.api;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.ResultActions;
import tools.jackson.databind.ObjectMapper;

import java.util.Map;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.ssafy.d205.domain.report.entity.ReportStatus;
import com.ssafy.d205.domain.report.entity.UserReport;
import com.ssafy.d205.domain.report.repository.UserReportRepository;
import com.ssafy.d205.support.IntegrationTest;

/**
 * 신고.
 *
 * <p>확인하기 어려운 것이 하나 있습니다. <b>신고의 효과는 없음입니다.</b> 저장된다는 것
 * 말고는 관찰할 API 가 없어서 대부분의 테스트가 DB 를 직접 읽습니다. 신고를 조회하는
 * API 를 테스트를 위해 만들 수도 있었지만, 쓰지 않을 API 가 계약으로 굳어지는 편이 더
 * 나쁩니다.
 */
class ReportApiTest extends IntegrationTest {

    private static final String USER_ID_HEADER = "X-User-Id";
    private static final String DEVICE_ID_HEADER = "X-Device-Id";

    @Autowired
    MockMvc mvc;

    @Autowired
    ObjectMapper objectMapper;

    @Autowired
    JdbcTemplate jdbcTemplate;

    @Autowired
    UserReportRepository userReportRepository;

    @Test
    @DisplayName("신고하면 사유와 메모가 그대로 남는다")
    void reportIsStored() throws Exception {
        String me = createUser();
        String other = createUser();

        report(me, other, "ABUSE", "채팅으로 욕설을 했습니다").andExpect(status().isCreated());

        Map<String, Object> row = onlyReportAbout(seqOf(other));
        assertThat(row.get("reason")).isEqualTo("ABUSE");
        assertThat(row.get("memo")).isEqualTo("채팅으로 욕설을 했습니다");
        // INT UNSIGNED 는 드라이버가 Long 으로 줍니다. 값이 같아도 타입이 달라
        // isEqualTo 가 "3 이 아니라 3L" 이라며 실패합니다.
        assertThat(((Number) row.get("reporter_seq")).intValue()).isEqualTo(seqOf(me));
        assertThat(((Number) row.get("reported_seq")).intValue()).isEqualTo(seqOf(other));
        assertThat((String) row.get("created_at")).hasSize(14);
    }

    @Test
    @DisplayName("들어온 신고는 미검토 상태다")
    void aNewReportIsPending() throws Exception {
        // 값을 넣는 것은 컬럼 기본값이 아니라 UserReport 생성자입니다. JPA 가 보내는
        // INSERT 에 status 가 늘 들어 있어서 DEFAULT 는 지나가지 않습니다. 여기서 보는
        // 것은 어느 층이 채우느냐가 아니라 "들어온 신고는 아직 안 본 것"이라는 사실입니다.
        String me = createUser();
        String other = createUser();

        report(me, other, "ABUSE", null).andExpect(status().isCreated());

        Map<String, Object> row = onlyReportAbout(seqOf(other));
        assertThat(row.get("status")).isEqualTo("PENDING");
        assertThat(row.get("reviewed_at")).isNull();
    }

    @Test
    @DisplayName("검토하면 상태와 시각이 함께 적힌다")
    void reviewingWritesBothStatusAndTime() throws Exception {
        // 운영자 API 가 아직 없어서 도메인 메서드를 직접 부릅니다. 인증 없이 열면
        // 아무나 신고를 기각 처리해 숨길 수 있어 API 를 두지 않았습니다.
        String me = createUser();
        String other = createUser();
        report(me, other, "CHEATING", null).andExpect(status().isCreated());

        int seq = (int) (long) (Long) onlyReportAbout(seqOf(other)).get("user_reports_seq");
        UserReport found = userReportRepository.findById(seq).orElseThrow();
        found.review(ReportStatus.DISMISSED, "20260905120000");
        userReportRepository.saveAndFlush(found);

        Map<String, Object> row = onlyReportAbout(seqOf(other));
        assertThat(row.get("status")).isEqualTo("DISMISSED");
        assertThat(row.get("reviewed_at")).isEqualTo("20260905120000");
    }

    @Test
    @DisplayName("신고당한 사람에게는 아무 일도 일어나지 않는다")
    void reportingChangesNothingForTheTarget() throws Exception {
        // 차단을 대신하는 기능이라 차단처럼 동작하리라 기대하기 쉽습니다. 그렇지 않다는
        // 것이 이 기능의 핵심이라 못 박아 둡니다. 검색에서 사라지지도, 친구 요청이
        // 막히지도 않습니다.
        String me = createUser();
        String other = createUser();
        String nickname = nicknameOf(other);

        report(me, other, "ABUSE", null).andExpect(status().isCreated());

        mvc.perform(get("/api/v1/users").param("nickname", nickname).header(USER_ID_HEADER, me))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.users.length()").value(1));

        mvc.perform(post("/api/v1/friend-requests")
                        .header(USER_ID_HEADER, me)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"userId\":\"" + other + "\"}"))
                .andExpect(status().isCreated());
    }

    @Test
    @DisplayName("메모는 없어도 된다")
    void memoIsOptional() throws Exception {
        String me = createUser();
        String other = createUser();

        report(me, other, "CHEATING", null).andExpect(status().isCreated());

        assertThat(onlyReportAbout(seqOf(other)).get("memo")).isNull();
    }

    @Test
    @DisplayName("빈 메모는 없는 것과 같게 저장된다")
    void blankMemoIsStoredAsAbsent() throws Exception {
        // 화면이 비운 칸을 "" 로 보낼지 생략할지는 그쪽 사정입니다. 저장된 뒤에 둘을
        // 구분하면 운영자가 "빈 문자열"과 "없음"을 다른 것으로 보게 됩니다.
        String me = createUser();
        String other = createUser();

        report(me, other, "SPAM", "   ").andExpect(status().isCreated());

        assertThat(onlyReportAbout(seqOf(other)).get("memo")).isNull();
    }

    @Test
    @DisplayName("같은 사람을 여러 번 신고하면 기록이 여러 개 남는다")
    void repeatedReportsAreSeparateRows() throws Exception {
        // 막지 않는 것이 의도입니다. 횟수가 운영자에게 신호가 됩니다. 한 사람이 부풀릴
        // 수 있다는 대가는 알고 있고, 운영자 도구에서 신고 건수와 신고한 사람 수를
        // 나눠 보는 것으로 갚습니다.
        String me = createUser();
        String other = createUser();

        report(me, other, "ABUSE", "한 번").andExpect(status().isCreated());
        report(me, other, "SPAM", "두 번").andExpect(status().isCreated());

        assertThat(reportsAbout(seqOf(other))).isEqualTo(2);
    }

    @Test
    @DisplayName("사유가 목록에 없으면 400 INVALID_REQUEST")
    void unknownReasonIsRejected() throws Exception {
        // 열거형으로 바꾸지 못한 본문은 @Valid 까지 가지 못하고 잭슨에서 멈춥니다.
        // 핸들러가 없으면 스프링 기본 응답이 나가 클라이언트가 code 로 분기할 수 없습니다.
        String me = createUser();
        String other = createUser();

        report(me, other, "NOT_A_REASON", null)
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("INVALID_REQUEST"));

        assertThat(reportsAbout(seqOf(other))).isZero();
    }

    @Test
    @DisplayName("사유가 없으면 400 INVALID_REQUEST")
    void missingReasonIsRejected() throws Exception {
        String me = createUser();
        String other = createUser();

        mvc.perform(post("/api/v1/reports")
                        .header(USER_ID_HEADER, me)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"userId\":\"" + other + "\"}"))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("INVALID_REQUEST"));
    }

    @Test
    @DisplayName("메모가 200자를 넘으면 400 INVALID_REQUEST")
    void tooLongMemoIsRejected() throws Exception {
        String me = createUser();
        String other = createUser();

        report(me, other, "OTHER", "가".repeat(201))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("INVALID_REQUEST"));

        assertThat(reportsAbout(seqOf(other))).isZero();
    }

    @Test
    @DisplayName("200자 메모는 받는다")
    void memoAtTheLimitIsAccepted() throws Exception {
        // 경계에서 한 칸 어긋나는 실수를 잡습니다. 위 테스트만 있으면 199자에서 막아도
        // 통과합니다.
        String me = createUser();
        String other = createUser();

        report(me, other, "OTHER", "가".repeat(200)).andExpect(status().isCreated());

        assertThat(reportsAbout(seqOf(other))).isOne();
    }

    @Test
    @DisplayName("자기 자신은 신고할 수 없다")
    void nobodyReportsThemselves() throws Exception {
        // 스키마의 CHECK 가 막지만 제약 위반이 아니라 뜻이 있는 응답을 줍니다.
        // 초대가 자기 자신을 다루는 방식과 같습니다.
        String me = createUser();

        report(me, me, "ABUSE", null)
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.code").value("TARGET_NOT_FOUND"));

        assertThat(reportsAbout(seqOf(me))).isZero();
    }

    @Test
    @DisplayName("없는 사람은 신고할 수 없다")
    void unknownTargetIsRejected() throws Exception {
        String me = createUser();

        report(me, UUID.randomUUID().toString(), "ABUSE", null)
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.code").value("TARGET_NOT_FOUND"));
    }

    @Test
    @DisplayName("부르는 사람의 계정이 없으면 ACCOUNT_NOT_FOUND")
    void unknownCallerIsRejected() throws Exception {
        // TARGET_NOT_FOUND 와 나뉘어야 합니다. 클라이언트가 할 일이 다릅니다 —
        // 이쪽은 계정을 다시 발급받아야 합니다.
        String other = createUser();

        report(UUID.randomUUID().toString(), other, "ABUSE", null)
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.code").value("ACCOUNT_NOT_FOUND"));
    }

    @Test
    @DisplayName("친구가 아니어도 신고할 수 있다")
    void strangersCanBeReported() throws Exception {
        // 주된 쓰임이 같은 방에서 만난 사람을 신고하는 것입니다. 친구 관계를 요구하면
        // 정작 필요한 자리에서 쓸 수 없습니다.
        String me = createUser();
        String stranger = createUser();

        report(me, stranger, "ABUSE", null).andExpect(status().isCreated());

        assertThat(reportsAbout(seqOf(stranger))).isOne();
    }

    @Test
    @DisplayName("신고자가 탈퇴해도 신고는 남고 신고자만 비워진다")
    void deletingTheReporterKeepsTheirReports() throws Exception {
        // 신고는 신고당한 사람에 대한 기록이지 신고자에 대한 기록이 아닙니다. 목격자가
        // 떠났다고 제3자에 대한 진술을 없앨 이유가 없습니다.
        String deviceId = UUID.randomUUID().toString();
        String me = createUser(deviceId);
        String other = createUser();
        report(me, other, "ABUSE", "남을까").andExpect(status().isCreated());

        // 삭제되고 나면 public_id 로는 찾을 수 없으므로 seq 를 먼저 잡아둡니다.
        int reportedSeq = seqOf(other);
        deleteAccount(me, deviceId);

        Map<String, Object> row = onlyReportAbout(reportedSeq);
        assertThat(row.get("reporter_seq")).isNull();
        assertThat(row.get("memo")).isEqualTo("남을까");
        assertThat(row.get("reason")).isEqualTo("ABUSE");
    }

    @Test
    @DisplayName("신고자가 탈퇴해도 CHECK 가 걸리지 않는다")
    void aNullReporterDoesNotTripTheSelfCheck() throws Exception {
        // ck_user_reports_not_self 가 reporter_seq <> reported_seq 인데, SET NULL 이
        // 되는 순간 그 비교가 NULL 이 됩니다. CHECK 는 거짓일 때만 막으므로 통과하는
        // 것이 맞지만, 이 성질에 기대고 있으니 확인해 둡니다. 여기서 막히면 탈퇴 자체가
        // 실패합니다.
        String deviceId = UUID.randomUUID().toString();
        String me = createUser(deviceId);
        String first = createUser();
        String second = createUser();
        report(me, first, "SPAM", null).andExpect(status().isCreated());
        report(me, second, "ABUSE", null).andExpect(status().isCreated());

        int firstSeq = seqOf(first);
        int secondSeq = seqOf(second);

        deleteAccount(me, deviceId);

        assertThat(onlyReportAbout(firstSeq).get("reporter_seq")).isNull();
        assertThat(onlyReportAbout(secondSeq).get("reporter_seq")).isNull();
    }

    @Test
    @DisplayName("신고당한 사람이 탈퇴하면 그 신고도 사라진다")
    void deletingTheTargetRemovesReportsAboutThem() throws Exception {
        String me = createUser();
        String deviceId = UUID.randomUUID().toString();
        String other = createUser(deviceId);
        report(me, other, "ABUSE", null).andExpect(status().isCreated());

        int reportedSeq = seqOf(other);
        deleteAccount(other, deviceId);

        assertThat(reportsAbout(reportedSeq)).isZero();
    }

    private ResultActions report(String caller, String target, String reason, String memo)
            throws Exception {
        String body = memo == null
                ? "{\"userId\":\"" + target + "\",\"reason\":\"" + reason + "\"}"
                : "{\"userId\":\"" + target + "\",\"reason\":\"" + reason
                        + "\",\"memo\":\"" + memo + "\"}";

        return mvc.perform(post("/api/v1/reports")
                .header(USER_ID_HEADER, caller)
                .contentType(MediaType.APPLICATION_JSON)
                .content(body));
    }

    private void deleteAccount(String userId, String deviceId) throws Exception {
        mvc.perform(delete("/api/v1/accounts/me")
                        .header(USER_ID_HEADER, userId)
                        .header(DEVICE_ID_HEADER, deviceId))
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

    private String nicknameOf(String userId) {
        return jdbcTemplate.queryForObject(
                "SELECT nickname FROM users WHERE public_id = ?", String.class, userId);
    }

    private int seqOf(String userId) {
        return jdbcTemplate.queryForObject(
                "SELECT users_seq FROM users WHERE public_id = ?", Integer.class, userId);
    }

    /**
     * 그 사람에 대한 신고 수.
     *
     * <p>전체를 세지 않는 이유는 이 클래스의 테스트들이 한 데이터베이스를 나눠 쓰기
     * 때문입니다. 전체를 세면 앞선 테스트가 남긴 행까지 세어, 통과 여부가 실행 순서에
     * 따라 달라집니다.
     */
    private int reportsAbout(int reportedSeq) {
        return jdbcTemplate.queryForObject(
                "SELECT COUNT(*) FROM user_reports WHERE reported_seq = ?", Integer.class, reportedSeq);
    }

    private Map<String, Object> onlyReportAbout(int reportedSeq) {
        return jdbcTemplate.queryForMap(
                "SELECT * FROM user_reports WHERE reported_seq = ?", reportedSeq);
    }
}
