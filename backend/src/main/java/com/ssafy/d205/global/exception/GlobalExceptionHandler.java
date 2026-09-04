package com.ssafy.d205.global.exception;

import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.validation.FieldError;
import org.springframework.context.MessageSourceResolvable;
import org.springframework.web.bind.MethodArgumentNotValidException;
import org.springframework.http.converter.HttpMessageNotReadableException;
import org.springframework.web.bind.MissingRequestHeaderException;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.RestControllerAdvice;
import org.springframework.web.method.annotation.HandlerMethodValidationException;

import java.util.stream.Collectors;

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

    /**
     * 쿼리 파라미터나 경로 변수의 제약 위반입니다.
     *
     * <p>요청 본문(@Valid @RequestBody)은 MethodArgumentNotValidException 으로 오지만,
     * 파라미터에 직접 붙인 제약은 스프링 프레임워크 6.1부터 이 예외로 옵니다. 둘을
     * 같은 코드로 내보내는 것은 클라이언트에게 "요청이 규칙에 안 맞는다"는 사실이
     * 같기 때문입니다. 어디가 틀렸는지는 메시지가 알려줍니다.
     */
    @ExceptionHandler(HandlerMethodValidationException.class)
    public ResponseEntity<ErrorResponse> handleParameterValidation(HandlerMethodValidationException e) {
        String message = e.getAllErrors().stream()
                .map(MessageSourceResolvable::getDefaultMessage)
                .collect(Collectors.joining(" "));
        return ResponseEntity.badRequest().body(new ErrorResponse("INVALID_REQUEST", message));
    }

    /**
     * 본문을 아예 읽지 못한 경우입니다. JSON 이 깨졌거나, 값이 필드의 타입에 맞지 않습니다.
     *
     * <p>신고 사유처럼 열거형으로 받는 값이 목록에 없으면 여기로 옵니다. @Valid 가
     * 도는 것은 본문이 객체로 바뀐 뒤라, 바뀌지 못한 요청은 검증까지 가지 못합니다.
     * 이 핸들러가 없으면 스프링 기본 응답이 나가고 클라이언트는 code 로 분기할 수
     * 없습니다.
     *
     * <p><b>예외 메시지를 그대로 쓰지 않습니다.</b> 잭슨의 메시지에는 클래스 이름과
     * 필드 경로가 들어 있어서 내부 구조가 그대로 흘러나갑니다.
     */
    @ExceptionHandler(HttpMessageNotReadableException.class)
    public ResponseEntity<ErrorResponse> handleUnreadableBody(HttpMessageNotReadableException e) {
        return ResponseEntity.badRequest()
                .body(new ErrorResponse("INVALID_REQUEST", "요청 본문의 값이 형식에 맞지 않습니다."));
    }

    @ExceptionHandler(MissingRequestHeaderException.class)
    public ResponseEntity<ErrorResponse> handleMissingHeader(MissingRequestHeaderException e) {
        return ResponseEntity.badRequest()
                .body(new ErrorResponse("MISSING_HEADER", e.getHeaderName() + " 헤더가 필요합니다."));
    }

    @ExceptionHandler(UnknownCallerException.class)
    public ResponseEntity<ErrorResponse> handleAccountNotFound(UnknownCallerException e) {
        return ResponseEntity.status(HttpStatus.NOT_FOUND)
                .body(new ErrorResponse("ACCOUNT_NOT_FOUND", "계정을 찾을 수 없습니다."));
    }

    /**
     * 상대를 찾을 수 없습니다. 부르는 사람이 없는 경우(ACCOUNT_NOT_FOUND)와 코드를
     * 구분합니다. 전자는 클라이언트가 계정 발급을 다시 불러야 하고, 후자는 "그
     * 사용자가 없습니다"를 보여주면 됩니다.
     *
     * <p>차단 관계일 때도 이 코드입니다. 차단당했다는 사실을 알려주면 차단한 사람이
     * 드러나므로, 없는 사용자와 구분되지 않게 두는 것이 의도입니다.
     */
    @ExceptionHandler(TargetUserNotFoundException.class)
    public ResponseEntity<ErrorResponse> handleTargetNotFound(TargetUserNotFoundException e) {
        return ResponseEntity.status(HttpStatus.NOT_FOUND)
                .body(new ErrorResponse("TARGET_NOT_FOUND", "상대를 찾을 수 없습니다."));
    }

    @ExceptionHandler(SelfFriendRequestException.class)
    public ResponseEntity<ErrorResponse> handleSelfRequest(SelfFriendRequestException e) {
        return ResponseEntity.badRequest()
                .body(new ErrorResponse("SELF_FRIEND_REQUEST", "자기 자신에게 친구 요청을 보낼 수 없습니다."));
    }


    @ExceptionHandler(AlreadyFriendsException.class)
    public ResponseEntity<ErrorResponse> handleAlreadyFriends(AlreadyFriendsException e) {
        return ResponseEntity.status(HttpStatus.CONFLICT)
                .body(new ErrorResponse("ALREADY_FRIENDS", "이미 친구입니다."));
    }

    @ExceptionHandler(FriendRequestAlreadySentException.class)
    public ResponseEntity<ErrorResponse> handleRequestAlreadySent(FriendRequestAlreadySentException e) {
        return ResponseEntity.status(HttpStatus.CONFLICT)
                .body(new ErrorResponse("REQUEST_ALREADY_SENT", "이미 보낸 친구 요청입니다."));
    }

    @ExceptionHandler(FriendRequestNotFoundException.class)
    public ResponseEntity<ErrorResponse> handleRequestNotFound(FriendRequestNotFoundException e) {
        return ResponseEntity.status(HttpStatus.NOT_FOUND)
                .body(new ErrorResponse("FRIEND_REQUEST_NOT_FOUND", "친구 요청을 찾을 수 없습니다."));
    }

    @ExceptionHandler(NotFriendsException.class)
    public ResponseEntity<ErrorResponse> handleNotFriends(NotFriendsException e) {
        return ResponseEntity.status(HttpStatus.NOT_FOUND)
                .body(new ErrorResponse("NOT_FRIENDS", "친구가 아닙니다."));
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
