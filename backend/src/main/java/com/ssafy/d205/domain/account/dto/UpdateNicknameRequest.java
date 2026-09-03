package com.ssafy.d205.domain.account.dto;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Pattern;

import com.ssafy.d205.domain.user.entity.NicknamePolicy;

public record UpdateNicknameRequest(
        @NotBlank(message = "nickname은 필수입니다.")
        @Pattern(regexp = NicknamePolicy.REGEX,
                message = "닉네임은 한글, 영문, 숫자만 써서 2~12글자여야 합니다. 공백과 특수문자는 쓸 수 없습니다.")
        String nickname
) {
}
