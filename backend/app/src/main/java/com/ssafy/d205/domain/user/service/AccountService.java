package com.ssafy.d205.domain.user.service;

import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.context.ApplicationEventPublisher;
import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.Optional;

import com.ssafy.d205.domain.user.dto.AccountResponse;
import com.ssafy.d205.domain.user.dto.IssuedAccount;
import com.ssafy.d205.domain.user.dto.UpdateAppearanceRequest;
import com.ssafy.d205.domain.user.entity.AuthProvider;
import com.ssafy.d205.domain.user.entity.User;
import com.ssafy.d205.domain.user.entity.UserAppearance;
import com.ssafy.d205.domain.user.event.AccountDeletedEvent;
import com.ssafy.d205.domain.user.repository.UserAppearanceRepository;
import com.ssafy.d205.domain.user.repository.UserIdentityRepository;
import com.ssafy.d205.global.security.AccountTokens;
import com.ssafy.d205.domain.user.repository.UserRepository;
import com.ssafy.d205.global.common.TimeProvider;
import com.ssafy.d205.global.exception.NicknameGenerationFailedException;
import com.ssafy.d205.global.exception.NicknameTakenException;
import com.ssafy.d205.global.exception.SuspendedAccountException;
import com.ssafy.d205.global.exception.UnknownCallerException;

