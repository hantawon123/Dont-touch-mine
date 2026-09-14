package com.ssafy.d205.domain.user.event;

/**
 * 계정이 지워졌습니다. 삭제 트랜잭션 안에서 발행되고, 듣는 쪽은 커밋 뒤에 받습니다.
 *
 * <p>플레이 로그(분석 스키마)에서 이 사람의 식별자를 지우는 데 씁니다. AccountService 가 분석
 * 코드를 직접 부르지 않고 이벤트로 알리는 이유는 둘입니다. 사용자 도메인이 분석 도메인을 몰라야
 * 나중에 수집을 별도 서비스로 뺄 때 이 자리만 바꾸면 되고, 분석 DB 는 트랜잭션 매니저가 달라
 * 게임 트랜잭션 안에서 부르면 실패가 탈퇴를 롤백시킬 수 있습니다.
 *
 * @param publicId 지워진 계정의 users.public_id. seq 는 이미 사라졌고 로그에 남은 것도 이 값입니다
 */
public record AccountDeletedEvent(String publicId) {
}
