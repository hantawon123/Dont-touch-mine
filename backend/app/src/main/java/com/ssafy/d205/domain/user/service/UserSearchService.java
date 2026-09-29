package com.ssafy.d205.domain.user.service;

import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.List;

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
     * 닉네임이 정확히 일치하는 사용자를 찾습니다.
     *
     * <p>부르는 사람이 누군지 알아야 합니다. 자기 자신을 결과에서 빼려면 내부 seq 가
     * 필요하기 때문입니다. 그래서 존재하지 않는 userId 로 부르면 404 입니다. 빈 결과가
     * 아니라 404 인 것은 "검색 결과가 없다"와 "당신이 누군지 모르겠다"가 다른
     * 상황이기 때문입니다.
     *
     * <p><b>검색어를 손대지 않습니다.</b> 예전에는 소문자로 바꿔 넘겼는데, 그때는
     * 대소문자를 무시하고 접두사로 찾았기 때문입니다. 지금은 대소문자를 구분하므로
     * 받은 그대로 비교해야 합니다.
     *
     * <p>limit 은 결과에 영향을 주지 않습니다. 유니크 제약 때문에 많아야 한 건입니다.
     * 파라미터를 없애지 않은 것은 계약을 깨지 않기 위해서입니다.
     */
    @Transactional(readOnly = true)
    public UserSearchResponse searchByNickname(String callerUserId, String nickname, int limit) {
        User caller = userRepository.findByPublicId(callerUserId)
                .orElseThrow(() -> new UnknownCallerException(callerUserId));

        // 소문자로 바꾸지 않습니다. 대소문자를 구분하므로 받은 그대로 비교합니다.
        // limit 은 쓰지 않습니다. 유니크 제약 때문에 결과가 많아야 한 건입니다.
        List<UserSummary> users = userRepository
                .findByExactNickname(nickname, caller.getSeq())
                .stream()
                .map(row -> new UserSummary(row.getUserId(), row.getNickname()))
                .toList();

        return new UserSearchResponse(users);
    }
}
