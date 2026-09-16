package com.ssafy.d205.global.exception;

/**
 * 같은 경기에서 같은 사람을 이미 신고했습니다 (S15P21D205-1017).
 *
 * <p>경기 키를 안 보낸 옛 클라이언트에게는 "24시간 안에 같은 사람을 이미 신고했다"는 뜻입니다.
 */
public class ReportAlreadySentException extends RuntimeException {

    public ReportAlreadySentException() {
        super("이 경기에서 그 사람을 이미 신고했습니다.");
    }
}