@Service
@Slf4j
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
    private final UserAppearanceRepository userAppearanceRepository;
    private final TimeProvider timeProvider;
    private final ApplicationEventPublisher events;
    private final AccountTokens accountTokens;

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
            // 정지된 계정은 여기서 막습니다. 클라이언트가 앱을 켤 때 처음 부르는 자리라
            // 그 한 판을 통째로 막는 유일한 지점입니다.
            //
            // 이것만으로는 반쪽입니다. 발급은 앱 시작 때 한 번만 불리므로, 이미 켜져 있는
            // 클라이언트는 X-User-Id 를 이미 들고 있어 계속 요청할 수 있습니다. 나머지
            // 절반은 SuspensionInterceptor 가 막습니다.
            //
            // 새 계정 발급은 막지 않습니다. 정지는 계정에 걸린 것이고, 기기 식별자를
            // 바꾸면 새 계정이 나오는 것은 이 구조에서 어차피 막을 수 없습니다.
            requireNotSuspended(existing.get());
            return new IssuedAccount(respond(existing.get()), false);
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
                    return new IssuedAccount(respond(winner.get()), false);
                }
            }
        }
        throw new NicknameGenerationFailedException(MAX_ATTEMPTS);
    }

    @Transactional(readOnly = true)
    public AccountResponse get(String userId) {
        return respond(caller(userId));
    }

    /**
     * 옷장에서 고른 외형을 저장합니다. <b>멱등합니다.</b>
     *
     * <p>PUT 하나로 넣기와 덮어쓰기를 다 합니다. 처음 저장인지 아닌지를 클라이언트가 구분해
     * 부를 이유가 없고, 같은 값을 다시 저장해도 성공입니다. 적용 버튼을 두 번 눌렀다고
     * 오류가 날 이유가 없습니다.
     *
     * <p>파츠 id 가 실제로 있는지는 보지 않습니다. 형식은 요청 검증이 봤고, 목록은 서버가
     * 모릅니다(AppearancePolicy).
     */
    @Transactional
    public AccountResponse setAppearance(String userId, UpdateAppearanceRequest request) {
        User user = caller(userId);

        userAppearanceRepository.upsert(
                user.getSeq(),
                request.bodyColor(),
                request.hood(),
                request.shoes(),
                request.face(),
                timeProvider.now());

        return respond(user);
    }

    /**
     * 외형을 초기화합니다. 행을 지워 "아직 고르지 않은" 상태로 되돌립니다.
     *
     * <p>클라이언트가 기본 파츠로 PUT 하게 두지 않은 이유가 있습니다. 그렇게 하면 한 번
     * 저장한 사람은 영원히 appearanceSet 이 true 이고, 나중에 기본 파츠가 바뀌어도 초기화를
     * 누른 사람들은 옛 기본값에 못 박힙니다. 행이 없으면 클라이언트가 그때의 기본값을 씁니다.
     *
     * <p>없는 행을 지워도 성공입니다. 초기화를 두 번 눌러도 결과는 같습니다.
     */
    @Transactional
    public AccountResponse clearAppearance(String userId) {
        User user = caller(userId);

        userAppearanceRepository.findById(user.getSeq())
                .ifPresent(userAppearanceRepository::delete);

        return AccountResponse.from(user);
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
    /**
     * 닉네임 검색에 나올지 정합니다.
     *
     * <p>닉네임 변경과 따로 둔 이유는 성질이 다르기 때문입니다. 닉네임은 남과 겹칠 수
     * 있어 거절당하지만, 이 값은 나 혼자의 것이라 늘 성공합니다. 한 요청으로 묶으면
     * 닉네임이 중복이라 409 가 날 때 이 설정까지 함께 되돌아갑니다 - 사용자는
     * 체크박스만 껐는데 그것도 안 먹힌 셈이 됩니다.
     *
     * <p>같은 값을 다시 넣어도 성공합니다. 체크박스를 두 번 눌러 원래대로 돌아온
     * 경우가 오류일 이유가 없습니다.
     */
    @Transactional
    public AccountResponse setSearchable(String userId, boolean searchable) {
        User user = caller(userId);

        user.setSearchable(searchable, timeProvider.now());
        return respond(user);
    }

    @Transactional
    public AccountResponse rename(String userId, String nickname) {
        User user = caller(userId);

        if (!user.getNickname().equals(nickname) && userRepository.existsByNickname(nickname)) {
            throw new NicknameTakenException(nickname);
        }

        user.rename(nickname, timeProvider.now());
        return respond(user);
    }

    /**
     * 계정을 삭제합니다. <b>되돌릴 수 없습니다.</b>
     *
     * <p>하드 삭제입니다. users 한 행을 지우면 user_identities, friendships,
     * user_presence 가 ON DELETE CASCADE 로 함께 사라집니다. 스키마가 그렇게
     * 설계돼 있고, 소프트 삭제는 조회 쿼리 열 곳에 필터를 추가해야 하는데 하나만
     * 빠뜨려도 삭제된 계정이 조용히 새어 나옵니다.
     *
     * <p><b>자격증명을 요구합니다.</b> X-User-Id 는 인증이 아니라 식별이라 남의 id 를 아는
     * 사람이 그 계정을 지울 수 있습니다. 다른 API 는 조작해도 되돌릴 수 있지만 이건
     * 복구가 안 됩니다. deviceId 는 user_identities 주석대로 자격증명이고 클라이언트가
     * 갖고 있으므로 요구해도 부담이 없습니다.
     *
     * <p>자격증명이 틀리면 계정이 없는 것과 같은 응답을 줍니다. "userId 는 맞는데
     * deviceId 가 틀렸다"고 알려주면 <b>남의 계정이 존재한다는 사실이 드러납니다.</b>
     *
     * <p><b>DEVICE 신원에만 동작합니다.</b> AuthProvider 에는 STEAM 과 EPIC 도 있지만
     * 여기서는 DEVICE 를 찾습니다. 지금은 발급이 항상 DEVICE 행을 만들어서 모든 계정에
     * 그 행이 있으므로 문제가 없습니다.
     *
     * <p>STEAM/EPIC 연결을 붙일 때 이 결합을 반드시 다시 봐야 합니다. Steam 으로만
     * 시작해서 DEVICE 행이 없는 계정이 생기면 <b>그 계정은 영구히 탈퇴할 수 없습니다.</b>
     * 지금 미리 일반화하지 않는 것은 신원 종류가 하나뿐인 상태에서 만드는 추상화가
     * 실제 요구와 어긋날 가능성이 크기 때문입니다.
     */
    @Transactional
    public void delete(String callerUserId, String deviceId) {
        // 자격증명으로 계정을 찾고 그것이 호출자와 같은지만 봅니다. publicId 로 한 번 더
        // 찾아 seq 를 비교할 필요가 없습니다. 호출자가 없거나 deviceId 가 없거나 둘이
        // 안 맞는 세 경우 모두 같은 404 로 끝나므로 관찰되는 동작이 같고, 이렇게 쓰면
        // 코드가 보안 성질을 그대로 말합니다 — 자격증명이 가리키는 계정만 지운다.
        User user = userIdentityRepository
                .findUserByProviderAndProviderUserId(AuthProvider.DEVICE, deviceId)
                .filter(found -> found.getPublicId().equals(callerUserId))
                .orElseThrow(() -> {
                    // 남의 userId 로 deviceId 를 찔러보는 시도를 남깁니다. 응답은 계정이
                    // 없는 것과 구분되지 않으므로, 흔적이 남는 곳은 여기뿐입니다.
                    //
                    // 문구를 단정하지 않는 이유가 있습니다. 여기 오는 경우가 셋인데
                    // (기기 식별자가 없음 / 호출자가 없음 / 둘이 안 맞음) 셋을 구분하려면
                    // 조회를 한 번 더 해야 합니다. "맞지 않습니다"로 적으면 탈퇴한
                    // 클라이언트의 재시도를 찔러보기로 읽게 됩니다.
                    //
                    // deviceId 는 절대 넣지 않습니다. 자격증명이라 로그에 남으면 그
                    // 로그를 읽을 수 있는 사람이 계정을 지울 수 있게 됩니다.
                    log.warn("계정 삭제 실패 - 자격증명으로 계정을 확인할 수 없습니다. userId={}",
                            callerUserId);
                    return new UnknownCallerException(callerUserId);
                });

        // 자식 행은 DB의 ON DELETE CASCADE 가 지웁니다. UserIdentity 의 @ManyToOne 에
        // cascade 설정이 없는 것은 그래서입니다.
        //
        // 주의: Hibernate 는 그 삭제를 모릅니다. 이 뒤에 같은 트랜잭션에서 자식 엔티티를
        // 로드해 쓰는 코드를 넣으면 영속성 컨텍스트에 살아 있는 자식이 남고, flush 순서에
        // 따라 FK 위반이나 지워진 행의 부활이 생깁니다.
        userRepository.delete(user);

        // 플레이 로그(분석 스키마)는 CASCADE 가 닿지 않는 다른 DB 접속입니다. 거기서 이 사람의
        // 식별자를 지우는 일은 커밋 뒤에 듣는 쪽(GameEventEraser)이 합니다. 여기서 직접 부르면
        // 분석 DB 장애가 탈퇴를 실패시키고, 롤백되면 지울 이유가 없는 행을 지웁니다.
        events.publishEvent(new AccountDeletedEvent(user.getPublicId()));
    }

    private Optional<User> findByDevice(String deviceId) {
        return userIdentityRepository.findUserByProviderAndProviderUserId(AuthProvider.DEVICE, deviceId);
    }

    private User caller(String userId) {
        return userRepository.findByPublicId(userId)
                .orElseThrow(() -> new UnknownCallerException(userId));
    }

    private static void requireNotSuspended(User user) {
        if (user.isSuspended()) {
            throw new SuspendedAccountException();
        }
    }

    /**
     * 계정 응답을 만듭니다. 외형이 있으면 함께 싣습니다.
     *
     * <p>계정을 돌려주는 모든 경로가 이 메서드를 지납니다. 발급, 조회, 닉네임 변경, 검색 허용
     * 변경 중 하나에서만 외형이 빠지면 클라이언트는 그 응답으로 화면을 다시 그리다가 옷장이
     * 기본값으로 튀는 것을 봅니다.
     */
    private AccountResponse respond(User user) {
        UserAppearance appearance = userAppearanceRepository.findById(user.getSeq()).orElse(null);
        // 계정을 돌려주는 모든 자리에 토큰이 함께 갑니다. 발급에서만 주면 앱을 껐다 켠
        // 클라이언트가 /me 로 계정을 읽은 뒤 토큰 없이 Photon 에 붙습니다.
        return AccountResponse.from(user, appearance, accountTokens.issue(user.getPublicId()));
    }
}
