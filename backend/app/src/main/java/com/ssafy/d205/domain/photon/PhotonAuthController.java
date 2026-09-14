package com.ssafy.d205.domain.photon;

import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import java.security.MessageDigest;
import java.nio.charset.StandardCharsets;
import java.util.Optional;

import com.ssafy.d205.domain.user.entity.User;
import com.ssafy.d205.global.security.AccountTokens;
import com.ssafy.d205.domain.user.repository.UserRepository;

/**
 * Photon 커스텀 인증 (S15P21D205-925).
 *
 * <p>Photon 서버가 클라이언트 접속 시점에 이 주소를 부릅니다. 정지된 계정이면 거절하고,
 * 그러면 Photon 이 접속 자체를 막습니다. 백엔드 API 만 막는 {@code SuspensionInterceptor}
 * 와 달리 <b>게임을 막는 것은 여기뿐입니다</b> - 방 만들기와 참가와 플레이는 전부 Photon
 * 이고 우리 백엔드에는 방 API 가 없습니다.
 *
 * <p><b>클라이언트가 아니라 Photon 이 부릅니다.</b> 그래서 우리 세션도 X-User-Id 도 쓸 수
 * 없습니다. 대신 두 가지로 지킵니다.
 *
 * <ul>
 *   <li>공유 비밀({@code key}) - Photon 대시보드에 등록해 두는 값입니다. 클라이언트에는
 *       보이지 않으므로, 이 값이 맞으면 Photon 을 거쳐 온 요청입니다.
 *   <li>계정 토큰({@code token}) - 그 클라이언트가 정말 그 계정인지 봅니다. 없으면 정지된
 *       사람이 남의 userId 를 넣어 통과합니다.
 * </ul>
 *
 * <p><b>응답은 항상 200 입니다.</b> 거절도 200 에 {@code ResultCode: 2} 로 나갑니다.
 * Photon 은 HTTP 오류를 "인증 서버 고장"으로 읽고 대시보드 설정에 따라 통과시키므로,
 * 403 이나 401 로 답하면 막으려던 사람이 들어옵니다.
 *
 * <p><b>알 수 없는 계정은 통과시킵니다.</b> 백엔드가 앱 시작 때 잠깐 죽어 있어 클라이언트가
 * 아직 계정을 못 받은 경우가 있고, 그때 막으면 fail-open 결정과 어긋납니다. 토큰 검증이
 * 먼저라 아무 값이나 넣어 통과하는 길은 닫혀 있습니다.
 */
@RestController
@RequestMapping("/api/v1/photon")
@RequiredArgsConstructor
@Slf4j
public class PhotonAuthController {

    /** Photon 규격의 결과 코드입니다. 0(미완료)은 여러 단계 인증용이라 쓰지 않습니다. */
    private static final int OK = 1;
    private static final int REJECTED = 2;
    private static final int BAD_PARAMETERS = 3;

    private final UserRepository userRepository;
    private final AccountTokens tokens;

    @Value("${photon.auth.key:}")
    private String expectedKey;

    @GetMapping("/auth")
    public PhotonAuthResponse authenticate(@RequestParam(required = false) String userId,
                                           @RequestParam(required = false) String token,
                                           @RequestParam(required = false) String key) {
        // 설정이 없으면 이 기능 전체가 꺼진 것으로 봅니다. 반쯤 켜진 상태에서 막기 시작하면
        // 배포 순서가 어긋났을 때 아무도 게임에 못 들어옵니다.
        if (!tokens.isEnabled()) {
            return PhotonAuthResponse.ok(userId);
        }

        if (!hasExpectedKey(key)) {
            // Photon 을 거치지 않은 요청입니다. 주소가 공개라 누구나 부를 수 있고,
            // 여기서 통과시키면 정지 여부를 밖에서 물어볼 수 있는 창구가 됩니다.
            log.warn("Photon 인증에 공유 비밀이 맞지 않습니다. 비밀 지문={}", tokens.fingerprint());
            return PhotonAuthResponse.badParameters("Invalid key.");
        }

        if (userId == null || userId.isBlank()) {
            return PhotonAuthResponse.badParameters("Missing userId.");
        }

        if (!tokens.matches(userId, token)) {
            return PhotonAuthResponse.badParameters("Invalid token.");
        }

        Optional<User> user = userRepository.findByPublicId(userId);
        if (user.isEmpty()) {
            return PhotonAuthResponse.ok(userId);
        }

        if (user.get().isSuspended()) {
            log.info("정지된 계정의 Photon 접속을 막았습니다. userId={}", userId);
            return PhotonAuthResponse.rejected("이용이 제한된 계정입니다.");
        }

        return PhotonAuthResponse.ok(userId);
    }

    /**
     * 길이가 다르면 {@link MessageDigest#isEqual} 도 일찍 끝나지만, 비밀의 길이는
     * 숨길 값이 아니고 값 자체를 한 글자씩 맞혀 나가는 것만 막으면 됩니다.
     */
    private boolean hasExpectedKey(String presented) {
        if (expectedKey == null || expectedKey.isBlank()) {
            // 토큰 비밀은 있는데 이 값만 비어 있는 경우입니다. 대시보드에 아직 넣지 않은
            // 상태일 수 있으므로 막지 않고, 토큰 검증만으로 갑니다.
            return true;
        }
        if (presented == null) {
            return false;
        }
        return MessageDigest.isEqual(
                presented.getBytes(StandardCharsets.UTF_8),
                expectedKey.getBytes(StandardCharsets.UTF_8));
    }

    /**
     * Photon 이 읽는 응답입니다. ResultCode 만 필수이고 나머지는 선택입니다.
     *
     * <p>UserId 를 돌려주면 클라이언트의 값을 덮어씁니다. 우리가 받은 값을 그대로 돌려주어
     * 클라이언트가 정한 id 와 어긋나지 않게 합니다.
     */
    public record PhotonAuthResponse(int ResultCode, String UserId, String Message) {

        static PhotonAuthResponse ok(String userId) {
            return new PhotonAuthResponse(OK, userId, null);
        }

        static PhotonAuthResponse rejected(String message) {
            return new PhotonAuthResponse(REJECTED, null, message);
        }

        static PhotonAuthResponse badParameters(String message) {
            return new PhotonAuthResponse(BAD_PARAMETERS, null, message);
        }
    }
}
