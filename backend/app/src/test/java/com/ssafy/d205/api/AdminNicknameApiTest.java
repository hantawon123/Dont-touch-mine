package com.ssafy.d205.api;

import jakarta.servlet.http.Cookie;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.mock.web.MockHttpSession;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.MvcResult;
import org.springframework.test.web.servlet.ResultActions;
import tools.jackson.databind.JsonNode;
import tools.jackson.databind.ObjectMapper;

import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.patch;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.ssafy.d205.support.IntegrationTest;

/**
 * 운영자가 남의 닉네임을 바꾸는 두 가지 (S15P21D205-1047).
 *
 * <p>이름을 직접 지정하는 것과, 부적절한 닉네임을 "부적절한닉네임123" 꼴로 치우는 것입니다.
 *
 * <p>여기서 고정하려는 것 중 가장 중요한 것은 <b>닉네임 변경권</b>입니다. 이 게임에서 닉네임
 * 변경은 한 번뿐이라 클라이언트는 {@code nicknameSet} 이 true 인 사람의 입력 칸을 잠급니다.
 * 부적절한 닉네임을 치울 때 그 값을 비우지 않으면 그 사람은 치워진 이름을 평생 달게 되고,
 * 반대로 직접 지정에서 비워 버리면 운영자가 정해 준 이름을 바로 다시 바꿀 수 있습니다.
 */
class AdminNicknameApiTest extends IntegrationTest {

    private static final String USER_ID_HEADER = "X-User-Id";
    private static final String LOGIN = "/api/v1/admin/session";
    private static final String USERS = "/api/v1/admin/users";
    private static final String ACCOUNTS = "/api/v1/accounts";

    /** SecurityConfig 가 테스트 프로필에서 쓰는 계정 이름입니다. 감사 행의 처리자가 됩니다. */
    private static final String ADMIN = "test-admin";

    /** 서버가 붙이는 이름의 꼴입니다. 접두사 일곱 자에 숫자 세~다섯 자리. */
    private static final String FORCED = "^부적절한닉네임[0-9]{3,5}$";

    @Autowired
    MockMvc mvc;

    @Autowired
    ObjectMapper objectMapper;

    @Test
    @DisplayName("로그인 없이는 이름을 바꿀 수 없다")
    void needsAnAdminSession() throws Exception {
        String user = createUser();

        // 403 입니다. CsrfFilter 가 인가보다 먼저 돌기 때문입니다 - 세션도 토큰도 없는 요청은
        // 401 까지 가지 못합니다. 관리 화면의 call() 이 403 을 만나면 세션을 한 번 물어보는
        // 이유가 이것입니다.
        mvc.perform(put(USERS + "/{userId}/nickname", user)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"nickname\":\"멀쩡한이름\",\"reason\":\"권한 확인\"}"))
                .andExpect(status().isForbidden());

