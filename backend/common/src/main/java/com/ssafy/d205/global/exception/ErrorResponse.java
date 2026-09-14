package com.ssafy.d205.global.exception;

/**
 * @param code    클라이언트가 분기에 쓸 값입니다. 메시지는 문구가 바뀔 수 있으니
 *                코드로 분기하고 메시지는 사람이 읽는 데만 쓰세요.
 * @param message 사용자에게 그대로 보여줘도 되는 문구입니다. 예외 클래스명이나
 *                스택트레이스, SQL 조각은 담지 않습니다.
 */
public record ErrorResponse(
        String code,
        String message
) {
}
