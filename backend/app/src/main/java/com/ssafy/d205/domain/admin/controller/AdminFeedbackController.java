package com.ssafy.d205.domain.admin.controller;

import lombok.RequiredArgsConstructor;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PatchMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import com.ssafy.d205.domain.feedback.dto.FeedbackListResponse;
import com.ssafy.d205.domain.feedback.service.FeedbackCleanupService;
import com.ssafy.d205.domain.feedback.service.FeedbackReadService;

/**
 * 운영자가 피드백을 읽습니다.
 *
 * <p>경로가 /api/v1/admin 아래인 것이 곧 접근 규칙입니다. SecurityConfig 의 첫 체인이
 * 이 경로 전체에 로그인을 요구하므로 메서드마다 권한을 적을 필요가 없습니다. 반대로
 * <b>이 경로 밖에 관리 기능을 만들면 아무나 부를 수 있습니다.</b>
 *
 * <p>신고와 달리 쓰는 API 가 없습니다. 검토 상태를 두지 않았으므로 운영자가 바꿀 값이
 * 없습니다. 읽음 표시를 두지 않은 이유는 UserFeedback 주석에 있습니다.
 */
@RestController
@RequestMapping("/api/v1/admin/feedback")
@RequiredArgsConstructor
public class AdminFeedbackController {

    private final FeedbackReadService feedbackReadService;
    private final FeedbackCleanupService feedbackCleanupService;

    /**
     * 최근 피드백. 한 건씩 그대로 봅니다.
     *
     * <p>신고 목록처럼 사람 단위로 묶지 않습니다. 신고는 "이 사람이 몇 번 신고당했나"가
     * 판단 단위지만 피드백은 한 건 한 건이 읽을 내용이라, 묶으면 정작 본문이 사라집니다.
     *
     * <p>limit 을 크게 줘도 상한(200)으로 깎입니다. 400 을 주지 않는 이유는
     * FeedbackReadService 의 상수 주석에 있습니다.
     */
    @GetMapping
    public FeedbackListResponse list(@RequestParam(required = false) Integer limit) {
        return feedbackReadService.recent(limit);
    }

    /**
     * 피드백 한 건을 목록에서 치웁니다. 행은 남습니다.
     *
     * <p>읽음 표시가 아닙니다. 읽음을 두지 않은 이유는 UserFeedback 주석에 있고 그대로
     * 유효합니다 - "읽음"이 "처리했음"처럼 읽히기 때문입니다. 이것은 <b>더는 목록에
     * 띄우지 말라</b>는 뜻이고, 스팸 한 줄이 계속 위쪽을 차지하는 것을 막습니다.
     *
     * <p>PATCH 인 이유는 행의 한 필드를 바꾸는 것이기 때문입니다. 되돌릴 수 없는
     * 완전 삭제는 아래 DELETE 입니다.
     */
    @PatchMapping("/{feedbackId}/hidden")
    public CleanupResult hide(@PathVariable Integer feedbackId) {
        return new CleanupResult(feedbackCleanupService.hide(feedbackId) ? 1 : 0);
    }

    /**
     * 피드백 한 건을 지웁니다. <b>되돌릴 수 없습니다.</b>
     *
     * <p>없는 번호를 줘도 200 입니다. 두 사람이 같은 화면을 보다가 둘 다 눌렀을 때 뒤에
     * 누른 쪽에게 404 를 주면 무엇이 잘못됐는지 알 수 없는데, 원하는 결과는 이미
     * 이루어져 있습니다. 응답의 affected 가 0 이면 그런 경우입니다.
     */
    @DeleteMapping("/{feedbackId}")
    public CleanupResult purge(@PathVariable Integer feedbackId) {
        return new CleanupResult(feedbackCleanupService.purge(feedbackId) ? 1 : 0);
    }

    /** @param affected 이번 요청이 치우거나 지운 건수. 0 이면 이미 그렇게 돼 있었다는 뜻입니다. */
    public record CleanupResult(int affected) {
    }
}
