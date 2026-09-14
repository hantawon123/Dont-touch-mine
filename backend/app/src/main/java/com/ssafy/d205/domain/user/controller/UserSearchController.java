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
import com.ssafy.d205.domain.user.service.UserSearchService;

@RestController
@RequestMapping("/api/v1/users")
@RequiredArgsConstructor
public class UserSearchController {

    private static final String USER_ID_HEADER = "X-User-Id";

    private final UserSearchService userSearchService;

    /**
     * 닉네임이 <b>정확히 일치하는</b> 사용자를 찾습니다.
     *
     * <p>앞글자로는 찾히지 않습니다. 예전에는 접두사로 찾을 수 있었는데, 몇 글자만 쳐도
     * 모르는 사람이 걸려 나왔습니다. 친구를 찾으려면 닉네임을 알고 있어야 합니다.
     *
     * <p><b>대소문자를 구분합니다.</b> player 를 검색하면 Player 는 나오지 않습니다.
     * 둘은 서로 다른 닉네임이고 동시에 존재할 수 있습니다.
     *
     * <p>검색어에 닉네임과 같은 문자 규칙을 적용합니다. 닉네임이 될 수 없는 값은 어차피
     * 아무도 가질 수 없으므로 조회할 이유가 없습니다.
     *
     * <p>접두사이던 동안에는 이 검사가 보안 장치이기도 했습니다. LIKE 패턴에 % 나 _ 가
     * 들어오면 와일드카드로 해석되어 전체 사용자가 걸렸기 때문입니다. 지금은 등호
     * 비교라 그 위험이 없어졌지만, 검사는 그대로 둡니다. 쿼리 모양이 다시 바뀌었을 때
     * 방어가 사라져 있는 것보다 낫습니다.
     */
    @GetMapping
    public UserSearchResponse search(
            @RequestHeader(USER_ID_HEADER) String userId,

            // 닉네임 규칙과 같은 검사입니다. 정확히 일치로 찾으므로, 닉네임이 될 수 없는
            // 값은 어차피 아무도 가질 수 없어 조회할 이유가 없습니다.
            @Pattern(regexp = NicknamePolicy.REGEX,
                    message = "검색어는 한글, 영문, 숫자만 써서 2~12글자여야 합니다.")
            @RequestParam String nickname,

            // 결과가 많아야 한 건이라 이 값은 동작에 영향을 주지 않습니다. 계약을
            // 깨지 않으려고 남겨 두었습니다.
            @Min(value = 1, message = "limit은 1 이상이어야 합니다.")
            @Max(value = 50, message = "limit은 50을 넘을 수 없습니다.")
            @RequestParam(defaultValue = "20") int limit
    ) {
        return userSearchService.searchByNickname(userId, nickname, limit);
    }
}
