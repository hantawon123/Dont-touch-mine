package com.ssafy.d205.domain.user.service;

import lombok.RequiredArgsConstructor;
import org.springframework.data.domain.PageRequest;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.List;
import java.util.Locale;

import com.ssafy.d205.domain.user.dto.UserSearchResponse;
import com.ssafy.d205.domain.user.dto.UserSummary;
import com.ssafy.d205.domain.user.entity.User;
import com.ssafy.d205.domain.user.repository.UserRepository;
import com.ssafy.d205.global.exception.UnknownCallerException;

@Service
@RequiredArgsConstructor
public class UserSearchService {

    private final UserRepository userRepository;

    /**
     * 닉네임 접두사로 다른 사용자를 찾습니다.
     *
     * <p>부르는 사람이 누군지 알아야 합니다. 자기 자신을 결과에서 빼고 차단 관계를
     * 검사하려면 내부 seq 가 필요하기 때문입니다. 그래서 존재하지 않는 userId 로
     * 부르면 404 입니다. 빈 결과가 아니라 404 인 것은 "검색 결과가 없다"와 "당신이
     * 누군지 모르겠다"가 다른 상황이기 때문입니다.
     *
     * <p>검색어를 소문자로 바꿔 넘깁니다. nickname_lower 컬럼이 as_cs 콜레이션이라
     * 대문자가 섞이면 아무것도 걸리지 않습니다.
     *
     * <p>Locale.ROOT 를 쓰는 이유는 터키어 로케일에서 대문자 I 가 점 없는 소문자로
     * 바뀌는 문제를 피하려는 것입니다. 서버 로케일에 따라 검색 결과가 달라지면
     * 재현이 안 되는 버그가 됩니다.
     */
    @Transactional(readOnly = true)
    public UserSearchResponse searchByNickname(String callerUserId, String nickname, int limit) {
        User caller = userRepository.findByPublicId(callerUserId)
                .orElseThrow(() -> new UnknownCallerException(callerUserId));

        List<UserSummary> users = userRepository
                .searchByNicknamePrefix(nickname.toLowerCase(Locale.ROOT),
                                        caller.getSeq(),
                                        PageRequest.of(0, limit))
                .stream()
                .map(row -> new UserSummary(row.getUserId(), row.getNickname()))
                .toList();

        return new UserSearchResponse(users);
    }
}
