package com.ssafy.d205.domain.notification.event;

/**
 * WebSocket 으로 클라이언트에 밀어주는 알림의 종류.
 *
 * <p>이름이 그대로 프레임의 {@code type} 값으로 나갑니다. 클라이언트는 이 문자열로 분기하므로
 * 이름을 바꾸면 클라이언트도 함께 바꿔야 하고, 값을 더하면 docs/client-guide.md 의 표에도
 * 넣어야 합니다. ClientGuideTest 가 그것을 확인합니다.
 *
 * <p>알림은 <b>신호</b>입니다. 페이로드에는 토스트를 그릴 만큼만 담고, 목록의 진실은 여전히
 * REST 조회입니다. 그래서 종류마다 "받으면 무엇을 다시 읽어야 하는가"가 정해져 있습니다.
 */
public enum NotificationType {

    /** 누가 나에게 친구 요청을 보냈다. 받은 요청 목록을 다시 읽는다. */
    FRIEND_REQUEST_RECEIVED,

    /** 내가 보낸 친구 요청을 상대가 수락했다. 친구 목록과 보낸 요청 목록을 다시 읽는다. */
    FRIEND_REQUEST_ACCEPTED,

    /**
     * 나와 상대 사이의 대기 중 요청이 사라졌다. 상대가 거절했거나 취소한 경우 모두입니다.
     * 어느 쪽인지는 구분하지 않습니다. 화면이 할 일은 같고, 거절을 알려주면 거절한 사람이
     * 드러납니다. 받은·보낸 요청 목록을 다시 읽는다.
     */
    FRIEND_REQUEST_REMOVED,

    /**
     * 친구 관계가 끊겼다. 상대가 끊은 경우입니다. 두 사람 사이의 방 초대도 함께 사라지므로
     * 친구 목록과 받은 초대 목록을 다시 읽는다.
     */
    FRIEND_REMOVED,

    /** 친구가 나를 방으로 불렀다. 같은 방으로 다시 부른 갱신도 여기에 해당한다. */
    ROOM_INVITE_RECEIVED
}
