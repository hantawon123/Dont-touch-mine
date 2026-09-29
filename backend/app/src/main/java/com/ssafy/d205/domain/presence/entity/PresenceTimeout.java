package com.ssafy.d205.domain.presence.entity;

import java.time.Duration;

/**
 * 하트비트가 끊긴 것을 오프라인으로 판정하는 규칙.
 *
 * <p>클라이언트가 크래시로 죽으면 종료 신호가 오지 않습니다. 저장된 status 는 계속
 * ONLINE 이지만 실제로는 접속이 끊긴 상태입니다. 마지막 하트비트가 얼마나 오래됐는지로
 * 판정합니다.
 *
 * <p><b>규칙을 이 한 곳에만 둡니다.</b> 친구 목록 조회와 스윕이 같은 기준을 써야 하는데
 * 둘이 갈리면 목록에는 오프라인인데 DB 는 온라인인 상태가 생깁니다. S15P21D205-430 에서
 * 접속 상태를 범위에서 뺀 이유가 판정 로직이 두 곳에 생기는 것이었습니다.
 *
 * <p>이 클래스는 시계를 읽지 않습니다. 기준 시각은 호출부가 TimeProvider 로 한 번
 * 계산해서 넘깁니다. 여기서 읽으면 친구 목록의 행마다 기준이 미세하게 달라집니다.
 *
 * <p>90초는 하트비트 주기(30초)의 세 배입니다. 한 번 놓치는 것은 흔하고 두 번 연속
 * 놓치는 것은 드물다는 가정입니다. 너무 짧으면 잠깐 끊긴 사람이 오프라인으로 깜빡이고,
 * 너무 길면 나간 사람이 오래 온라인으로 남습니다.
 */
public final class PresenceTimeout {

    public static final Duration TIMEOUT = Duration.ofSeconds(90);

    private PresenceTimeout() {
    }

    /**
     * 저장된 값과 마지막 하트비트로 실제 상태를 계산합니다.
     *
     * <p>문자열을 그대로 받는 이유는 조회 투영이 문자열을 주기 때문입니다. 호출부마다
     * enum 으로 바꾸고 null 을 확인하는 코드가 생기는 것을 막습니다.
     *
     * @param storedStatus DB의 status. 접속 기록이 없으면 null 입니다.
     * @param heartbeatAt  마지막 하트비트. yyyyMMddHHmmss UTC.
     * @param thresholdAt  이 시각보다 오래된 하트비트는 끊긴 것으로 봅니다.
     *                     한 번의 조회 안에서는 같은 값을 써야 합니다.
     */
    public static PresenceStatus effective(String storedStatus, String heartbeatAt, String thresholdAt) {
        if (storedStatus == null || heartbeatAt == null) {
            return PresenceStatus.OFFLINE;
        }
        // 고정 길이 UTC 문자열이라 사전순 비교가 시간순 비교와 같습니다.
        if (heartbeatAt.compareTo(thresholdAt) < 0) {
            return PresenceStatus.OFFLINE;
        }
        return PresenceStatus.valueOf(storedStatus);
    }
}
