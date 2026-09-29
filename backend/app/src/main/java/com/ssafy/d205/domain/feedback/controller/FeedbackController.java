package com.ssafy.d205.domain.feedback.controller;

import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.http.HttpStatus;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestHeader;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;

import com.ssafy.d205.domain.feedback.dto.SendFeedbackRequest;
import com.ssafy.d205.domain.feedback.service.FeedbackService;

/**
 * 플레이어가 보내는 피드백.
 *
 * <p>보내는 것만 있습니다. 자기가 보낸 피드백을 조회하는 API 도, 취소하는 API 도 두지
 * 않았습니다. 신고와 같은 이유입니다 - 결과가 화면에 나타나지 않으므로 클라이언트가
 * 다시 읽을 이유가 없습니다.
 *
 * <p>답장이 없다는 것을 화면이 분명히 말해야 합니다. 서버가 201 을 주는 것은 "적어
 * 뒀다"는 뜻이고 "읽고 답하겠다"는 뜻이 아닙니다. 답을 보낼 경로(메일·알림)가 없는
 * 채로 답장을 약속하면 지키지 못합니다.
 */
@RestController
@RequestMapping("/api/v1/feedback")
@RequiredArgsConstructor
public class FeedbackController {

    private static final String USER_ID_HEADER = "X-User-Id";

    private final FeedbackService feedbackService;

    /**
     * 피드백 보내기.
     *
     * <p>본문이 없는 201 입니다. 만들어진 기록을 가리킬 곳이 없어서 Location 도 두지
     * 않습니다. 클라이언트가 할 일은 "보냈습니다"를 띄우고 입력창을 비우는 것뿐입니다.
     *
     * <p>익명으로 받지 않습니다. 헤더가 없으면 400(MISSING_HEADER)입니다. 누가 썼는지
     * 모르면 같은 사람이 열 번 보낸 것과 열 명이 보낸 것을 구분할 수 없고, 그 구분이
     * 피드백을 읽는 사람에게 가장 중요한 정보입니다. 이 게임은 계정 발급이 자동이라
     * 익명을 허용해서 얻을 것도 없습니다.
     */
    @PostMapping
    @ResponseStatus(HttpStatus.CREATED)
    public void submit(@RequestHeader(USER_ID_HEADER) String callerUserId,
                       @Valid @RequestBody SendFeedbackRequest request) {
        feedbackService.submit(callerUserId, request);
    }
}
