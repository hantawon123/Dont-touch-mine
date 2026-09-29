package com.ssafy.d205.global.exception;

/**
 * 상대가 로비나 경기 중이라 방으로 부를 수 없습니다.
 *
 * <p>서버는 로비와 경기 중을 구분하지 않습니다. Photon 세션 안에 있으면 둘 다 IN_GAME 이고,
 * 어느 쪽이든 초대 토스트가 뜨지 않는 곳이라 보내도 상대가 볼 수 없습니다.
 */
public class TargetInGameException extends RuntimeException {

    public TargetInGameException() {
        super("게임 중인 친구에게는 초대를 보낼 수 없습니다.");
    }
}
