package com.ssafy.d205.domain.analytics.internalapi;

import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

/**
 * 탈퇴한 사람의 플레이 로그에서 userId 를 지웁니다. 계정 서비스가 탈퇴 커밋 뒤에 부릅니다 (S15P21D205-1002).
 *
 * <p>서비스가 나뉘기 전에는 같은 프로세스 안의 AccountDeletedEvent 리스너가 이 일을 했습니다(871).
 * 이제 두 서비스가 이벤트를 공유할 수 없어 HTTP 로 받습니다. 약속은 같습니다. 게임 DB 의 "탈퇴는
 * 흔적을 남기지 않는다"를 분석 로그에도 적용해, 행은 남기고 사람만 지웁니다. 행을 지우면 경기
 * 통계가 그만큼 비고, userId 만 비우면 "누군가"의 위치 표본으로 남아 히트맵에는 그대로 쓰입니다.
 *
 * <p>멱등합니다. 두 번 불러도 두 번째는 0 행입니다. 계정 서비스가 실패로 보고 재시도해도 안전합니다.
 *
 * <p>user_public_id 선두 인덱스가 없어 풀 스캔입니다. 탈퇴는 드물어 지금은 두지 않습니다. 행이 수천만이
 * 되면 인덱스를 추가합니다.
 */
@RestController
@RequestMapping("/internal/users")
@RequiredArgsConstructor
@Slf4j
public class InternalUserEventsController {

    private static final String ERASE = "UPDATE game_event SET user_public_id = NULL WHERE user_public_id = ?";

    private final JdbcTemplate jdbcTemplate;

    @DeleteMapping("/{userId}/events")
    public EraseResult erase(@PathVariable String userId) {
        int erased = jdbcTemplate.update(ERASE, userId);
        log.info("탈퇴한 계정의 플레이 로그 {}행에서 userId 를 지웠습니다.", erased);
        return new EraseResult(erased);
    }

    /** @param erased 이번 호출로 userId 를 지운 행 수. 재시도면 0 일 수 있고 그것은 정상입니다. */
    public record EraseResult(int erased) {
    }
}
