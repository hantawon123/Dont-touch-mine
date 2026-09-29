package com.ssafy.d205.domain.analytics.exception;

/**
 * 이벤트 배치가 형식은 맞지만 내용 규칙을 어겼습니다. 목록에 없는 이름, 범위 밖 시각, 너무 큰
 * params 가 여기 옵니다. 핸들러가 400 INVALID_REQUEST 로 옮기고, 메시지가 어느 이벤트의 무엇이
 * 틀렸는지 말합니다.
 *
 * <p>배치 단위입니다. 하나가 틀리면 전부 거부합니다. 일부만 받으면 클라이언트는 무엇이
 * 들어갔는지 알 수 없어 재전송 판단을 못 합니다.
 */
public class EventBatchRejectedException extends RuntimeException {

    public EventBatchRejectedException(int index, String reason) {
        super("events[" + index + "]: " + reason);
    }
}
