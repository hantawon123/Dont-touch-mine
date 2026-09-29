package com.ssafy.d205.domain.ops.service;

import org.springframework.stereotype.Component;
import org.springframework.transaction.event.TransactionPhase;
import org.springframework.transaction.event.TransactionalEventListener;

import java.util.concurrent.atomic.AtomicInteger;

import com.ssafy.d205.domain.user.event.AccountDeletedEvent;

/**
 * 직전 샘플 이후 탈퇴 수를 셉니다.
 *
 * <p>탈퇴는 users 행이 지워지므로 나중에 셀 수 없습니다. 유일한 신호가 커밋 뒤 이벤트라
 * 그것을 받아 메모리에 더해 두고, {@link OpsSampler} 가 1분마다 읽으면서 0 으로 돌립니다.
 *
 * <p>앱이 재시작하면 그 사이 값은 사라집니다. 운영 지표라 그 정도는 받아들이고, 정확한 탈퇴
 * 기록이 필요해지면 그때 감사 테이블을 둡니다. 여기서 DB 에 쓰지 않는 이유는 탈퇴 요청의
 * 트랜잭션 바깥에서 실패해도 탈퇴 자체에는 영향을 주지 않게 하려는 것입니다.
 */
@Component
public class AccountDeletionCounter {

    private final AtomicInteger sinceLastSample = new AtomicInteger();

    @TransactionalEventListener(phase = TransactionPhase.AFTER_COMMIT)
    public void onAccountDeleted(AccountDeletedEvent event) {
        sinceLastSample.incrementAndGet();
    }

    /** 지금까지 쌓인 수를 돌려주고 0 으로 돌립니다. 샘플러만 부릅니다. */
    int drain() {
        return sinceLastSample.getAndSet(0);
    }

    /** 아직 샘플에 실리지 않은 수. 개요 탭의 "오늘 탈퇴"가 샘플 합계에 더합니다. */
    public int pending() {
        return sinceLastSample.get();
    }
}
