package com.ssafy.d205.global.exception;

/**
 * X-User-Id 는 왔는데 그 계정의 토큰이 없거나 맞지 않습니다. 401 UNAUTHORIZED 로 나갑니다.
 *
 * <p>SUSPENDED(403)와 다릅니다. 그쪽은 "누구인지는 확인됐고 막혔다"이고, 이쪽은 "누구인지
 * 증명하지 못했다"입니다. 토큰이 없는 요청은 정지 검사에 닿기 전에 여기서 끝나므로, 남의
 * userId 를 빌려 정지 여부를 밖에서 알아내는 길도 같이 닫힙니다.
 *
 * <p>ACCOUNT_NOT_FOUND(404)와도 다릅니다. 계정이 없는 것이 아니라 계정을 다시 발급받아도
 * 해결되지 않습니다. 정상 클라이언트가 이 응답을 받는 경우는 서버 비밀이 바뀐 뒤 옛 토큰을
 * 들고 있을 때뿐이고, 그때는 계정을 다시 읽으면 새 토큰이 옵니다.
 */
public class UnauthorizedException extends RuntimeException {

    public UnauthorizedException() {
        super("계정 토큰이 없거나 맞지 않습니다.");
    }
}
