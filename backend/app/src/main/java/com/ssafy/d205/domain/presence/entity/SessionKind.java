package com.ssafy.d205.domain.presence.entity;

/**
 * 지금 있는 Photon 룸이 어떤 룸인가.
 *
 * <p><b>로비와 경기는 같은 룸입니다.</b> 사람들이 로비에 모여 있다가 방장이 시작하면 그 룸이
 * 경기 씬으로 넘어갑니다. 룸이 바뀌지 않으므로 sessionId 는 그대로이고, 서버가 sessionId 만
 * 보고 둘을 가려낼 방법이 없습니다. 그래서 클라이언트가 어느 쪽인지 함께 보고합니다.
 *
 * <p>이것이 접속 상태를 결정하는 두 번째 값입니다. 첫 번째는 sessionId 의 유무입니다 —
 * 없으면 룸 밖이라 ONLINE 이고, 그때 이 값은 아무 뜻이 없어 무시합니다.
 */
public enum SessionKind {

    /** 로비에서 사람을 기다리는 중. IN_LOBBY 가 됩니다. */
    LOBBY,

    /** 경기가 시작됐다. IN_GAME 이 됩니다. */
    MATCH
}
