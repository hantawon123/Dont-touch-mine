package com.ssafy.d205.api;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.test.web.servlet.MockMvc;
import tools.jackson.databind.ObjectMapper;

import java.util.Locale;
import java.util.Map;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.patch;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.ssafy.d205.support.IntegrationTest;

class UserSearchApiTest extends IntegrationTest {

    private static final String USER_ID_HEADER = "X-User-Id";

    @Autowired
    MockMvc mvc;

    @Autowired
    ObjectMapper objectMapper;

    @Autowired
    JdbcTemplate jdbcTemplate;

    @Test
    @DisplayName("닉네임 접두사로 다른 사용자를 찾는다")
    void findsByNicknamePrefix() throws Exception {
        String prefix = newPrefix();
        createUser(prefix + "AA");
        createUser(prefix + "BB");
        String me = createUser(newPrefix() + "ME");

        mvc.perform(searchRequest(me, prefix, null))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.users.length()").value(2));
    }

    @Test
    @DisplayName("검색은 대소문자를 무시한다")
    void searchIsCaseInsensitive() throws Exception {
        // V6이 만든 nickname_lower 컬럼이 없으면 이 테스트가 깨집니다. V4로 nickname이
        // 대소문자를 구분하게 됐으므로, 소문자 컬럼 없이는 대문자로 저장된 닉네임을
        // 소문자 검색어로 찾을 수 없습니다.
        String prefix = newPrefix();
        createUser(prefix + "AA");
        String me = createUser(newPrefix() + "ME");

        mvc.perform(searchRequest(me, prefix.toLowerCase(Locale.ROOT), null))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.users.length()").value(1));

        mvc.perform(searchRequest(me, prefix.toUpperCase(Locale.ROOT), null))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.users.length()").value(1));
    }

    @Test
    @DisplayName("접두사가 아닌 부분 일치는 찾지 못한다")
    void doesNotMatchInTheMiddle() throws Exception {
        // 의도한 동작입니다. LIKE '%query%' 는 인덱스를 타지 못하므로 접두사로
        // 정했습니다. 부분 일치로 바꾸기로 하면 이 테스트를 뒤집어야 합니다.
        String prefix = newPrefix();
        createUser(prefix + "AA");
        String me = createUser(newPrefix() + "ME");

        mvc.perform(searchRequest(me, prefix.substring(2), null))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.users.length()").value(0));
    }

    @Test
    @DisplayName("자기 자신은 결과에 담기지 않는다")
    void excludesCaller() throws Exception {
        String prefix = newPrefix();
        String me = createUser(prefix + "ME");
        createUser(prefix + "AA");

        mvc.perform(searchRequest(me, prefix, null))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.users.length()").value(1))
                .andExpect(jsonPath("$.users[0].userId").value(org.hamcrest.Matchers.not(me)));
    }

    @Test
    @DisplayName("내가 차단한 사람은 결과에 담기지 않는다")
    void excludesBlockedUser() throws Exception {
        String prefix = newPrefix();
        String me = createUser(prefix + "ME");
        String other = createUser(prefix + "AA");

        block(me, other);

        mvc.perform(searchRequest(me, prefix, null))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.users.length()").value(0));
    }

    @Test
    @DisplayName("나를 차단한 사람도 결과에 담기지 않는다")
    void excludesUserWhoBlockedMe() throws Exception {
        // 차단은 양방향으로 적용합니다. 한쪽만 걸러내면 차단당한 사람이 상대를
        // 계속 찾아낼 수 있어 차단의 의미가 없어집니다.
        String prefix = newPrefix();
        String me = createUser(prefix + "ME");
        String other = createUser(prefix + "AA");

        block(other, me);

        mvc.perform(searchRequest(me, prefix, null))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.users.length()").value(0));
    }

    @Test
    @DisplayName("결과가 없으면 빈 배열")
    void emptyResultIsEmptyArray() throws Exception {
        String me = createUser(newPrefix() + "ME");

        mvc.perform(searchRequest(me, newPrefix(), null))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.users").isArray())
                .andExpect(jsonPath("$.users.length()").value(0));
    }

    @Test
    @DisplayName("limit 이 결과 개수를 제한한다")
    void limitCapsResults() throws Exception {
        String prefix = newPrefix();
        createUser(prefix + "AA");
        createUser(prefix + "BB");
        createUser(prefix + "CC");
        String me = createUser(newPrefix() + "ME");

        mvc.perform(searchRequest(me, prefix, 2))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.users.length()").value(2));
    }

