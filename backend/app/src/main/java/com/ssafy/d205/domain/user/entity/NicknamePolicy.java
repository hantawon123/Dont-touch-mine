package com.ssafy.d205.domain.user.entity;

import java.util.regex.Pattern;

/**
 * 닉네임 규칙.
 *
 * <p>한글, 영문, 숫자만 2~12글자. 공백과 특수문자는 받지 않습니다.
 *
 * <p>규칙을 정규식 한 줄로 두는 이유는 검증하는 곳이 둘이기 때문입니다. 요청 DTO의
 * 애너테이션과 서버가 만드는 자동 닉네임이 같은 규칙을 따라야 하는데, 규칙이 두 벌로
 * 갈라지면 서버가 스스로 만든 닉네임이 변경 API에서 거부되는 상태가 됩니다.
 *
 * <p>한글은 완성형(가-힣)만 허용합니다. 자모 단독(ㅋ, ㅏ)을 열면 "ㅋㅋㅋㅋ" 같은
 * 닉네임이 통과하는데, 그건 이름이라기보다 잡음에 가깝습니다.
 *
 * <p>길이는 String.length()가 세는 단위로 맞습니다. 허용 문자가 전부 BMP 안의 한
 * 단위짜리라서, 이모지처럼 두 단위를 차지하는 글자가 애초에 들어올 수 없습니다.
 */
public final class NicknamePolicy {

    public static final int MIN_LENGTH = 2;
    public static final int MAX_LENGTH = 12;

    /** DTO 애너테이션에 그대로 넣기 위해 문자열 상수로도 둡니다. */
    public static final String REGEX = "^[가-힣a-zA-Z0-9]{2,12}$";

    private static final Pattern PATTERN = Pattern.compile(REGEX);

    private NicknamePolicy() {
    }

    public static boolean isValid(String nickname) {
        return nickname != null && PATTERN.matcher(nickname).matches();
    }
}
