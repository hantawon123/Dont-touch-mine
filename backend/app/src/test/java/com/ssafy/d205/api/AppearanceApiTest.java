package com.ssafy.d205.api;

import tools.jackson.databind.ObjectMapper;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.jdbc.core.JdbcTemplate;
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
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.ssafy.d205.domain.user.dto.IssueAccountRequest;
import com.ssafy.d205.domain.user.dto.UpdateAppearanceRequest;
import com.ssafy.d205.domain.user.entity.AppearancePolicy;
import com.ssafy.d205.support.IntegrationTest;

class AppearanceApiTest extends IntegrationTest {

    private static final String USER_ID_HEADER = "X-User-Id";
    private static final String ME = "/api/v1/accounts/me";
    private static final String APPEARANCE = ME + "/appearance";

    private static final UpdateAppearanceRequest OUTFIT =
            new UpdateAppearanceRequest("body_black", "hood_bear_purple", "shoes_pink", "face_smile");

    @Autowired
    MockMvc mvc;

    @Autowired
    ObjectMapper objectMapper;

    @Autowired
    JdbcTemplate jdbcTemplate;

    @Test
    @DisplayName("새 계정은 외형이 없다")
    void newAccountHasNoAppearance() throws Exception {
        String userId = issueAndGetUserId();

        mvc.perform(get(ME).header(USER_ID_HEADER, userId))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.appearanceSet").value(false))
                .andExpect(jsonPath("$.appearance").value((Object) null));
    }

    @Test
    @DisplayName("외형을 저장하면 바뀐 계정이 돌아오고 조회에도 남는다")
    void savesAppearance() throws Exception {
        String userId = issueAndGetUserId();

        mvc.perform(setRequest(userId, OUTFIT))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.userId").value(userId))
                .andExpect(jsonPath("$.appearanceSet").value(true))
                .andExpect(jsonPath("$.appearance.bodyColor").value("body_black"))
                .andExpect(jsonPath("$.appearance.hood").value("hood_bear_purple"))
                .andExpect(jsonPath("$.appearance.shoes").value("shoes_pink"))
                .andExpect(jsonPath("$.appearance.face").value("face_smile"));

        mvc.perform(get(ME).header(USER_ID_HEADER, userId))
                .andExpect(jsonPath("$.appearanceSet").value(true))
                .andExpect(jsonPath("$.appearance.hood").value("hood_bear_purple"));
    }

    @Test
    @DisplayName("다시 저장하면 덮어쓴다. 행이 늘지 않는다")
    void secondSaveOverwrites() throws Exception {
        String userId = issueAndGetUserId();
        mvc.perform(setRequest(userId, OUTFIT)).andExpect(status().isOk());

        UpdateAppearanceRequest changed =
                new UpdateAppearanceRequest("body_white", OUTFIT.hood(), OUTFIT.shoes(), "face_wink");

        mvc.perform(setRequest(userId, changed))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.appearance.bodyColor").value("body_white"))
                .andExpect(jsonPath("$.appearance.face").value("face_wink"))
                .andExpect(jsonPath("$.appearance.hood").value(OUTFIT.hood()));

        assertThat(rowsFor(userId)).isOne();
    }

    @Test
    @DisplayName("같은 값을 다시 저장해도 성공한다")
    void saveIsIdempotent() throws Exception {
        String userId = issueAndGetUserId();

        mvc.perform(setRequest(userId, OUTFIT)).andExpect(status().isOk());
        mvc.perform(setRequest(userId, OUTFIT))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.appearance.bodyColor").value("body_black"));
    }

    @Test
    @DisplayName("다시 발급을 불러도 저장한 외형이 함께 온다")
    void reissueCarriesAppearance() throws Exception {
        // 앱을 다시 켰을 때의 경로입니다. 클라이언트는 발급 응답 하나로 외형까지 알아야
        // 하고, 따로 조회하지 않습니다.
        String deviceId = newDeviceId();
        String userId = userIdOf(mvc.perform(issueRequest(deviceId))
                .andExpect(status().isCreated())
                .andReturn().getResponse().getContentAsString());

        mvc.perform(setRequest(userId, OUTFIT)).andExpect(status().isOk());

        mvc.perform(issueRequest(deviceId))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.appearanceSet").value(true))
                .andExpect(jsonPath("$.appearance.shoes").value("shoes_pink"));
    }

    @Test
    @DisplayName("초기화하면 외형이 없는 상태로 돌아간다")
    void clearRemovesAppearance() throws Exception {
        String userId = issueAndGetUserId();
        mvc.perform(setRequest(userId, OUTFIT)).andExpect(status().isOk());

        mvc.perform(delete(APPEARANCE).header(USER_ID_HEADER, userId))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.appearanceSet").value(false))
                .andExpect(jsonPath("$.appearance").value((Object) null));

        mvc.perform(get(ME).header(USER_ID_HEADER, userId))
                .andExpect(jsonPath("$.appearanceSet").value(false));
        assertThat(rowsFor(userId)).isZero();
    }

