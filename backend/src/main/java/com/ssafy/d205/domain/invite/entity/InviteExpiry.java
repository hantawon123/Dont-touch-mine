package com.ssafy.d205.domain.invite.entity;

import java.time.Duration;

/**
 * 초대가 얼마나 살아있는지.
 *
 * <p>방 코드는 그 방이 사라지면 죽은 값입니다. 서버는 Photon 의 방 목록을 모르므로 방이
 * 없어진 것을 알 방법이 없고, 대신 초대 자체에 수명을 둡니다.
 *
 * <p><b>규칙을 이 한 곳에만 둡니다.</b> 조회와 스윕이 같은 기준을 써야 합니다. 둘이
 * 갈리면 목록에는 없는데 DB 에는 남아 있는 초대가 생깁니다.
 * {@code PresenceTimeout} 이 같은 이유로 같은 모양입니다.
 *
 * <p>3분은 로비에서 사람을 기다리는 시간과 경기가 끝난 뒤 방이 사라지는 시간 사이에서
 * 고른 값입니다. 짧으면 초대를 보고도 들어가기 전에 사라지고, 길면 이미 끝난 방의
 * 초대를 눌러 입장에 실패하는 일이 늘어납니다.
 */
public final class InviteExpiry {

    public static final Duration LIFETIME = Duration.ofMinutes(3);

    private InviteExpiry() {
    }
}
