package com.ssafy.d205.common;

import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.validation.FieldError;
import org.springframework.web.bind.MethodArgumentNotValidException;
import org.springframework.web.bind.MissingRequestHeaderException;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.RestControllerAdvice;

import java.util.stream.Collectors;

import com.ssafy.d205.account.AccountNotFoundException;
import com.ssafy.d205.account.NicknameGenerationFailedException;
import com.ssafy.d205.account.NicknameTakenException;

/**
 * 예외를 HTTP 응답으로 옮깁니다.
 *
 * <p>이게 없으면 스프링 기본 처리가 나가는데, 그건 예외 종류를 대체로 500으로 뭉개거나
 * 응답 본문에 내부 정보를 흘립니다. 잘못된 요청과 서버 고장을 클라이언트가 구분할 수
 * 있어야 재시도 여부를 판단할 수 있습니다.
 */
@RestControllerAdvice
public class GlobalExceptionHandler {

    @ExceptionHandler(MethodArgumentNotValidException.class)
    public ResponseEntity<ErrorResponse> handleValidation(MethodArgumentNotValidException e) {
        String message = e.getBindingResult().getFieldErrors().stream()
                .map(FieldError::getDefaultMessage)
                .collect(Collectors.joining(" "));
        return ResponseEntity.badRequest().body(new ErrorResponse("INVALID_REQUEST", message));
    }

    @ExceptionHandler(MissingRequestHeaderException.class)
    public ResponseEntity<ErrorResponse> handleMissingHeader(MissingRequestHeaderException e) {
        return ResponseEntity.badRequest()
                .body(new ErrorResponse("MISSING_HEADER", e.getHeaderName() + " 헤더가 필요합니다."));
    }

    @ExceptionHandler(AccountNotFoundException.class)
    public ResponseEntity<ErrorResponse> handleAccountNotFound(AccountNotFoundException e) {
        return ResponseEntity.status(HttpStatus.NOT_FOUND)
                .body(new ErrorResponse("ACCOUNT_NOT_FOUND", "계정을 찾을 수 없습니다."));
    }

    @ExceptionHandler(NicknameTakenException.class)
    public ResponseEntity<ErrorResponse> handleNicknameTaken(NicknameTakenException e) {
        return ResponseEntity.status(HttpStatus.CONFLICT)
                .body(new ErrorResponse("NICKNAME_TAKEN", "이미 사용 중인 닉네임입니다."));
    }

    /**
     * 닉네임 변경이 uk_users_nickname에 걸린 경우입니다.
     *
     * <p>서비스가 미리 조회해 확인하지만 그 사이에 다른 요청이 같은 닉네임을 차지할 수
     * 있습니다. 그 경쟁에서 진 요청이 여기로 옵니다. 사용자에게는 위와 같은 상황이므로
     * 같은 409를 줍니다.
     */
    @ExceptionHandler(DataIntegrityViolationException.class)
    public ResponseEntity<ErrorResponse> handleConflict(DataIntegrityViolationException e) {
        return ResponseEntity.status(HttpStatus.CONFLICT)
                .body(new ErrorResponse("CONFLICT", "요청이 다른 데이터와 충돌했습니다. 다시 시도해 주세요."));
    }

    @ExceptionHandler(NicknameGenerationFailedException.class)
    public ResponseEntity<ErrorResponse> handleNicknameGeneration(NicknameGenerationFailedException e) {
        return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR)
                .body(new ErrorResponse("NICKNAME_GENERATION_FAILED", "계정 발급에 실패했습니다. 다시 시도해 주세요."));
    }
}