    @Test
    @DisplayName("저장한 적이 없어도 초기화는 성공한다")
    void clearIsIdempotent() throws Exception {
        String userId = issueAndGetUserId();

        mvc.perform(delete(APPEARANCE).header(USER_ID_HEADER, userId))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.appearanceSet").value(false));
    }

    @Test
    @DisplayName("파츠 하나가 빠지면 400")
    void rejectsMissingPart() throws Exception {
        String userId = issueAndGetUserId();

        mvc.perform(put(APPEARANCE)
                        .header(USER_ID_HEADER, userId)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"bodyColor\":\"body_black\",\"hood\":\"hood_a\",\"shoes\":\"shoes_a\"}"))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("INVALID_REQUEST"));
    }

    @ParameterizedTest
    @DisplayName("형식에 맞지 않는 파츠 id 는 400")
    @ValueSource(strings = {
            "",
            " ",
            "has space",
            "한글",
            "thirty_three_characters_long_id_x"
    })
    void rejectsMalformedPartId(String partId) throws Exception {
        String userId = issueAndGetUserId();

        mvc.perform(setRequest(userId, new UpdateAppearanceRequest(partId, "hood_a", "shoes_a", "face_a")))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.code").value("INVALID_REQUEST"));
    }

    @Test
    @DisplayName("색상값 같은 기호와 32자 경계는 통과한다")
    void acceptsSymbolsAndBoundaryLength() throws Exception {
        // 몸 색상이 팔레트 id 가 아니라 색상값일 수 있습니다. 소문자와 밑줄만 허용했다면
        // 여기서 막혔을 것입니다.
        String longest = "x".repeat(AppearancePolicy.MAX_LENGTH);
        String userId = issueAndGetUserId();

        mvc.perform(setRequest(userId, new UpdateAppearanceRequest("#A1B2C3", "Hood-Bear.v2", longest, "face_a")))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.appearance.bodyColor").value("#A1B2C3"))
                .andExpect(jsonPath("$.appearance.shoes").value(longest));
    }

    @Test
    @DisplayName("서버는 파츠 목록을 모른다. 처음 보는 id 도 그대로 저장한다")
    void doesNotValidateAgainstCatalog() throws Exception {
        String userId = issueAndGetUserId();

        mvc.perform(setRequest(userId, new UpdateAppearanceRequest("never_seen_1", "never_seen_2", "never_seen_3", "never_seen_4")))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.appearance.bodyColor").value("never_seen_1"));
    }

    @Test
    @DisplayName("없는 계정이면 404")
    void unknownCallerIsNotFound() throws Exception {
        mvc.perform(setRequest(UUID.randomUUID().toString(), OUTFIT))
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.code").value("ACCOUNT_NOT_FOUND"));
    }

    @Test
    @DisplayName("같은 계정이 동시에 두 번 저장해도 둘 다 성공하고 행은 하나다")
    void concurrentSavesLeaveOneRow() throws Exception {
        // "조회해서 없으면 넣기"로 짰다면 한쪽이 PK 위반으로 409 가 됩니다. 한 문장 upsert 라
        // 그런 경합이 없어야 합니다.
        String userId = issueAndGetUserId();
        CountDownLatch ready = new CountDownLatch(2);
        CountDownLatch go = new CountDownLatch(1);
        ExecutorService pool = Executors.newFixedThreadPool(2);

        try {
            Future<Integer> a = pool.submit(() -> raceToSave(userId, OUTFIT, ready, go));
            Future<Integer> b = pool.submit(() -> raceToSave(userId,
                    new UpdateAppearanceRequest("body_white", "hood_b", "shoes_b", "face_b"), ready, go));

            ready.await(10, TimeUnit.SECONDS);
            go.countDown();

            List<Integer> statuses = List.of(a.get(20, TimeUnit.SECONDS), b.get(20, TimeUnit.SECONDS));
            assertThat(statuses).containsExactly(200, 200);
        } finally {
            pool.shutdownNow();
        }

        assertThat(rowsFor(userId)).isOne();
    }

    private int raceToSave(String userId, UpdateAppearanceRequest request,
                           CountDownLatch ready, CountDownLatch go) throws Exception {
        ready.countDown();
        go.await(10, TimeUnit.SECONDS);
        return mvc.perform(setRequest(userId, request)).andReturn().getResponse().getStatus();
    }

    private int rowsFor(String userId) {
        return jdbcTemplate.queryForObject("""
                SELECT COUNT(*) FROM user_appearances a
                  JOIN users u ON u.users_seq = a.user_seq
                 WHERE u.public_id = ?
                """, Integer.class, userId);
    }

    private RequestBuilder setRequest(String userId, UpdateAppearanceRequest request) throws Exception {
        return put(APPEARANCE)
                .header(USER_ID_HEADER, userId)
                .contentType(MediaType.APPLICATION_JSON)
                .content(objectMapper.writeValueAsString(request));
    }

    private static String newDeviceId() {
        return UUID.randomUUID().toString();
    }

    private RequestBuilder issueRequest(String deviceId) throws Exception {
        return post("/api/v1/accounts")
                .contentType(MediaType.APPLICATION_JSON)
                .content(objectMapper.writeValueAsString(new IssueAccountRequest(deviceId)));
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
