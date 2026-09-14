package com.ssafy.d205.global.security;

/**
 * 로그인 시도에서 받은 값을 로그에 남길 수 있는 모양으로 다듬습니다.
 *
 * <p>로그인은 인증 없이 부르는 자리라 아이디 칸에 무엇이든 들어옵니다. 줄바꿈이 섞여
 * 있으면 <b>부르는 쪽이 로그에 가짜 줄을 써 넣을 수 있습니다.</b> 나중에 로그를 읽는
 * 사람이 일어나지 않은 일을 보게 됩니다.
 *
 * <p>길이도 자릅니다. 아이디 칸에 수십 KB 를 넣어 로그 파일을 채우는 것을 막습니다.
 */
final class LoginAttempt {

    /** 이보다 긴 아이디는 어차피 계정이 아닙니다. */
    private static final int MAX = 64;

    private LoginAttempt() {
    }

    static String forLog(String value) {
        if (value == null || value.isEmpty()) {
            return "(없음)";
        }

        int length = Math.min(value.length(), MAX);
        StringBuilder safe = new StringBuilder(length);

        for (int index = 0; index < length; index++) {
            char character = value.charAt(index);
            safe.append(Character.isISOControl(character) ? '?' : character);
        }

        return value.length() > MAX ? safe + "..." : safe.toString();
    }
}
