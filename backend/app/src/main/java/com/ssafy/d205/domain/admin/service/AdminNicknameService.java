package com.ssafy.d205.domain.admin.service;

import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.concurrent.ThreadLocalRandom;

import com.ssafy.d205.domain.admin.dto.AdminRenameResult;
import com.ssafy.d205.domain.admin.entity.NicknameAudit;
import com.ssafy.d205.domain.admin.repository.NicknameAuditRepository;
import com.ssafy.d205.domain.user.entity.NicknameBlocklist;
import com.ssafy.d205.domain.user.entity.NicknamePolicy;
import com.ssafy.d205.domain.user.entity.User;
import com.ssafy.d205.domain.user.repository.UserRepository;
import com.ssafy.d205.global.common.TimeProvider;
import com.ssafy.d205.global.exception.NicknameForbiddenException;
import com.ssafy.d205.global.exception.NicknameGenerationFailedException;
import com.ssafy.d205.global.exception.NicknameTakenException;
import com.ssafy.d205.global.exception.TargetUserNotFoundException;

/**
 * 운영자가 남의 닉네임을 바꿉니다 (S15P21D205-1047).
 *
 * <p>두 가지입니다. 이름을 직접 지정하는 것과, 부적절한 닉네임을 서버가 지은 이름으로 치우는
 * 것입니다. 후자가 이 기능이 생긴 이유입니다 - 신고를 보다 부적절한 이름을 발견했을 때 운영자가
 * 대체할 이름을 고민할 일이 아닙니다.
 *
 * <p>{@code AccountService.rename} 과 규칙을 나눠 갖습니다. 글자 규칙({@link NicknamePolicy})과
 * 금칙어({@link NicknameBlocklist})와 중복(uk_users_nickname)은 같습니다. 다른 것은 둘입니다 -
 * 여기는 남의 계정을 바꾸므로 감사 행을 남기고, 닉네임 변경권을 다르게 다룹니다
 * ({@link User#renameByAdmin}).
 *
 * <p>정지를 맡은 {@link AccountSuspensionService} 와 나눈 이유는 대상이 다르기 때문입니다.
 * 정지는 계정을 막고 이쪽은 이름만 바꿉니다. 정지를 풀어도 바뀐 이름은 그대로입니다.
 */
@Service
@Slf4j
@RequiredArgsConstructor
public class AdminNicknameService {

    /** 부적절한 닉네임을 치울 때 붙이는 이름. 뒤에 숫자가 붙습니다. */
    static final String FORCED_PREFIX = "부적절한닉네임";

    /**
     * 부적절한 닉네임 처리의 감사 사유. 운영자에게 따로 묻지 않습니다 - 치운 이름이
     * {@code before_nickname} 에 남으므로 판단의 근거는 기록 자체에 있습니다.
     */
    static final String FORCED_REASON = "부적절한 닉네임";

    /** 숫자를 다시 뽑는 횟수. {@code AccountService.MAX_ATTEMPTS} 와 같은 뜻입니다. */
    private static final int MAX_ATTEMPTS = 5;

    /** 처음 붙이는 숫자의 자릿수. "부적절한닉네임123" 의 세 자리입니다. */
    private static final int MIN_DIGITS = 3;

    /** 접두사가 7자라 12자 한도에서 숫자에 쓸 수 있는 최대 자릿수입니다. */
    private static final int MAX_DIGITS = NicknamePolicy.MAX_LENGTH - FORCED_PREFIX.length();

    private final UserRepository userRepository;
    private final NicknameAuditRepository nicknameAuditRepository;
    private final NicknameBlocklist nicknameBlocklist;
    private final TimeProvider timeProvider;

    /**
     * 운영자가 지정한 이름으로 바꿉니다. <b>멱등합니다.</b>
     *
     * <p>지금과 같은 이름이면 아무것도 하지 않고 {@code changed=false} 입니다. 감사 행도 남기지
     * 않습니다. 정지가 같은 요청에도 행을 남기는 것과 다른데, 그쪽은 사유를 고쳐 적는 것이 운영
     * 행위인 반면 여기서는 바뀐 것이 없습니다.
     *
     * <p>글자 규칙은 요청 DTO 가 먼저 보고, 금칙어는 여기서 봅니다. 금칙어를 중복보다 먼저 보는
     * 이유는 {@code AccountService.rename} 과 같습니다.
     *
     * <p>닉네임 변경권은 건드리지 않습니다. 운영자가 정해 준 이름을 본인이 또 바꿀 수 있게 풀어
     * 주면, 이름을 지정한 뜻이 사라집니다. 본인에게 다시 정하게 하려면 부적절한 닉네임 처리를
     * 쓰면 됩니다.
     *
     * @param adminUsername 누른 운영자의 계정 이름. 컨트롤러가 관리자 세션에서 읽어 넘깁니다
     */
    @Transactional
    public AdminRenameResult rename(String userId, String nickname, String reason, String adminUsername) {
        User user = target(userId);

        if (user.getNickname().equals(nickname)) {
            return new AdminRenameResult(false, nickname);
        }

        if (nicknameBlocklist.isForbidden(nickname)) {
            throw new NicknameForbiddenException();
        }

        // 미리 조회해 확인하지만 그것만으로 동시 요청을 막지는 못합니다. 실제로 막는 것은
        // uk_users_nickname 이고, 제약에 걸린 예외는 핸들러가 409 로 옮깁니다.
        if (userRepository.existsByNickname(nickname)) {
            throw new NicknameTakenException(nickname);
        }

        apply(user, nickname, reason, adminUsername);
        return new AdminRenameResult(true, nickname);
    }

