package com.ssafy.d205.domain.account.service;

import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Component;
import org.springframework.transaction.annotation.Transactional;

import com.ssafy.d205.domain.account.dto.AccountResponse;
import com.ssafy.d205.domain.user.entity.AuthProvider;
import com.ssafy.d205.domain.user.entity.NicknameGenerator;
import com.ssafy.d205.domain.user.entity.User;
import com.ssafy.d205.domain.user.entity.UserIdentity;
import com.ssafy.d205.domain.user.repository.UserIdentityRepository;
import com.ssafy.d205.domain.user.repository.UserRepository;

/**
 * 새 계정을 만드는 일만 합니다. 유니크 제약 위반은 여기서 잡지 않고 그대로 올려보냅니다.
 *
 * <p><b>이 코드가 AccountService와 다른 빈에 있는 이유</b>가 있습니다. 트랜잭션 안에서
 * 제약 위반이 나면 그 트랜잭션은 롤백 전용으로 표시되고, 뒤이은 조회까지 함께 실패합니다.
 * 그래서 "삽입이 실패하면 다시 읽는다"를 하려면 잡는 쪽이 트랜잭션 밖에 있어야 합니다.
 * 같은 클래스의 메서드를 부르면 스프링 프록시를 지나지 않아 @Transactional이 아예
 * 걸리지 않으므로, 클래스를 나누는 것이 유일한 방법입니다.
 */
@Component
@RequiredArgsConstructor
public class AccountRegistrar {

    private final UserRepository userRepository;
    private final UserIdentityRepository userIdentityRepository;
    private final NicknameGenerator nicknameGenerator;

    /**
     * users와 user_identities에 각각 한 행을 넣습니다. 둘 중 하나만 남으면
     * 신원 없는 유령 계정이나 주인 없는 신원이 되므로 한 트랜잭션으로 묶습니다.
     */
    @Transactional
    public AccountResponse register(String deviceId) {
        User user = userRepository.save(User.create(nicknameGenerator.generate()));
        userIdentityRepository.save(UserIdentity.link(user, AuthProvider.DEVICE, deviceId));
        return AccountResponse.from(user);
    }
}
