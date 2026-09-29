package com.ssafy.d205.domain.invite.service;

import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Component;
import org.springframework.transaction.annotation.Transactional;

import com.ssafy.d205.domain.invite.entity.InviteExpiry;
import com.ssafy.d205.domain.invite.repository.RoomInviteRepository;
import com.ssafy.d205.global.common.TimeProvider;

/**
 * 만료된 초대를 지웁니다.
 *
 * <p><b>이것이 없어도 API 응답은 정확합니다.</b> 조회할 때 만료된 행을 걸러내기
 * 때문입니다. 스윕이 하는 일은 테이블에서 죽은 행을 치우는 것뿐입니다.
 *
 * <p>그래도 두는 이유는 이 테이블이 계속 자란다는 점입니다. 접속 상태는 유저당 한 행이라
 * 스윕이 없어도 크기가 정해져 있지만, 초대는 부를 때마다 행이 생깁니다. 치우지 않으면
 * 아무도 읽지 않는 행이 쌓이고, 조회가 훑어야 할 양도 함께 늘어납니다.
 *
 * <p>접속 상태 스윕과 달리 값을 바꾸지 않고 행을 지웁니다. 만료된 초대에는 남겨서
 * 읽을 것이 없습니다.
 *
 * <p><b>앱 인스턴스가 하나일 때만 안전합니다.</b> 여러 개로 늘리면 같은 스윕이 동시에
 * 돌면서 서로의 DELETE 와 겹칩니다. 지우는 연산이라 데이터가 깨지지는 않지만 불필요한
 * 잠금 경합이 생깁니다. PresenceSweeper 와 같은 조건입니다.
 */
@Component
@RequiredArgsConstructor
@Slf4j
public class InviteSweeper {

    private final RoomInviteRepository roomInviteRepository;
    private final TimeProvider timeProvider;

    /**
     * 기본값 30초. 접속 상태 스윕과 같은 주기입니다.
     *
     * <p>fixedRate 가 아니라 fixedDelay 입니다. 이전 실행이 끝난 뒤 간격을 세므로
     * 실행이 오래 걸릴 때 다음 실행이 겹쳐 들어오지 않습니다.
     *
     * <p>간격을 설정으로 뺀 이유는 테스트입니다. 스케줄러가 테스트 중에 돌면 초대를
     * 과거로 밀어 만료를 재현하는 테스트와 경합해 간헐적으로 실패합니다.
     * application-test.yml 이 한 시간으로 늘려 사실상 끕니다.
     */
    @Scheduled(fixedDelayString = "${invite.sweep-interval-ms:30000}")
    @Transactional
    public void sweep() {
        int swept = roomInviteRepository.deleteExpired(timeProvider.minus(InviteExpiry.LIFETIME));
        if (swept > 0) {
            log.info("만료된 초대 {}건을 지웠습니다.", swept);
        }
    }
}
