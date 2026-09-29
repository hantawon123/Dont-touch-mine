package com.ssafy.d205.domain.user.dto;

import jakarta.validation.constraints.NotNull;

/**
 * @param searchable 닉네임 검색에 나올지. <b>Boolean 이지 boolean 이 아닙니다.</b>
 *                   기본형으로 두면 값을 빠뜨린 요청에 false 가 채워져, "끄겠다"는 뜻이
 *                   아닌데 꺼집니다. 감싼 형이라야 &#64;NotNull 이 그것을 잡습니다.
 */
public record UpdateSearchableRequest(
        @NotNull(message = "searchable은 필수입니다.")
        Boolean searchable
) {
}
