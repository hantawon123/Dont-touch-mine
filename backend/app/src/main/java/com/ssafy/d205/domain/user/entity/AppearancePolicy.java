package com.ssafy.d205.domain.user.entity;

/**
 * 파츠 id 의 형식. 요청 검증과 문서가 같은 값을 보게 하려고 한 곳에 둡니다.
 *
 * <p><b>서버는 파츠 목록을 모릅니다.</b> 어떤 id 가 실제로 있는지는 클라이언트의 카탈로그가
 * 정하고, 여기서는 길이와 문자만 봅니다. 목록을 서버가 들면 파츠를 하나 늘릴 때마다 배포가
 * 필요해지고, 클라이언트와 서버의 목록이 어긋나는 순간 옷장이 통째로 400 이 됩니다.
 *
 * <p>문자 규칙을 넉넉히 둔 이유가 있습니다. 몸 색상이 팔레트 id 가 아니라 {@code #A1B2C3}
 * 같은 색상값일 수 있습니다. 소문자와 밑줄만 허용하면 그 순간 막힙니다. 공백과 제어 문자만
 * 막고 나머지 ASCII 출력 문자는 다 받습니다.
 */
public final class AppearancePolicy {

    /**
     * 파츠 id 최대 길이. V12 의 컬럼 길이와 같은 값입니다.
     *
     * <p>Photon 복제 한도가 아닙니다. 클라이언트는 외형을 아직 자기 화면에만 적용하고 다른
     * 사람에게 복제하지 않으며, 서버와는 이 문자열 id 를 그대로 주고받습니다. 그래서 이 값은
     * 저장 길이만 뜻하고, 바꿀 때 맞춰야 할 곳은 V12 와 client-guide 두 곳입니다.
     */
    public static final int MAX_LENGTH = 32;

    /** 공백 없는 ASCII 출력 문자(0x21~0x7E)만, 1~32자. */
    public static final String REGEX = "^[\\x21-\\x7E]{1," + MAX_LENGTH + "}$";

    private AppearancePolicy() {
    }
}
