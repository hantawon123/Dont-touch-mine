package com.ssafy.d205.domain.presence.entity;

/**
 * 접속 상태.
 *
 * <p>클라이언트의 FriendPresence enum(Offline / Online / InGame ...)에 대응합니다. 표기가
 * 다른 것은 의도된 것입니다. 이 API는 FriendshipStatus 도 PENDING / ACCEPTED 로
 * 내보내고 있어서, 여기서 표기를 바꾸면 우리 API 안에서 규칙이 갈립니다.
 *
 * <p>ONLINE 은 앱을 켰지만 Photon 룸 밖이라는 뜻입니다. <b>이것은 Fusion 이 알 수 없는
 * 상태입니다</b> — Photon 세션 밖이니까요. 그래서 접속 상태를 이 서버가 들고 있어야 합니다.
 *
 * <p>IN_LOBBY 와 IN_GAME 은 둘 다 룸 안입니다. 나누는 이유는 친구 목록에서 "지금 부르면
 * 올 수 있는 사람"과 "경기가 끝나야 오는 사람"이 달라 보여야 하기 때문입니다. 다만 룸은
 * 하나이고 로비에서 경기로 넘어가도 룸이 바뀌지 않으므로, 서버는 sessionId 로 둘을
 * 유도하지 못하고 클라이언트가 {@link SessionKind} 로 알려줍니다.
 *
 * <p><b>둘을 나눠도 방 초대는 둘 다 막습니다.</b> 초대 토스트는 홈과 게임 찾기에서만
 * 뜨므로 로비에 있는 사람은 받아도 보지 못합니다. 나눈 것은 표시를 위한 것입니다.
 */
public enum PresenceStatus {
    OFFLINE,
    ONLINE,
    IN_LOBBY,
    IN_GAME
}
