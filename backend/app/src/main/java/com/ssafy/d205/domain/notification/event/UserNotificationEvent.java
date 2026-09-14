package com.ssafy.d205.domain.notification.event;

import com.ssafy.d205.domain.user.entity.User;

/**
 * 한 사람에게 보낼 알림 하나. 도메인 서비스가 트랜잭션 안에서 발행하고, 발송은 커밋 뒤에
 * NotificationPublisher 가 합니다.
 *
 * <p>서비스가 WebSocket 을 직접 부르지 않는 이유는 둘입니다. 친구·초대 도메인이 전달 수단을
 * 몰라야 나중에 수단이 바뀌어도 이 자리만 남고, 커밋 전에 보내면 상대가 알림을 받고 목록을
 * 다시 읽었을 때 행이 아직 없는 창이 생깁니다.
 *
 * <p>엔티티가 아니라 값만 담습니다. 커밋 뒤에 듣는 쪽은 영속성 컨텍스트가 닫힌 뒤라 지연
 * 로딩이 되지 않습니다.
 *
 * @param targetSeq    받는 사람의 users_seq
 * @param type         알림 종류
 * @param fromUserId   일으킨 사람의 public_id
 * @param fromNickname 일으킨 사람의 닉네임. 토스트 문구에 바로 쓴다
 * @param roomCode     방 초대일 때만 값이 있다. 나머지는 null
 */
public record UserNotificationEvent(
        Integer targetSeq,
        NotificationType type,
        String fromUserId,
        String fromNickname,
        String roomCode
) {

    public static UserNotificationEvent friendRequestReceived(User target, User from) {
        return of(target, NotificationType.FRIEND_REQUEST_RECEIVED, from, null);
    }

    public static UserNotificationEvent friendRequestAccepted(User requester, User acceptedBy) {
        return of(requester, NotificationType.FRIEND_REQUEST_ACCEPTED, acceptedBy, null);
    }

    public static UserNotificationEvent friendRequestRemoved(User target, User from) {
        return of(target, NotificationType.FRIEND_REQUEST_REMOVED, from, null);
    }

    public static UserNotificationEvent friendRemoved(User target, User from) {
        return of(target, NotificationType.FRIEND_REMOVED, from, null);
    }

    public static UserNotificationEvent roomInviteReceived(User invitee, User inviter, String roomCode) {
        return of(invitee, NotificationType.ROOM_INVITE_RECEIVED, inviter, roomCode);
    }

    private static UserNotificationEvent of(User target, NotificationType type, User from, String roomCode) {
        return new UserNotificationEvent(target.getSeq(), type, from.getPublicId(), from.getNickname(), roomCode);
    }
}
