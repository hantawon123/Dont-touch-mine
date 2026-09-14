package com.ssafy.d205.domain.ops.service;

import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Component;
import org.springframework.transaction.event.TransactionPhase;
import org.springframework.transaction.event.TransactionalEventListener;

import java.util.Set;
import java.util.concurrent.ConcurrentHashMap;

import com.ssafy.d205.domain.user.event.AccountDeletedEvent;

/**
 * 탈퇴가 커밋되면 분석 서비스에 그 사람의 로그 익명화를 요청하고, 실패하면 기억해 두고 다시 보냅니다
 * (S15P21D205-1002).
 *
 * <p>커밋 <b>뒤</b>에 부릅니다. 트랜잭션 안에서 부르면 분석 서비스가 느릴 때 탈퇴 응답이 함께 늦고,
 * 분석 호출이 성공한 뒤 커밋이 실패하면 살아 있는 계정의 로그를 지운 셈이 됩니다. 커밋 뒤라 이 호출이
 * 실패해도 탈퇴는 이미 성공한 상태이고, 익명화만 늦어집니다.
 *
 * <p>재시도 목록은 메모리입니다. 앱이 재시작하면 그 사이 실패분은 잃습니다. 서비스가 나뉘기 전에도 같은
 * 프로세스에서 best-effort 로 하던 일이라 약속의 수준은 같습니다. 분석 서비스가 몇 시간 죽어 있는 동안
 * 탈퇴한 사람이 있고 그 사이 계정 서비스도 재시작했다면 그 사람의 userId 가 로그에 남습니다. 그 경우를
 * 없애려면 계정 DB 에 대기 테이블이 필요한데, 탈퇴 빈도에 비해 과합니다. 필요해지면 그때 둡니다.
 *
 * <p>같은 이벤트를 세는 {@link AccountDeletionCounter} 와 별개입니다. 그쪽은 숫자를 세고 이쪽은 요청을 보냅니다.
 */
@Component
@RequiredArgsConstructor
@Slf4j
public class AnalyticsErasureRelay {

    private final AnalyticsInternalClient analytics;

    /** 아직 분석 서비스가 받아주지 않은 userId. 집합이라 같은 사람이 두 번 들어가지 않습니다. */
    private final Set<String> pending = ConcurrentHashMap.newKeySet();

    @TransactionalEventListener(phase = TransactionPhase.AFTER_COMMIT)
    public void onAccountDeleted(AccountDeletedEvent event) {
        if (!analytics.eraseUserEvents(event.publicId())) {
            pending.add(event.publicId());
        }
    }

    /** 실패했던 요청을 다시 보냅니다. 성공한 것만 목록에서 뺍니다. */
    @Scheduled(fixedDelayString = "${analytics.erase-retry-ms:60000}", initialDelayString = "${analytics.erase-retry-ms:60000}")
    public int retry() {
        int delivered = 0;
        for (String userId : pending) {
            if (analytics.eraseUserEvents(userId)) {
                pending.remove(userId);
                delivered++;
            }
        }
        if (delivered > 0) {
            log.info("미뤄 둔 탈퇴 로그 익명화 {}건을 분석 서비스에 전달했습니다. 남은 것 {}건.", delivered, pending.size());
        }
        return delivered;
    }

    /** 아직 전달하지 못한 수. 서버 탭과 테스트가 봅니다. */
    public int pendingCount() {
        return pending.size();
    }
}