        mvc.perform(post(USERS + "/{userId}/nickname/reset", user))
                .andExpect(status().isForbidden());
    }

    @Test
    @DisplayName("운영자가 지정한 이름으로 바뀌고 이력에 옛 이름이 남는다")
    void renamesAndKeepsTheOldNameInTheAudit() throws Exception {
        Admin admin = login();
        String user = createUser();
        String before = nicknameOf(admin, user);

        rename(admin, user, "새로운이름", "신고 확인 결과")
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.changed").value(true))
                .andExpect(jsonPath("$.nickname").value("새로운이름"));

        assertThat(nicknameOf(admin, user)).isEqualTo("새로운이름");

        JsonNode entry = renamesOf(admin, user).get(0);
        assertThat(entry.get("beforeNickname").asText()).isEqualTo(before);
        assertThat(entry.get("afterNickname").asText()).isEqualTo("새로운이름");
        assertThat(entry.get("reason").asText()).isEqualTo("신고 확인 결과");
        assertThat(entry.get("adminUsername").asText()).isEqualTo(ADMIN);
        assertThat(entry.get("actedAt").asText()).hasSize(14);
        assertThat(entry.get("accountDeleted").asBoolean()).isFalse();
    }

    @Test
    @DisplayName("지금과 같은 이름을 보내면 바뀌지 않고 이력도 남지 않는다")
    void sameNameIsNotAnAction() throws Exception {
        Admin admin = login();
        String user = createUser();
        String now = nicknameOf(admin, user);

        rename(admin, user, now, "그대로 두기")
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.changed").value(false));

        // 바뀐 것이 없으면 감사 행도 없습니다. 정지가 같은 요청에도 행을 남기는 것과 다릅니다 -
        // 그쪽은 사유를 고쳐 적는 것이 운영 행위인데 여기서는 아무 일도 일어나지 않았습니다.
        assertThat(renamesOf(admin, user)).isEmpty();
    }

    @Test
    @DisplayName("글자 규칙을 어기면 400 이다")
    void rejectsNamesThePolicyForbids() throws Exception {
        Admin admin = login();
        String user = createUser();

        rename(admin, user, "긴", "한 글자")
                .andExpect(status().isBadRequest());
        rename(admin, user, "띄 어 쓰기", "공백")
                .andExpect(status().isBadRequest());
        rename(admin, user, "가나다라마바사아자차카타파", "열세 자")
                .andExpect(status().isBadRequest());
    }

    @Test
    @DisplayName("금칙어는 운영자도 붙일 수 없다")
    void appliesTheBlocklistToAdminsToo() throws Exception {
        Admin admin = login();
        String user = createUser();

        // 운영자에게만 예외를 두면, 그 이름을 받은 사람이 자기 이름을 고칠 때 거절당합니다.
        rename(admin, user, "시발이", "금칙어 확인")
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("NICKNAME_FORBIDDEN"));
    }

    @Test
    @DisplayName("남이 쓰는 이름은 409 다")
    void rejectsNamesAlreadyTaken() throws Exception {
        Admin admin = login();
        String mine = createUser();
        String other = createUser();

        rename(admin, mine, nicknameOf(admin, other), "중복 확인")
                .andExpect(status().isConflict())
                .andExpect(jsonPath("$.code").value("NICKNAME_TAKEN"));
    }

    @Test
    @DisplayName("사유 없이는 바꿀 수 없다")
    void requiresAReason() throws Exception {
        Admin admin = login();
        String user = createUser();

        rename(admin, user, "멀쩡한이름", "")
                .andExpect(status().isBadRequest());
    }

    @Test
    @DisplayName("없는 계정은 404 다")
    void unknownAccountIsNotFound() throws Exception {
        Admin admin = login();
        String nobody = UUID.randomUUID().toString();

        rename(admin, nobody, "멀쩡한이름", "없는 계정")
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.code").value("TARGET_NOT_FOUND"));

        reset(admin, nobody).andExpect(status().isNotFound());
    }

    @Test
    @DisplayName("부적절 처리는 부적절한닉네임NNN 을 붙이고 치운 이름을 기록한다")
    void forcedRenameReplacesTheNameWithAGeneratedOne() throws Exception {
        Admin admin = login();
        String user = createUser();
        String before = nicknameOf(admin, user);

        String assigned = assignedNameOf(reset(admin, user)
                .andExpect(jsonPath("$.changed").value(true)));

        assertThat(assigned).matches(FORCED);
        assertThat(nicknameOf(admin, user)).isEqualTo(assigned);

        JsonNode entry = renamesOf(admin, user).get(0);
        assertThat(entry.get("beforeNickname").asText()).isEqualTo(before);
        assertThat(entry.get("afterNickname").asText()).isEqualTo(assigned);
        // 사유를 운영자에게 묻지 않습니다. 치운 이름이 위에 남아 있어 근거는 기록 자체에 있습니다.
        assertThat(entry.get("reason").asText()).isEqualTo("부적절한 닉네임");
    }

    @Test
    @DisplayName("부적절 처리를 두 번 하면 다른 번호가 붙는다")
    void forcedRenameDoesNotCollideWithItself() throws Exception {
        Admin admin = login();
        String first = createUser();
        String second = createUser();

        String one = assignedNameOf(reset(admin, first));
        String two = assignedNameOf(reset(admin, second));

        // 같은 번호가 나오면 uk_users_nickname 에 걸려 두 번째가 409 가 됩니다. 그러지 않는
        // 것이 forcedNickname() 의 재시도입니다.
        assertThat(one).isNotEqualTo(two);
        assertThat(two).matches(FORCED);
    }

    @Test
    @DisplayName("부적절 처리는 본인에게 닉네임 변경권을 돌려준다")
    void forcedRenameGivesTheChangeBack() throws Exception {
        Admin admin = login();
        String user = createUser();

        // 스스로 이름을 정하면 변경권을 씁니다. 이 뒤로 클라이언트의 입력 칸은 잠깁니다.
        selfRename(user, "내가정한이름").andExpect(status().isOk());
        assertThat(nicknameSetOf(user)).isTrue();

        reset(admin, user).andExpect(status().isOk());

        assertThat(nicknameSetOf(user)).isFalse();
        // 그래서 본인이 멀쩡한 이름으로 한 번 다시 정할 수 있습니다.
        selfRename(user, "다시정한이름").andExpect(status().isOk());
    }

    @Test
    @DisplayName("직접 지정은 변경권을 건드리지 않는다")
    void directRenameLeavesTheChangeRightAsItWas() throws Exception {
        Admin admin = login();
        String settled = createUser();
        String untouched = createUser();

        selfRename(settled, "스스로정함").andExpect(status().isOk());
        rename(admin, settled, "운영자가정함", "직접 지정").andExpect(status().isOk());

        // 운영자가 정해 준 이름을 바로 또 바꿀 수 있으면 지정한 뜻이 사라집니다.
        assertThat(nicknameSetOf(settled)).isTrue();

        // 반대로 아직 정하지 않은 사람에게 이름을 지정해도 변경권은 그대로 남습니다. 서버가
        // 지어 준 임시 닉네임을 쓰던 사람이 자기 이름을 한 번도 못 정한 채 잠기면 안 됩니다.
        rename(admin, untouched, "운영자가붙임", "직접 지정").andExpect(status().isOk());
        assertThat(nicknameSetOf(untouched)).isFalse();
    }

    private ResultActions rename(Admin admin, String userId, String nickname, String reason) throws Exception {
        return mvc.perform(put(USERS + "/{userId}/nickname", userId)
                .session(admin.session())
                .cookie(admin.csrf())
                .header("X-XSRF-TOKEN", admin.csrf().getValue())
                .contentType(MediaType.APPLICATION_JSON)
                .content(objectMapper.writeValueAsString(new Rename(nickname, reason))));
    }

    private ResultActions reset(Admin admin, String userId) throws Exception {
        return mvc.perform(post(USERS + "/{userId}/nickname/reset", userId)
                .session(admin.session())
                .cookie(admin.csrf())
                .header("X-XSRF-TOKEN", admin.csrf().getValue()));
    }

    /** 본인이 스스로 바꾸는 개명입니다. 그쪽 요청은 사유를 받지 않으므로 닉네임만 보냅니다. */
    private ResultActions selfRename(String userId, String nickname) throws Exception {
        return mvc.perform(patch(ACCOUNTS + "/me")
                .header(USER_ID_HEADER, userId)
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"nickname\":\"" + nickname + "\"}"));
    }

    private String assignedNameOf(ResultActions result) throws Exception {
        return objectMapper.readTree(result.andExpect(status().isOk())
                        .andReturn().getResponse().getContentAsString())
                .get("nickname").asText();
    }

    /** 운영자가 보는 지금 이름입니다. */
    private String nicknameOf(Admin admin, String userId) throws Exception {
        return detailOf(admin, userId).get("user").get("nickname").asText();
    }

    private JsonNode renamesOf(Admin admin, String userId) throws Exception {
        return detailOf(admin, userId).get("renames");
    }

    private JsonNode detailOf(Admin admin, String userId) throws Exception {
        String body = mvc.perform(get(USERS + "/{userId}", userId).session(admin.session()))
                .andExpect(status().isOk())
                .andReturn().getResponse().getContentAsString();
        return objectMapper.readTree(body);
    }

    /** 클라이언트가 닉네임 입력 칸을 잠글지 정하는 값입니다. */
    private boolean nicknameSetOf(String userId) throws Exception {
        String body = mvc.perform(get(ACCOUNTS + "/me").header(USER_ID_HEADER, userId))
                .andExpect(status().isOk())
                .andReturn().getResponse().getContentAsString();
        return objectMapper.readTree(body).get("nicknameSet").asBoolean();
    }

    private Admin login() throws Exception {
        MockHttpSession session = new MockHttpSession();

        MvcResult result = mvc.perform(post(LOGIN)
                        .session(session)
                        .param("username", ADMIN)
                        .param("password", "test-password"))
                .andExpect(status().isNoContent())
                .andReturn();

        Cookie token = result.getResponse().getCookie("XSRF-TOKEN");
        assertThat(token).as("로그인 응답에 CSRF 토큰 쿠키가 없습니다").isNotNull();
        return new Admin(session, token);
    }

    private String createUser() throws Exception {
        String body = mvc.perform(post(ACCOUNTS)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"deviceId\":\"" + UUID.randomUUID() + "\"}"))
                .andExpect(status().isCreated())
                .andReturn().getResponse().getContentAsString();
        return objectMapper.readTree(body).get("userId").asText();
    }

    private record Admin(MockHttpSession session, Cookie csrf) {
    }

    /** 운영자 개명의 요청 본문. */
    private record Rename(String nickname, String reason) {
    }
}
