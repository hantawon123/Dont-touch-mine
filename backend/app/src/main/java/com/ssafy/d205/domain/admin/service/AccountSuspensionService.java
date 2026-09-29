package com.ssafy.d205.domain.admin.service;

import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import com.ssafy.d205.domain.admin.entity.SuspensionAudit;
import com.ssafy.d205.domain.admin.repository.SuspensionAuditRepository;
import com.ssafy.d205.domain.user.entity.User;
import com.ssafy.d205.domain.user.repository.UserRepository;
import com.ssafy.d205.global.common.TimeProvider;
import com.ssafy.d205.global.exception.TargetUserNotFoundException;

/**
 * 운영자가 계정을 정지하고 해제합니다.
 *
 * <p>신고를 다루는 {@code ReportReviewService} 와 나눈 이유는 대상이 다르기 때문입니다.
 * 그쪽은 신고 기록을, 이쪽은 계정을 건드립니다. 신고를 완전 삭제해도 정지는 남아야 하는데
 * 한 서비스에 두면 그 경계가 흐려집니다.
 *
 * <p>정지는 신고와 독립입니다. 신고 없이도 정지할 수 있고, 신고를 많이 받아도 자동으로
 * 정지되지 않습니다. 사람이 보고 누릅니다.
 *
 * <p><b>누를 때마다 감사 로그 한 행이 남습니다</b>(S15P21D205-974). users 는 지금 상태만
 * 들고 있어서 해제하면 정지했던 사실이 사라집니다. 계정을 막는 일은 나중에 반드시 물어보는
 * 사람이 생기므로, 상태와 별개로 기록을 남깁니다. 기록은 같은 트랜잭션에서 저장되므로
 * "막혔는데 기록이 없다"는 상태가 생기지 않습니다.
 */
@Service
@Slf4j
@RequiredArgsConstructor
public class AccountSuspensionService {

    private final UserRepository userRepository;
    private final SuspensionAuditRepository suspensionAuditRepository;
    private final TimeProvider timeProvider;

    /**
     * 계정을 정지합니다. <b>멱등합니다.</b> 이미 정지된 계정이면 사유와 시각이 갱신됩니다.
     *
     * <p>이미 정지된 계정에 다시 눌러도 감사 로그는 남습니다. 사유를 고쳐 적는 것도 운영
     * 행위이고, 남기지 않으면 사유가 언제 왜 바뀌었는지 추적이 끊깁니다.
     *
     * @param adminUsername 누른 운영자의 계정 이름. 컨트롤러가 관리자 세션에서 읽어 넘깁니다
     * @return 이번 요청이 새로 정지했으면 true, 이미 정지 상태였으면 false
     */
    @Transactional
    public boolean suspend(String userId, String reason, String adminUsername) {
        User user = target(userId);
        boolean wasActive = !user.isSuspended();
        String now = timeProvider.now();

        user.suspend(reason, now);
        suspensionAuditRepository.save(SuspensionAudit.suspended(user, reason, adminUsername, now));

        // 로그도 그대로 둡니다. 감사 테이블이 생겼지만 DB 가 열리지 않는 상황에서 볼 수 있는
        // 것은 로그뿐이고, 둘은 서로의 대조군이기도 합니다.
        // userId 는 공개 식별자라 로그에 남겨도 새로 드러나는 것이 없습니다.
        log.info("계정을 정지했습니다. userId={} 새로정지={} 처리자={} 사유={}",
                userId, wasActive, adminUsername, reason);
        return wasActive;
    }

    /**
     * 정지를 해제합니다. <b>멱등합니다.</b> 정지 상태가 아니어도 200 입니다.
     *
     * <p>없는 계정이 아니라면 실패할 이유가 없습니다. 두 운영자가 같은 화면을 보다가 둘 다
     * 눌렀을 때 뒤에 누른 쪽에게 오류를 주면 무엇이 잘못됐는지 알 수 없는데, 원하는 결과는
     * 이미 이루어져 있습니다. 신고 숨김이 없는 번호에 200 을 주는 것과 같은 이유입니다.
     *
     * <p>정지 상태가 아니었어도 감사 로그는 남습니다. 운영자가 실제로 누른 일이기 때문입니다.
     *
     * @param adminUsername 누른 운영자의 계정 이름
     * @return 이번 요청이 실제로 해제했으면 true, 이미 정상이었으면 false
     */
    @Transactional
    public boolean lift(String userId, String adminUsername) {
        User user = target(userId);
        boolean wasSuspended = user.isSuspended();
        String now = timeProvider.now();

        user.lift();
        suspensionAuditRepository.save(SuspensionAudit.lifted(user, adminUsername, now));

        log.info("계정 정지를 해제했습니다. userId={} 실제해제={} 처리자={}",
                userId, wasSuspended, adminUsername);
        return wasSuspended;
    }

    /**
     * 없는 계정은 TARGET_NOT_FOUND 입니다. ACCOUNT_NOT_FOUND 가 아닙니다 - 그쪽은 부르는
     * 사람이 없다는 뜻이고, 여기서 부르는 사람은 로그인한 운영자입니다.
     */
    private User target(String userId) {
        return userRepository.findByPublicId(userId)
                .orElseThrow(() -> new TargetUserNotFoundException(userId));
    }
}
