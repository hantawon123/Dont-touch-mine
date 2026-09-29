package com.ssafy.d205.domain.analytics.exception;

/** 한 IP 가 분당 허용량을 넘겼습니다. 핸들러가 429 로 옮깁니다. */
public class RateLimitedException extends RuntimeException {

    public RateLimitedException(String ip) {
        super("요청이 너무 잦습니다: " + ip);
    }
}
