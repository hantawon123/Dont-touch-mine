package com.ssafy.d205.domain.user.dto;

import com.ssafy.d205.domain.user.entity.UserAppearance;

/**
 * 저장된 외형. 네 파츠 id 를 그대로 돌려줍니다.
 *
 * <p>클라이언트가 모르는 id 가 올 수 있습니다. 카탈로그에서 파츠를 빼거나 이름을 바꾼 뒤에도
 * 이전에 저장한 값은 그대로 남기 때문입니다. 읽는 쪽이 기본 파츠로 대체해야 합니다.
 */
public record AppearanceResponse(
        String bodyColor,
        String hood,
        String shoes,
        String face
) {
    public static AppearanceResponse from(UserAppearance appearance) {
        return new AppearanceResponse(
                appearance.getBodyColor(),
                appearance.getHood(),
                appearance.getShoes(),
                appearance.getFace());
    }
}
