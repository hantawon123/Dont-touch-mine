package com.ssafy.d205.friend;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;

/**
 * @param userId 요청을 받을 사람의 공개 식별자(users.public_id).
 *               <p>닉네임이 아니라 id 로 받습니다. 닉네임은 바뀔 수 있고 대소문자를
 *               구분하므로, 검색 결과에서 고른 대상을 정확히 가리키려면 id 여야 합니다.
 */
public record SendFriendRequestRequest(
        @NotBlank(message = "userId는 필수입니다.")
        @Size(max = 36, message = "userId는 36자를 넘을 수 없습니다.")
        String userId
) {
}
