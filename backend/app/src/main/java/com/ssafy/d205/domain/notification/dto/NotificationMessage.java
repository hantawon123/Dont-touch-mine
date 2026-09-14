package com.ssafy.d205.domain.notification.dto;

import com.ssafy.d205.domain.notification.event.UserNotificationEvent;

/**
 * WebSocket 으로 나가는 알림 프레임의 본문.
 *
 * <pre>
 * { "type": "ROOM_INVITE_RECEIVED", "sentAt": "20260908123000",
 *   "from": { "userId": "...", "nickname": "..." }, "roomCode": "7K2M9P" }
 * </pre>
 *
 * <p>{@code roomCode} 는 방 초대가 아니면 null 입니다. 종류마다 다른 클래스를 두지 않은 것은
 * 클라이언트가 JsonUtility 로 한 형태에 받아 {@code type} 으로 분기하기 때문입니다.
 *
 * @param type     NotificationType 의 이름
 * @param sentAt   서버가 보낸 시각. 다른 API 와 같은 yyyyMMddHHmmss UTC
 * @param from     알림을 일으킨 사람
 * @param roomCode 방 초대일 때 들어갈 방의 코드
 */
public record NotificationMessage(
        String type,
        String sentAt,
        Sender from,
        String roomCode
) {

    /** 알림을 일으킨 사람. 토스트에 닉네임을 쓰고, 수락·거절은 userId 로 부릅니다. */
    public record Sender(String userId, String nickname) {
    }

    public static NotificationMessage of(UserNotificationEvent event, String sentAt) {
        return new NotificationMessage(
                event.type().name(),
                sentAt,
                new Sender(event.fromUserId(), event.fromNickname()),
                event.roomCode());
    }
}
