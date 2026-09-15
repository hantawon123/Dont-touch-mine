package com.ssafy.d205.domain.analytics.exception;

import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.http.converter.HttpMessageNotReadableException;
import org.springframework.validation.FieldError;
import org.springframework.web.bind.MethodArgumentNotValidException;
import org.springframework.web.bind.MissingServletRequestParameterException;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.RestControllerAdvice;
import org.springframework.web.method.annotation.HandlerMethodValidationException;

import java.util.stream.Collectors;

import com.ssafy.d205.domain.analytics.query.InvalidAnalyticsFilterException;
import com.ssafy.d205.domain.analytics.query.UnknownAnalyticsQuestionException;
import com.ssafy.d205.global.exception.ErrorResponse;

/**
 * 수집 서비스의 예외를 HTTP 응답으로 옮깁니다.
 *
 * <p>계정 서비스의 GlobalExceptionHandler 와 같은 응답 모양({@link ErrorResponse})을 씁니다. 클라이언트는
 * 어느 서비스가 답했는지 모르고 code 로만 분기하므로, 두 핸들러가 다른 모양을 내면 그 약속이 깨집니다.
 * 모양은 common 이 들고 있고, 어떤 예외를 어떤 코드로 옮기는지는 서비스마다 자기 것만 적습니다.
 *
 * <p>코드는 넷뿐입니다. INVALID_REQUEST(형식·내용 위반), RATE_LIMITED(분당 상한), 내부 조회의
 * QUESTION_NOT_FOUND(문서에 없는 질문 이름), 그리고 나머지는 스프링 기본 처리에 맡깁니다. 이 서비스에는
 * 계정도 세션도 없어 그 밖의 코드가 나올 자리가 없습니다.
 */
@RestControllerAdvice
public class AnalyticsExceptionHandler {

    /** 내부 조회의 필수 쿼리(positions 의 matchId)가 빠진 경우입니다. 스프링 기본 400 은 모양이 다릅니다. */
    @ExceptionHandler(MissingServletRequestParameterException.class)
    public ResponseEntity<ErrorResponse> handleMissingParameter(MissingServletRequestParameterException e) {
        return ResponseEntity.badRequest()
                .body(new ErrorResponse("INVALID_REQUEST", "필수 값 " + e.getParameterName() + " 이 없습니다."));
    }

    /** 자릿수는 맞지만 날짜가 아닌 필터 값(13월 등)입니다. */
    @ExceptionHandler(InvalidAnalyticsFilterException.class)
    public ResponseEntity<ErrorResponse> handleInvalidFilter(InvalidAnalyticsFilterException e) {
        return ResponseEntity.badRequest().body(new ErrorResponse("INVALID_REQUEST", e.getMessage()));
    }

    /**
     * 문서에 없는 질문 이름입니다. 부르는 쪽은 계정 서비스의 프록시이고 그 이름은 관리 화면이 고정으로
     * 갖고 있으므로, 여기 오면 화면과 문서가 어긋난 것입니다.
     */
    @ExceptionHandler(UnknownAnalyticsQuestionException.class)
    public ResponseEntity<ErrorResponse> handleUnknownQuestion(UnknownAnalyticsQuestionException e) {
        return ResponseEntity.status(HttpStatus.NOT_FOUND)
                .body(new ErrorResponse("QUESTION_NOT_FOUND", e.getMessage()));
    }

    @ExceptionHandler(MethodArgumentNotValidException.class)
    public ResponseEntity<ErrorResponse> handleValidation(MethodArgumentNotValidException e) {
        String message = e.getBindingResult().getFieldErrors().stream()
                .map(FieldError::getDefaultMessage)
                .collect(Collectors.joining(", "));
        return ResponseEntity.badRequest().body(new ErrorResponse("INVALID_REQUEST", message));
    }

    /** 컨트롤러 파라미터에 직접 붙은 제약(배치 크기 상한 등)을 어긴 경우입니다. */
    @ExceptionHandler(HandlerMethodValidationException.class)
    public ResponseEntity<ErrorResponse> handleParameterValidation(HandlerMethodValidationException e) {
        String message = e.getAllErrors().stream()
                .map(error -> error.getDefaultMessage())
                .collect(Collectors.joining(", "));
        return ResponseEntity.badRequest().body(new ErrorResponse("INVALID_REQUEST", message));
    }

    /**
     * 본문이 JSON 으로 읽히지 않았습니다. 잭슨의 메시지에는 클래스 이름과 필드 경로가 들어 있어
     * 그대로 내보내지 않습니다.
     */
    @ExceptionHandler(HttpMessageNotReadableException.class)
    public ResponseEntity<ErrorResponse> handleUnreadableBody(HttpMessageNotReadableException e) {
        return ResponseEntity.badRequest()
                .body(new ErrorResponse("INVALID_REQUEST", "요청 본문의 값이 형식에 맞지 않습니다."));
    }

    /**
     * 배치가 형식은 맞지만 내용 규칙(이벤트 목록, 시각 범위, params 크기)을 어긴 경우입니다. 형식
     * 위반과 같은 INVALID_REQUEST 인 것은 클라이언트의 대응이 같기 때문입니다. 그 배치를 버리고
     * 재전송하지 않는다. 메시지가 몇 번째 이벤트의 무엇인지 말합니다.
     */
    @ExceptionHandler(EventBatchRejectedException.class)
    public ResponseEntity<ErrorResponse> handleEventBatchRejected(EventBatchRejectedException e) {
        return ResponseEntity.badRequest().body(new ErrorResponse("INVALID_REQUEST", e.getMessage()));
    }

    /**
     * 한 IP 가 분당 허용량 넘게 보냈습니다. 클라이언트는 잠시 기다린 뒤 스풀에서 다시 보내면 됩니다.
     */
    @ExceptionHandler(RateLimitedException.class)
    public ResponseEntity<ErrorResponse> handleRateLimited(RateLimitedException e) {
        return ResponseEntity.status(HttpStatus.TOO_MANY_REQUESTS)
                .body(new ErrorResponse("RATE_LIMITED", "요청이 너무 잦습니다. 잠시 뒤 다시 보내세요."));
    }
}
