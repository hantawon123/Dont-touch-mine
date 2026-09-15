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
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
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
    @DisplayName("닉네임이 정확히 일치하면 찾는다")
    void findsByExactNickname() throws Exception {
        String name = newPrefix() + "AA";
        createUser(name);
        String me = createUser(newPrefix() + "ME");

        mvc.perform(searchRequest(me, name, null))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.users.length()").value(1))
                .andExpect(jsonPath("$.users[0].nickname").value(name));
    }

    @Test
    @DisplayName("앞글자만으로는 찾지 못한다")
    void aPrefixNoLongerFinds() throws Exception {
        // 예전에는 이것이 되는 것이 기능이었습니다. 몇 글자만 쳐도 모르는 사람이 걸려
        // 나오는 것을 막으려고 바꿨습니다.
        String prefix = newPrefix();
        createUser(prefix + "AA");
        String me = createUser(newPrefix() + "ME");

        mvc.perform(searchRequest(me, prefix, null))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.users.length()").value(0));
    }

    @Test
    @DisplayName("대소문자가 다르면 찾지 못한다")
    void searchIsCaseSensitive() throws Exception {
        // nickname 컬럼이 as_cs 라 player 와 Player 는 서로 다른 사람입니다. 검색이
        // 대소문자를 무시하면 한 번의 검색이 서로 다른 두 사람을 함께 내놓습니다.
        //
        // 끝을 "aA" 로 두는 것이 이 테스트의 전제입니다. 앞이 "Q" + 16진수라 그 다섯
        // 자리가 모두 숫자로 나오면(약 10%) 대문자로 바꿔도 원본과 같아집니다. 그러면
        // 같은 이름으로 검색하는 셈이라 당연히 걸리고, 열 번에 한 번 깨집니다.
        // 소문자 하나와 대문자 하나를 함께 두면 어느 쪽으로 바꿔도 반드시 달라집니다.
        String name = newPrefix() + "aA";
        createUser(name);
        String me = createUser(newPrefix() + "ME");

        // 전제를 테스트가 스스로 지킵니다. newPrefix 가 바뀌어 다시 같아지면, 조용히
        // 통과하거나 가끔 깨지는 대신 여기서 곧바로 실패합니다.
        assertThat(name.toLowerCase(Locale.ROOT)).isNotEqualTo(name);
        assertThat(name.toUpperCase(Locale.ROOT)).isNotEqualTo(name);

        mvc.perform(searchRequest(me, name.toLowerCase(Locale.ROOT), null))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.users.length()").value(0));

        mvc.perform(searchRequest(me, name.toUpperCase(Locale.ROOT), null))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.users.length()").value(0));
    }

    @Test
    @DisplayName("가운데 일치도 찾지 못한다")
    void doesNotMatchInTheMiddle() throws Exception {
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
        // 자기 닉네임을 정확히 쳐도 자기는 안 나옵니다.
        String myName = newPrefix() + "ME";
        String me = createUser(myName);

        mvc.perform(searchRequest(me, myName, null))
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
    @DisplayName("limit 은 결과에 영향을 주지 않는다")
    void limitNoLongerMatters() throws Exception {
        // 유니크 제약 때문에 결과가 많아야 한 건입니다. 파라미터는 계약을 깨지 않으려고
        // 남겨 두었을 뿐이고, 값이 무엇이든 답이 같아야 합니다.
        String name = newPrefix() + "AA";
        createUser(name);
        String me = createUser(newPrefix() + "ME");

        mvc.perform(searchRequest(me, name, 1))
                .andExpect(jsonPath("$.users.length()").value(1));
        mvc.perform(searchRequest(me, name, 50))
                .andExpect(jsonPath("$.users.length()").value(1));
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
    @DisplayName("정확히 일치 검색은 유니크 인덱스를 쓸 수 있다")
    void exactSearchCanUseTheUniqueIndex() throws Exception {
        // 기능 테스트만으로는 누군가 LIKE 로 되돌려도 전부 통과하고 성능만 조용히
        // 죽습니다. 쿼리 모양 자체를 지킵니다.
        //
        // 실제로 고른 인덱스(key)가 아니라 쓸 수 있는 후보(possible_keys)를 봅니다.
        // 테스트 DB 는 행이 수십 개뿐이라 옵티마이저가 전체 스캔을 더 싸게 볼 수
        // 있는데, 그건 쿼리 모양의 문제가 아닙니다.
        // 실제로 있는 닉네임이어야 합니다. 없는 값으로 물으면 MySQL 이 유니크 조회를
        // 최적화 단계에서 끝내고 "no matching row in const table" 로 계획 자체를
        // 세우지 않아, 인덱스를 못 쓰는 것과 구분되지 않습니다.
        String name = newPrefix() + "IX";
        createUser(name);

        assertThat(possibleKeysFor("u.nickname = '" + name + "'"))
                .contains("uk_users_nickname");

        // 앞에 와일드카드가 붙으면 후보에서 사라집니다. 되돌리면 안 되는 이유입니다.
        assertThat(possibleKeysFor("u.nickname LIKE CONCAT('%', '" + name + "', '%')"))
                .isNull();
    }

    private String possibleKeysFor(String whereClause) {
        Map<String, Object> plan = jdbcTemplate.queryForMap(
                "EXPLAIN SELECT u.public_id FROM users u WHERE " + whereClause);
        return (String) plan.get("possible_keys");
    }

    @Test
    @DisplayName("검색을 끈 사람은 결과에 담기지 않는다")
    void hiddenUsersAreLeftOut() throws Exception {
        String hiddenName = newPrefix() + "AA";
        String hidden = createUser(hiddenName);
        String me = createUser(newPrefix() + "ME");

        // 끄기 전에는 정확히 치면 나옵니다.
        mvc.perform(searchRequest(me, hiddenName, null))
                .andExpect(jsonPath("$.users.length()").value(1));

        setSearchable(hidden, false);

        // 화면에서 가리는 것이 아니라 응답 자체에 없어야 합니다. 담아 보내고 클라이언트가
        // 거르는 방식이면 네트워크를 보는 것만으로 드러납니다.
        mvc.perform(searchRequest(me, hiddenName, null))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.users.length()").value(0));
    }

    @Test
    @DisplayName("다시 켜면 다시 나온다")
    void turningItBackOnRestoresThem() throws Exception {
        String name = newPrefix() + "AA";
        String user = createUser(name);
        String me = createUser(newPrefix() + "ME");

        setSearchable(user, false);
        setSearchable(user, true);

        mvc.perform(searchRequest(me, name, null))
                .andExpect(jsonPath("$.users.length()").value(1));
    }

    @Test
    @DisplayName("컬럼 기본값이 TRUE 다 - 이미 가입한 사람을 위한 값")
    void theColumnDefaultKeepsExistingUsersVisible() {
        // V10 이 도는 순간 기존 사용자 전원이 이 값을 받습니다. FALSE 였으면 그 한 문장으로
        // 모두가 검색에서 사라지고 친구 추가가 통째로 멈춥니다.
        //
        // 새 계정으로는 이것을 확인할 수 없습니다. User 엔티티가 필드를 true 로 초기화해서
        // JPA 의 INSERT 에 값이 항상 실려 가므로, 컬럼 기본값을 FALSE 로 바꿔도 아래
        // 테스트들은 전부 통과합니다. 그래서 스키마를 직접 봅니다.
        //
        // 테스트 컨테이너는 빈 DB 로 시작해 "마이그레이션 전에 있던 행"을 만들 수 없습니다.
        // 확인할 수 있는 것은 그 행들이 받을 값이고, 그것이 이 값입니다.
        String columnDefault = jdbcTemplate.queryForObject("""
                SELECT COLUMN_DEFAULT
                  FROM information_schema.COLUMNS
                 WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'users'
                   AND COLUMN_NAME = 'searchable'
                """, String.class);

        assertThat(columnDefault).isEqualTo("1");
    }

    @Test
    @DisplayName("발급된 계정은 검색에 나온다")
    void newAccountsAreSearchable() throws Exception {
        // 위와 달리 이쪽이 보는 것은 엔티티의 초기값입니다. 둘 중 하나만 있으면 새
        // 계정과 기존 계정 가운데 한쪽이 검증되지 않은 채로 남습니다.
        String name = newPrefix() + "AA";
        createUser(name);
        String me = createUser(newPrefix() + "ME");

        mvc.perform(searchRequest(me, name, null))
                .andExpect(jsonPath("$.users.length()").value(1));
    }

    @Test
    @DisplayName("같은 값을 다시 보내도 성공한다")
    void settingTheSameValueTwiceIsFine() throws Exception {
        // 체크박스를 두 번 눌러 원래대로 돌아온 경우가 오류일 이유가 없습니다.
        String me = createUser(newPrefix() + "ME");

        setSearchable(me, false);
        setSearchable(me, false);
    }

    @Test
    @DisplayName("값을 빠뜨리면 400 이다")
    void theValueIsRequired() throws Exception {
        // Boolean 이 아니라 boolean 으로 두면 빠뜨린 요청에 false 가 채워져, 끄겠다는
        // 뜻이 아닌데 검색에서 사라집니다.
        String me = createUser(newPrefix() + "ME");

        mvc.perform(put("/api/v1/accounts/me/searchable")
                        .header(USER_ID_HEADER, me)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{}"))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("INVALID_REQUEST"));
    }

    @Test
    @DisplayName("검색을 꺼도 계정 조회에는 현재 값이 나온다")
    void theAccountShowsTheCurrentValue() throws Exception {
        // 화면이 체크박스를 어떤 상태로 그릴지 아는 유일한 경로입니다. 없으면 껐다가
        // 다시 들어왔을 때 켜진 것처럼 보입니다.
        String me = createUser(newPrefix() + "ME");

        mvc.perform(get("/api/v1/accounts/me").header(USER_ID_HEADER, me))
                .andExpect(jsonPath("$.searchable").value(true));

        setSearchable(me, false);

        mvc.perform(get("/api/v1/accounts/me").header(USER_ID_HEADER, me))
                .andExpect(jsonPath("$.searchable").value(false));
    }

    @Test
    @DisplayName("검색을 꺼도 id 로 보낸 친구 요청은 도착한다")
    void hiddenUsersStillReceiveFriendRequests() throws Exception {
        // 검색에서 빼는 것과 연락을 끊는 것은 다릅니다. 이미 id 를 아는 사람 - 같은 방에
        // 있었거나 이미 요청을 주고받은 사람 - 과의 경로까지 막으면 "검색 허용"이라는
        // 이름이 약속한 것보다 큰 일을 하게 됩니다.
        String hidden = createUser(newPrefix() + "AA");
        String me = createUser(newPrefix() + "ME");
        setSearchable(hidden, false);

        mvc.perform(post("/api/v1/friend-requests")
                        .header(USER_ID_HEADER, me)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"userId\":\"" + hidden + "\"}"))
                .andExpect(status().isCreated());

        mvc.perform(get("/api/v1/friend-requests")
                        .header(USER_ID_HEADER, hidden)
                        .param("direction", "incoming"))
                .andExpect(jsonPath("$.requests.length()").value(1));

        // 그리고 친구가 된 뒤에도 목록에 그대로 보여야 합니다.
        //
        // 검색 필터는 한 줄짜리라 나중에 누가 findFriends 에도 같은 조건을 붙이기 쉽습니다.
        // 그러면 검색을 끈 친구가 목록에서 사라져 친구가 없어진 것처럼 보입니다.
        // 검색에서 빼는 것과 이름을 숨기는 것은 다릅니다.
        mvc.perform(post("/api/v1/friend-requests/{userId}/accept", me)
                        .header(USER_ID_HEADER, hidden))
                .andExpect(status().isNoContent());

        mvc.perform(get("/api/v1/friends").header(USER_ID_HEADER, me))
                .andExpect(jsonPath("$.friends.length()").value(1));
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


    /** 검색 노출을 켜거나 끕니다. */
    private void setSearchable(String userId, boolean searchable) throws Exception {
        mvc.perform(put("/api/v1/accounts/me/searchable")
                        .header(USER_ID_HEADER, userId)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"searchable\":" + searchable + "}"))
                .andExpect(status().isOk());
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
