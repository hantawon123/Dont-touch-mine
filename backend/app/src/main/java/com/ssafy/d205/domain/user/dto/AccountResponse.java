package com.ssafy.d205.domain.user.dto;

import com.ssafy.d205.domain.user.entity.User;
import com.ssafy.d205.domain.user.entity.UserAppearance;

/**
 * @param nicknameSet 사용자가 닉네임을 직접 정했는지. false면 서버가 지어준 임시
 *                    닉네임이므로 클라이언트가 입력 화면을 띄워야 합니다.
 * @param userId users.public_id입니다. 이후 요청에서 X-User-Id 헤더로 이 값을 보냅니다.
 *               방에 들어갈 때는 Fusion 연결 토큰에 실어 보내고, 호스트가 PlayerAvatar.UserId
 *               로 복제해 같은 방 사람들이 서로의 값을 알게 됩니다.
 *               <p>내부 seq와 provider_user_id는 여기에 담지 않습니다. 전자는 가입자
 *               수와 다른 계정을 노출하고, 후자는 자격증명입니다.
 * @param appearanceSet 옷장에서 외형을 한 번이라도 저장했는지. false 면 {@code appearance}
 *                      는 null 이고 클라이언트가 기본 파츠를 씁니다.
 *                      <p>{@code appearance == null} 만으로 판별하게 두지 않은 이유는
 *                      Unity 의 JsonUtility 가 null 객체를 만들지 않기 때문입니다. 그쪽에서는
 *                      필드가 빈 객체가 됩니다. 불리언은 그 함정이 없습니다. nicknameSet 과
 *                      같은 자리입니다.
 * @param appearance 저장한 외형. 저장한 적이 없으면 null.
 */
public record AccountResponse(
        String userId,
        String nickname,
        boolean nicknameSet,
        boolean searchable,
        boolean appearanceSet,
        AppearanceResponse appearance,
        String createdAt,

        /**
         * Photon 커스텀 인증에 실어 보낼 토큰입니다(S15P21D205-925).
         *
         * <p>서버 비밀이 설정되지 않았으면 null 입니다. 그때는 인증도 꺼진 상태라
         * 클라이언트가 들고 갈 것이 없습니다.
         *
         * <p>userId 와 짝입니다. 이것 없이 userId 만 보내면 정지된 사람이 남의 값을 넣어
         * Photon 접속을 통과합니다.
         */
        String photonToken
) {
    /** 외형을 아직 고르지 않은 계정. 새로 만든 계정은 늘 여기입니다. */
    public static AccountResponse from(User user) {
        return from(user, null, null);
    }

    /** @param appearance 저장된 외형. 없으면 null. */
    public static AccountResponse from(User user, UserAppearance appearance) {
        return from(user, appearance, null);
    }

    /** @param photonToken Photon 인증 토큰. 비밀이 없으면 null. */
    public static AccountResponse from(User user, UserAppearance appearance, String photonToken) {
        return new AccountResponse(
                user.getPublicId(),
                user.getNickname(),
                user.isNicknameSet(),
                user.isSearchable(),
                appearance != null,
                appearance == null ? null : AppearanceResponse.from(appearance),
                user.getCreatedAt(),
                photonToken);
    }
}
