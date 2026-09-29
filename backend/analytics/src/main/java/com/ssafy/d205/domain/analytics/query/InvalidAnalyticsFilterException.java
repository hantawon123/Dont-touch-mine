package com.ssafy.d205.domain.analytics.query;

/** 필터 값이 형식은 맞지만 날짜로 읽히지 않는 경우(13월 등). 400 INVALID_REQUEST 로 답합니다. */
public class InvalidAnalyticsFilterException extends RuntimeException {

    public InvalidAnalyticsFilterException(String message) {
        super(message);
    }
}