    /**
     * 부적절한 닉네임을 치웁니다. 서버가 "부적절한닉네임123" 꼴의 이름을 지어 붙입니다.
     *
     * <p><b>멱등하지 않습니다.</b> 누를 때마다 다른 번호가 나옵니다. 그래서 컨트롤러가 PUT 이
     * 아니라 POST 입니다.
     *
     * <p>금칙어 검사를 지나지 않습니다. 서버가 지은 이름이라 {@code NicknameGenerator} 와 같은
     * 취급이고, 접두사가 목록에 걸리지 않는 것은 테스트가 지킵니다.
     *
     * <p><b>닉네임 변경권을 돌려줍니다.</b> 이름을 치우는 것이 목적이고, 그 사람이 멀쩡한 이름을
     * 한 번 정하게 하는 것이 그다음입니다. 치운 이름을 평생 달고 있게 하려면 이 줄을 지우는 것이
     * 아니라 그런 결정을 따로 해야 합니다.
     */
    @Transactional
    public AdminRenameResult reset(String userId, String adminUsername) {
        User user = target(userId);
        String nickname = forcedNickname();

        apply(user, nickname, FORCED_REASON, adminUsername);
        user.reopenNicknameChange();

        return new AdminRenameResult(true, nickname);
    }

    /**
     * 감사 행을 남기고 이름을 바꿉니다.
     *
     * <p>순서가 중요합니다. 감사 행이 바꾸기 전 이름을 엔티티에서 읽으므로, 먼저 바꾸면 before 와
     * after 가 같아집니다. 같은 트랜잭션이라 "이름은 바뀌었는데 기록이 없다"는 상태는 생기지
     * 않습니다.
     */
    private void apply(User user, String nickname, String reason, String adminUsername) {
        String now = timeProvider.now();
        String before = user.getNickname();

        nicknameAuditRepository.save(NicknameAudit.of(user, nickname, reason, adminUsername, now));
        user.renameByAdmin(nickname, now);

        // 감사 테이블이 있지만 로그도 남깁니다. DB 가 열리지 않는 상황에서 볼 수 있는 것은
        // 로그뿐이고, 둘은 서로의 대조군이기도 합니다(AccountSuspensionService 와 같습니다).
        log.info("운영자가 닉네임을 바꿨습니다. userId={} 전={} 후={} 처리자={} 사유={}",
                user.getPublicId(), before, nickname, adminUsername, reason);
    }

    /**
     * 쓰이지 않는 "부적절한닉네임NNN" 을 만듭니다.
     *
     * <p>세 자리로 시작해 충돌할 때마다 한 자리를 늘립니다. 세 자리는 900 가지뿐이라 이 기능을
     * 오래 쓰면 빈 번호를 못 찾는 날이 오는데, 자릿수를 늘리면 90000 가지가 됩니다. 처음부터 다섯
     * 자리로 뽑지 않는 이유는 짧은 이름이 화면에서 읽기 쉽고, 길어지는 것은 실제로 필요할 때만
     * 필요하기 때문입니다.
     *
     * <p>미리 조회해 확인하는 것만으로는 동시 요청을 막지 못합니다. 실제로 막는 것은
     * uk_users_nickname 이고, 그 경쟁에서 진 요청은 409 CONFLICT 가 됩니다.
     */
    private String forcedNickname() {
        for (int attempt = 0; attempt < MAX_ATTEMPTS; attempt++) {
            String candidate = FORCED_PREFIX + number(Math.min(MIN_DIGITS + attempt, MAX_DIGITS));
            if (!userRepository.existsByNickname(candidate)) {
                return candidate;
            }
        }
        throw new NicknameGenerationFailedException(MAX_ATTEMPTS);
    }

    /** 자릿수가 정해진 무작위 수. 맨 앞자리가 0 이 되지 않게 아래 경계를 둡니다. */
    private static String number(int digits) {
        int bound = (int) Math.pow(10, digits);
        return String.valueOf(ThreadLocalRandom.current().nextInt(bound / 10, bound));
    }

    /**
     * 없는 계정은 TARGET_NOT_FOUND 입니다. 이유는 {@link AccountSuspensionService} 와 같습니다 -
     * ACCOUNT_NOT_FOUND 는 부르는 사람이 없다는 뜻이고, 여기서 부르는 사람은 운영자입니다.
     */
    private User target(String userId) {
        return userRepository.findByPublicId(userId)
                .orElseThrow(() -> new TargetUserNotFoundException(userId));
    }
}
