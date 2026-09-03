package com.ssafy.d205.domain.user.controller;

import jakarta.validation.constraints.Max;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.Pattern;
import lombok.RequiredArgsConstructor;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestHeader;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import com.ssafy.d205.domain.user.dto.UserSearchResponse;
import com.ssafy.d205.domain.user.entity.NicknamePolicy;
import com.ssafy.d205.domain.user.entity.User;
import com.ssafy.d205.domain.user.service.UserSearchService;

@RestController
@RequestMapping("/api/v1/users")
@RequiredArgsConstructor
public class UserSearchController {

    private static final String USER_ID_HEADER = "X-User-Id";

    private final UserSearchService userSearchService;

    /**
     * 닉네임으로 다른 사용자를 찾습니다. 접두사 일치입니다.
     *
     * <p>검색어에 닉네임과 같은 문자 규칙을 적용합니다. 이게 <b>보안 장치도
     * 됩니다.</b> 쿼리가 LIKE CONCAT(:prefix, '%') 형태라 검색어에 % 나 _ 가 들어오면
     * 와일드카드로 해석되어 전체 사용자가 매칭됩니다. 허용 문자 목록이 그걸 막습니다.
     *
     * <p>최소 두 글자를 요구하는 것은 한 글자로 테이블을 훑는 것을 막기 위해서입니다.
     * 닉네임 자체가 두 글자 이상이므로 못 찾을 사람도 없습니다.
     */
    @GetMapping
    public UserSearchResponse search(
            @RequestHeader(USER_ID_HEADER) String userId,

            @Pattern(regexp = NicknamePolicy.REGEX,
                    message = "검색어는 한글, 영문, 숫자만 써서 2~12글자여야 합니다.")
            @RequestParam String nickname,

            @Min(value = 1, message = "limit은 1 이상이어야 합니다.")
            @Max(value = 50, message = "limit은 50을 넘을 수 없습니다.")
            @RequestParam(defaultValue = "20") int limit
    ) {
        return userSearchService.searchByNickname(userId, nickname, limit);
    }
}
