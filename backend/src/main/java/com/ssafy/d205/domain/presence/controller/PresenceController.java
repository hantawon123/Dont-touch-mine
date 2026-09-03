package com.ssafy.d205.domain.presence.controller;

import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.http.HttpStatus;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestHeader;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;

import com.ssafy.d205.domain.presence.dto.UpdatePresenceRequest;
import com.ssafy.d205.domain.presence.service.PresenceService;

@RestController
@RequestMapping("/api/v1/presence")
@RequiredArgsConstructor
public class PresenceController {

    private static final String USER_ID_HEADER = "X-User-Id";

    private final PresenceService presenceService;

    /**
     * 하트비트. 사용자당 한 행을 통째로 덮어쓰는 멱등 연산이라 PUT 입니다.
     *
     * <p>응답 본문이 없습니다. 클라이언트가 돌려받아 쓸 것이 없고, 30초마다 오는 요청에
     * 본문을 실으면 대역폭만 씁니다.
     */
    @PutMapping
    @ResponseStatus(HttpStatus.NO_CONTENT)
    public void report(@RequestHeader(USER_ID_HEADER) String userId,
                       @Valid @RequestBody UpdatePresenceRequest request) {
        presenceService.report(userId, request.sessionId());
    }

    /** 정상 종료를 알립니다. 타임아웃을 기다리지 않고 친구 목록에서 바로 내려갑니다. */
    @DeleteMapping
    @ResponseStatus(HttpStatus.NO_CONTENT)
    public void goOffline(@RequestHeader(USER_ID_HEADER) String userId) {
        presenceService.goOffline(userId);
    }
}
