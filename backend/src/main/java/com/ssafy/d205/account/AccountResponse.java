package com.ssafy.d205.account;

import com.ssafy.d205.user.User;

/**
 * @param userId users.public_id입니다. 이후 요청에서 X-User-Id 헤더로 이 값을 보냅니다.
 *               Photon UserId로도 같은 값을 씁니다.
 *               <p>내부 seq와 provider_user_id는 여기에 담지 않습니다. 전자는 가입자
 *               수와 다른 계정을 노출하고, 후자는 자격증명입니다.
 */
public record AccountResponse(
        String userId,
        String nickname,
        String createdAt
) {
    public static AccountResponse from(User user) {
        return new AccountResponse(user.getPublicId(), user.getNickname(), user.getCreatedAt());
    }
}
