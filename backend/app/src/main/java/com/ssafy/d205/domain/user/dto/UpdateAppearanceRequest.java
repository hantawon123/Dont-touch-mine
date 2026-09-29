package com.ssafy.d205.domain.user.dto;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Pattern;

import com.ssafy.d205.domain.user.entity.AppearancePolicy;

/**
 * 옷장에서 고른 파츠 넷. <b>넷 다 필수</b>입니다.
 *
 * <p>일부만 받아 나머지를 유지하는 PATCH 로 만들지 않았습니다. 화면은 항상 넷을 다 알고
 * 있어서 부분 갱신이 필요한 곳이 없고, 선택으로 두면 빈 요청 {@code {}} 가 400 대신 조용한
 * 200 이 됩니다. 검색 허용 설정을 PATCH /me 에 합치지 않은 것과 같은 판단입니다.
 *
 * <p>값의 뜻은 서버가 모릅니다. {@link AppearancePolicy} 형식만 봅니다.
 */
public record UpdateAppearanceRequest(
        @NotBlank(message = "bodyColor는 필수입니다.")
        @Pattern(regexp = AppearancePolicy.REGEX, message = "bodyColor는 공백 없는 1~32자여야 합니다.")
        String bodyColor,

        @NotBlank(message = "hood는 필수입니다.")
        @Pattern(regexp = AppearancePolicy.REGEX, message = "hood는 공백 없는 1~32자여야 합니다.")
        String hood,

        @NotBlank(message = "shoes는 필수입니다.")
        @Pattern(regexp = AppearancePolicy.REGEX, message = "shoes는 공백 없는 1~32자여야 합니다.")
        String shoes,

        @NotBlank(message = "face는 필수입니다.")
        @Pattern(regexp = AppearancePolicy.REGEX, message = "face는 공백 없는 1~32자여야 합니다.")
        String face
) {
}
