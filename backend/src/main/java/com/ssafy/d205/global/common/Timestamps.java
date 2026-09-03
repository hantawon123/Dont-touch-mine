package com.ssafy.d205.global.common;

import java.time.Instant;
import java.time.ZoneOffset;
import java.time.format.DateTimeFormatter;

/**
 * CHAR(14) 시각 문자열을 만듭니다.
 *
 * <p>스키마가 시각을 DATETIME이 아니라 yyyyMMddHHmmss 고정 길이 문자열로 저장합니다.
 * 고정 길이라 사전순 정렬이 시간순 정렬과 일치해서 범위 조회와 인덱스가 그대로
 * 동작합니다. 대신 문자열에는 타임존 정보가 없으므로 <b>UTC 규칙을 어기면 아무 오류
 * 없이 조용히 어긋납니다.</b>
 *
 * <p>그래서 시각 생성을 이 클래스 한 곳으로 모읍니다. 여기저기서 LocalDateTime.now()를
 * 쓰면 서버 타임존(Asia/Seoul)이 섞여 들어가는데, 그 데이터는 9시간 어긋난 채로
 * 저장되고 나중에 구분할 방법이 없습니다.
 */
public final class Timestamps {

    private static final DateTimeFormatter FORMAT =
            DateTimeFormatter.ofPattern("yyyyMMddHHmmss").withZone(ZoneOffset.UTC);

    private Timestamps() {
    }

    public static String now() {
        return FORMAT.format(Instant.now());
    }

    public static String of(Instant instant) {
        return FORMAT.format(instant);
    }
}
