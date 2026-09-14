package com.ssafy.d205.domain.analytics.controller;

import jakarta.servlet.http.HttpServletRequest;
import jakarta.validation.Valid;
import jakarta.validation.constraints.NotEmpty;
import jakarta.validation.constraints.Size;
import lombok.RequiredArgsConstructor;
import org.springframework.http.HttpStatus;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;

import java.util.List;

import com.ssafy.d205.domain.analytics.dto.GameEventRequest;
import com.ssafy.d205.domain.analytics.service.GameEventIngestService;

/**
 * 플레이 로그 수집. 명세는 docs/analytics-events.md, 특히 7절(전송 규약)입니다.
 *
 * <p>경로를 {@code /api/v1/events} 로 고정합니다. 나중에 수집을 별도 서비스로 뺄 때 nginx 에서
 * 이 경로만 다른 포트로 돌리면 이사가 끝나야 합니다. 그래서 다른 컨트롤러와 달리 도메인
 * 이름(analytics)이 경로에 없습니다.
 *
 * <p><b>X-User-Id 를 요구하지 않습니다.</b> 이벤트의 주체는 이벤트마다 user_public_id 로 들어오고,
 * 호스트가 다른 플레이어를 대신해 보내기 때문에 요청 하나가 한 사람의 것이 아닙니다. 인증이
 * 없다는 사실은 다른 API 와 같고, 대신 IP 레이트 리밋이 있습니다.
 *
 * <p>응답 본문이 없습니다. 클라이언트가 재전송을 판단할 근거는 HTTP 상태뿐입니다. 202 는
 * "받았다"이지 "저장했다"가 아닙니다. 저장은 뒤에서 일어나고 실패할 수도 있습니다.
 */
@RestController
@RequestMapping("/api/v1/events")
@RequiredArgsConstructor
public class GameEventController {

    private final GameEventIngestService ingestService;

    /**
     * 배열 상한. 애너테이션 값은 컴파일 시점 상수여야 해서 프로퍼티로 뺄 수 없습니다. 그래서
     * analytics.* 설정에 이 값은 없습니다. 클라이언트는 50건마다 flush 하므로(명세 7절) 200 은
     * 그 네 배이고, 스풀 재전송이 몰릴 때를 위한 여유입니다.
     *
     * <p>파라미터에 붙은 제약은 스프링이 HandlerMethodValidationException 으로 올리고 핸들러가
     * INVALID_REQUEST 로 바꿉니다. 클래스에 &#64;Validated 를 붙이면 안 됩니다. 그러면 AOP 검증이
     * 한 번 더 돌아 다른 예외(ConstraintViolationException)로 나가 500 이 됩니다.
     */
    public static final int MAX_BATCH_SIZE = 200;

    @PostMapping
    @ResponseStatus(HttpStatus.ACCEPTED)
    public void ingest(HttpServletRequest request,
                       @RequestBody
                       @NotEmpty(message = "events가 비어 있습니다.")
                       @Size(max = MAX_BATCH_SIZE, message = "한 요청에 " + MAX_BATCH_SIZE + "건까지 보낼 수 있습니다.")
                       List<@Valid GameEventRequest> events) {
        ingestService.ingest(request.getRemoteAddr(), events);
    }
}