    @ParameterizedTest
    @DisplayName("규칙을 어긴 검색어는 400")
    @ValueSource(strings = {
            "가",
            "열세글자짜리닉네임입니다요",
            "검색어 공백",
            "nick-name",
            "%",
            "a%",
            "_b",
            "ㅋㅋ"
    })
    void rejectsInvalidQuery(String nickname) throws Exception {
        // % 와 _ 를 막는 것이 특히 중요합니다. 쿼리가 LIKE CONCAT(:prefix, '%')
        // 형태라 이 문자가 통과하면 와일드카드로 해석되어 전체 사용자가 걸립니다.
        String me = createUser(newPrefix() + "ME");

        mvc.perform(searchRequest(me, nickname, null))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("INVALID_REQUEST"));
    }

    @ParameterizedTest
    @DisplayName("범위를 벗어난 limit 은 400")
    @ValueSource(ints = {0, -1, 51})
    void rejectsInvalidLimit(int limit) throws Exception {
        String me = createUser(newPrefix() + "ME");

        mvc.perform(searchRequest(me, newPrefix(), limit))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("INVALID_REQUEST"));
    }

    @Test
    @DisplayName("X-User-Id 헤더가 없으면 400")
    void missingHeaderIsBadRequest() throws Exception {
        mvc.perform(get("/api/v1/users").param("nickname", newPrefix()))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("MISSING_HEADER"));
    }

    @Test
    @DisplayName("nickname 파라미터가 없으면 400")
    void missingQueryIsBadRequest() throws Exception {
        String me = createUser(newPrefix() + "ME");

        mvc.perform(get("/api/v1/users").header(USER_ID_HEADER, me))
                .andExpect(status().isBadRequest());
    }

    @Test
    @DisplayName("없는 계정으로 검색하면 404")
    void unknownCallerIsNotFound() throws Exception {
        // 빈 결과가 아니라 404 입니다. "찾는 사람이 없다"와 "당신이 누군지 모르겠다"는
        // 다른 상황이고, 클라이언트는 후자에서 발급을 다시 불러야 합니다.
        mvc.perform(searchRequest(UUID.randomUUID().toString(), newPrefix(), null))
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.code").value("ACCOUNT_NOT_FOUND"));
    }

    @Test
    @DisplayName("접두사 검색은 nickname_lower 인덱스를 쓸 수 있고 부분 일치는 쓸 수 없다")
    void prefixSearchCanUseIndex() {
        // V6이 존재하는 이유를 지키는 테스트입니다. 기능 테스트만으로는 누군가
        // LIKE '%q%' 로 바꿔도 전부 통과하고 성능만 조용히 죽습니다.
        //
        // 실제로 고른 인덱스(key)가 아니라 쓸 수 있는 후보(possible_keys)를 봅니다.
        // 테스트 DB는 행이 수십 개뿐이라 옵티마이저가 전체 스캔을 더 싸게 보고
        // 인덱스를 안 고를 수 있는데, 그건 쿼리 모양의 문제가 아닙니다.
        assertThat(possibleKeysFor("u.nickname_lower LIKE CONCAT('qa', '%')"))
                .contains("ix_users_nickname_lower");

        // 앞에 와일드카드가 붙으면 후보에서 사라집니다. 이게 접두사로 정한 이유입니다.
        assertThat(possibleKeysFor("u.nickname_lower LIKE CONCAT('%', 'qa', '%')"))
                .isNull();
    }

    private String possibleKeysFor(String whereClause) {
        Map<String, Object> plan = jdbcTemplate.queryForMap(
                "EXPLAIN SELECT u.public_id FROM users u WHERE " + whereClause);
        return (String) plan.get("possible_keys");
    }

    /**
     * 6글자 영숫자 접두사. 첫 글자를 대문자로 두어 대소문자 무시 검색을 시험할 수
     * 있게 합니다. 테스트마다 달라야 다른 테스트가 만든 계정이 섞이지 않습니다.
     */
    private static String newPrefix() {
        return "Q" + UUID.randomUUID().toString().replace("-", "").substring(0, 5);
    }

    /** 발급 후 닉네임을 지정한 값으로 바꿔 돌려줍니다. */
    private String createUser(String nickname) throws Exception {
        String body = mvc.perform(post("/api/v1/accounts")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"deviceId\":\"" + UUID.randomUUID() + "\"}"))
                .andExpect(status().isCreated())
                .andReturn().getResponse().getContentAsString();

        String userId = objectMapper.readTree(body).get("userId").asText();

        mvc.perform(patch("/api/v1/accounts/me")
                        .header(USER_ID_HEADER, userId)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"nickname\":\"" + nickname + "\"}"))
                .andExpect(status().isOk());

        return userId;
    }

    /**
     * 차단 API가 아직 없어서 직접 넣습니다. public_id 로 내부 seq 를 찾는 것까지
     * 한 문장으로 처리해 테스트가 seq 를 알 필요가 없게 했습니다.
     */
    private void block(String blockerUserId, String blockedUserId) {
        jdbcTemplate.update("""
                INSERT INTO user_blocks (blocker_seq, blocked_seq, created_at)
                SELECT b.users_seq, t.users_seq, '20260101000000'
                  FROM users b, users t
                 WHERE b.public_id = ? AND t.public_id = ?
                """, blockerUserId, blockedUserId);
    }

    private org.springframework.test.web.servlet.RequestBuilder searchRequest(
            String callerUserId, String nickname, Integer limit) {
        var request = get("/api/v1/users")
                .header(USER_ID_HEADER, callerUserId)
                .param("nickname", nickname);
        if (limit != null) {
            request = request.param("limit", String.valueOf(limit));
        }
        return request;
    }
}
