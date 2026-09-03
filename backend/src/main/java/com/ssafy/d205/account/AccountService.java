package com.ssafy.d205.account;

import lombok.RequiredArgsConstructor;
import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.Optional;

import com.ssafy.d205.user.AuthProvider;
import com.ssafy.d205.user.User;
import com.ssafy.d205.user.UserIdentityRepository;
import com.ssafy.d205.user.UserRepository;

@Service
@RequiredArgsConstructor
public class AccountService {

    /**
     * 닉네임 충돌 재시도 횟수입니다. 조합이 230만 가지라 한 번 충돌할 확률도 낮고,
     * 다섯 번 연속 충돌한다면 그건 운이 아니라 무언가 잘못된 것이므로 예외로 알립니다.
     */
    private static final int MAX_ATTEMPTS = 5;

    private final AccountRegistrar accountRegistrar;
    private final UserRepository userRepository;
    private final UserIdentityRepository userIdentityRepository;

    /**
     * 기기 식별자로 계정을 발급합니다. <b>멱등합니다.</b>
     *
     * <p>같은 기기가 다시 부르면 새로 만들지 않고 기존 계정을 돌려줍니다. 클라이언트가
     * 응답을 못 받아 재시도하거나 앱을 지웠다 다시 깔아도 계정이 하나로 유지됩니다.
     * 이게 "중복 발급 방지"의 구현입니다.
     *
     * <p>먼저 조회하고 없으면 넣는 방식만으로는 <b>동시 요청을 막지 못합니다.</b> 두
     * 요청이 조회를 나란히 통과한 뒤 둘 다 삽입을 시도하기 때문입니다. 실제로 막는 것은
     * uk_user_identities_provider이고, 이 메서드는 그 제약에 걸린 쪽이 이긴 쪽의 결과를
     * 읽어 가도록 처리합니다.
     */
    public IssuedAccount issue(String deviceId) {
        Optional<User> existing = findByDevice(deviceId);
        if (existing.isPresent()) {
            return new IssuedAccount(AccountResponse.from(existing.get()), false);
        }

        for (int attempt = 0; attempt < MAX_ATTEMPTS; attempt++) {
            try {
                return new IssuedAccount(accountRegistrar.register(deviceId), true);
            } catch (DataIntegrityViolationException e) {
                // 여기로 오는 경우가 두 가지입니다.
                //
                //   1. 같은 기기가 동시에 요청해 uk_user_identities_provider에 걸렸다
                //   2. 자동 생성한 닉네임이 uk_users_nickname에 걸렸다
                //
                // 예외 메시지로 둘을 가르는 것은 드라이버와 MySQL 버전에 기대는 일이라
                // 하지 않습니다. 대신 기기로 다시 조회해서, 계정이 생겼으면 1번이고
                // 없으면 2번으로 판단합니다. 상태를 보고 판단하는 쪽이 튼튼합니다.
                Optional<User> winner = findByDevice(deviceId);
                if (winner.isPresent()) {
                    return new IssuedAccount(AccountResponse.from(winner.get()), false);
                }
            }
        }
        throw new NicknameGenerationFailedException(MAX_ATTEMPTS);
    }

    @Transactional(readOnly = true)
    public AccountResponse get(String userId) {
        return userRepository.findByPublicId(userId)
                .map(AccountResponse::from)
                .orElseThrow(() -> new AccountNotFoundException(userId));
    }

    /**
     * 닉네임을 바꿉니다.
     *
     * <p>미리 조회해서 중복을 확인하지만 그것만으로는 동시 요청을 막지 못합니다. 확인은
     * 친절한 메시지를 주기 위한 것이고, 실제로 막는 것은 uk_users_nickname입니다.
     * 제약에 걸린 예외는 핸들러가 409로 옮깁니다.
     *
     * <p>대소문자를 구분합니다(V4). player와 Player는 서로 다른 닉네임이라 둘이 동시에
     * 존재할 수 있습니다. 그래서 자기 닉네임인지 판별할 때도 정확히 같은지를 봐야
     * 합니다. equalsIgnoreCase로 두면 남이 쓰는 Player를 자기 것으로 착각해 중복
     * 검사를 건너뛰고, 제약 위반이 409가 아니라 500으로 나갑니다.
     */
    @Transactional
    public AccountResponse rename(String userId, String nickname) {
        User user = userRepository.findByPublicId(userId)
                .orElseThrow(() -> new AccountNotFoundException(userId));

        if (!user.getNickname().equals(nickname) && userRepository.existsByNickname(nickname)) {
            throw new NicknameTakenException(nickname);
        }

        user.rename(nickname);
        return AccountResponse.from(user);
    }

    private Optional<User> findByDevice(String deviceId) {
        return userIdentityRepository.findUserByProviderAndProviderUserId(AuthProvider.DEVICE, deviceId);
    }
}
