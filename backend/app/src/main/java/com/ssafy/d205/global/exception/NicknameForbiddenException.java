package com.ssafy.d205.global.exception;

/**
 * 닉네임에 금칙어가 들어 있습니다 (S15P21D205-1017).
 *
 * <p>어느 말에 걸렸는지는 메시지에도 응답에도 담지 않습니다. 담으면 한 글자씩 바꿔 보며 목록을
 * 알아내는 데 쓰입니다.
 */
public class NicknameForbiddenException extends RuntimeException {

    public NicknameForbiddenException() {
        super("쓸 수 없는 닉네임입니다.");
    }
}
