package com.ssafy.d205.domain.invite.entity;

/**
 * 방 코드의 모양.
 *
 * <p>클라이언트의 {@code RoomCodeFormat} 을 그대로 옮긴 것입니다. 6자이고, 알파벳은
 * 0-9A-Z 에서 <b>I, O, U, L 을 뺀 32자</b>입니다. I 와 1, O 와 0 은 눈으로 구분하기
 * 어렵고 코드는 사람이 읽어 옮기는 값이라 아예 넣지 않습니다.
 *
 * <p>서버가 이 규칙을 다시 확인하는 이유는 값이 DB 에 들어가기 때문입니다. 컬럼이
 * CHAR(6) 이라 길이는 스키마가 막지만, 소문자나 엉뚱한 글자가 담기는 것은 막지
 * 못합니다. 클라이언트가 만든 값이라도 서버가 받는 것은 그냥 문자열입니다.
 *
 * <p>규칙이 두 벌로 갈라지면 클라이언트가 만든 코드를 서버가 거절하는 상태가 됩니다.
 * 알파벳을 바꿀 일이 생기면 양쪽을 함께 고쳐야 합니다.
 */
public final class RoomCodePolicy {

    public static final int LENGTH = 6;

    /** DTO 애너테이션에 그대로 넣기 위해 문자열 상수로도 둡니다. */
    public static final String REGEX = "^[0-9A-HJKMNPQRSTVWXYZ]{6}$";

    private RoomCodePolicy() {
    }
}
