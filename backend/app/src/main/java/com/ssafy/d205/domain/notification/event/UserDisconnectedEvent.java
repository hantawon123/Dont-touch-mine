package com.ssafy.d205.domain.notification.event;

/**
 * 어떤 사람의 <b>마지막</b> 알림 연결이 끊겼습니다.
 *
 * <p>마지막일 때만 발행합니다. 탭을 둘 열어 둔 사람이 하나를 닫은 것은 접속이 끊긴 것이
 * 아니고, 그때 오프라인으로 내리면 남은 탭에서 자기가 오프라인으로 보입니다.
 *
 * <p>끊김을 알아내는 경로가 셋입니다 — 정상 종료(afterConnectionClosed), 알림을 보내다
 * 실패한 죽은 연결, ping 에 응답하지 않는 연결. 셋 다 레지스트리의 unbind 를 지나므로
 * 이벤트도 그 한 곳에서 나갑니다. 세 경로가 각자 프레즌스를 부르면 한 곳이 빠지는 날이
 * 오고, 그러면 그 사람만 타임아웃(90초)까지 온라인으로 남습니다.
 *
 * @param userSeq 끊긴 사람의 users_seq
 */
public record UserDisconnectedEvent(Integer userSeq) {
}
