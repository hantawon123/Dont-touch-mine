package com.ssafy.d205.global.common;

import java.time.Instant;
import java.time.ZoneOffset;
import java.time.format.DateTimeFormatter;

/**
 * CHAR(14) 시각 문자열의 형식만 담당합니다.
 *
 * <p>스키마가 시각을 DATETIME 이 아니라 yyyyMMddHHmmss 고정 길이 문자열로 저장합니다.
 * 고정 길이라 사전순 정렬이 시간순 정렬과 일치해서 범위 조회와 인덱스가 그대로
 * 동작합니다. 대신 문자열에는 타임존 정보가 없으므로 <b>UTC 규칙을 어기면 아무 오류 없이
 * 조용히 어긋납니다.</b> 그래서 형식 변환을 이 한 곳으로 모읍니다.
 *
 * <p><b>now() 를 두지 않습니다.</b> 시계를 읽는 것은 {@link TimeProvider} 하나만 합니다.
 * 여기에 now() 가 있으면 엔티티가 각자 시계를 읽게 되고, 그러면 한 연산 안에서 만들어진
 * 시각들이 서로 어긋납니다. 실제로 계정을 만들 때 users.created_at 과
 * user_identities.linked_at 이 1초 다를 수 있었습니다.
 */
public final class Timestamps {

    private static final DateTimeFormatter FORMAT =
            DateTimeFormatter.ofPattern("yyyyMMddHHmmss").withZone(ZoneOffset.UTC);

    private Timestamps() {
    }

    public static String format(Instant instant) {
        return FORMAT.format(instant);
    }
}
