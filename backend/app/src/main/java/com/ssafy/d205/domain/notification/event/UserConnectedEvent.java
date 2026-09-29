package com.ssafy.d205.domain.notification.event;

/**
 * 어떤 사람의 알림 연결이 열렸습니다. HELLO 로 사람에 묶이는 순간 레지스트리가 발행합니다.
 *
 * <p><b>연결이 살아 있다는 것 자체가 접속 신호입니다.</b> 그래서 프레즌스가 이 이벤트를
 * 듣습니다. 예전에는 클라이언트가 30초마다 {@code PUT /api/v1/presence} 를 보내 같은
 * 사실을 알렸고, 접속자 수만큼 요청과 트랜잭션이 생겼습니다.
 *
 * <p>레지스트리가 프레즌스 서비스를 직접 부르지 않는 이유는, 그 클래스가 <b>DB 를 모르는
 * 채로 남아야</b> 하기 때문입니다. 나중에 인스턴스를 늘려 Redis pub/sub 으로 바꿀 때
 * 손대는 곳이 레지스트리 하나이도록 발송·연결 관리 코드만 그 안에 둡니다.
 *
 * <p>같은 사람이 두 번째 연결을 열어도 발행됩니다. 듣는 쪽이 이미 룸 안인 사람을 밖으로
 * 끌어내지 않도록 처리합니다.
 *
 * @param userSeq 붙은 사람의 users_seq
 */
public record UserConnectedEvent(Integer userSeq) {
}
