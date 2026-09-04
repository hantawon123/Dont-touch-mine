package com.ssafy.d205.domain.invite.dto;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Pattern;
import jakarta.validation.constraints.Size;

import com.ssafy.d205.domain.invite.entity.RoomCodePolicy;

/**
 * @param userId   부를 사람의 공개 식별자. 닉네임이 아니라 id 로 받는 이유는 친구
 *                 요청과 같습니다 — 닉네임은 바뀌고 대소문자를 구분합니다.
 * @param roomCode 지금 있는 방의 코드. 서버는 이 방이 실제로 있는지 모릅니다.
 *                 형식만 확인하고, 없는 방이면 상대가 입장할 때 실패합니다.
 */
public record SendInviteRequest(
        @NotBlank(message = "userId는 필수입니다.")
        @Size(max = 36, message = "userId는 36자를 넘을 수 없습니다.")
        String userId,

        @NotBlank(message = "roomCode는 필수입니다.")
        @Pattern(regexp = RoomCodePolicy.REGEX,
                message = "방 코드는 숫자와 대문자 6자여야 합니다. I, O, U, L은 쓰지 않습니다.")
        String roomCode
) {
}
