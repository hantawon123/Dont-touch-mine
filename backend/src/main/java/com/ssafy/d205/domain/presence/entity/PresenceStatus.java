package com.ssafy.d205.domain.presence.entity;

/**
 * 접속 상태.
 *
 * <p>클라이언트의 FriendPresence enum(Offline / Online / InGame)에 대응합니다. 표기가
 * 다른 것은 의도된 것입니다. 이 API는 FriendshipStatus 도 PENDING / ACCEPTED 로
 * 내보내고 있어서, 여기서 표기를 바꾸면 우리 API 안에서 규칙이 갈립니다. 값이 셋뿐이라
 * 클라이언트가 매핑하는 비용이 작습니다.
 *
 * <p>IN_GAME 은 Photon 세션 안에 있다는 뜻이고, ONLINE 은 앱을 켰지만 세션 밖이라는
 * 뜻입니다. <b>후자는 Fusion 이 알 수 없는 상태입니다</b> — Photon 세션 밖이니까요.
 * 그래서 접속 상태를 이 서버가 들고 있어야 합니다.
 */
public enum PresenceStatus {
    OFFLINE,
    ONLINE,
    IN_GAME
}
