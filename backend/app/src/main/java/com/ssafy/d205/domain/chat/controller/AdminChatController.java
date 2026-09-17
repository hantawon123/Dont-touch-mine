package com.ssafy.d205.domain.chat.controller;

import jakarta.validation.constraints.Pattern;
import lombok.RequiredArgsConstructor;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import com.ssafy.d205.domain.chat.dto.AdminChatLine;
import com.ssafy.d205.domain.chat.service.AdminChatService;

/**
 * 신고를 판단하려는 운영자가 그때 오간 말을 읽는 자리 (S15P21D205-1030).
 *
 * <p><b>운영자 인증이 걸린 경로입니다.</b> {@code /api/v1/admin/**} 는 SecurityConfig 가 세션으로
 * 잠급니다. 채팅 원문은 개인정보이고, 이 화면은 그것을 사람 이름과 함께 펼쳐 보여줍니다.
 *
 * <p>기록은 3일만 남습니다(S15P21D205-1031). 이 화면에서 본 것을 근거로 정지한다면 그 줄을
 * 정지 사유에 함께 적어 두는 편이 낫습니다. 며칠 뒤에는 근거가 사라져 있습니다.
 */
@RestController
@RequestMapping("/api/v1/admin/chat")
@RequiredArgsConstructor
public class AdminChatController {

    /**
     * 사람은 대화가 끝난 뒤에 신고를 누릅니다. 신고 시각만 보면 정작 문제가 된 말이 구간 밖에
     * 있으므로 앞뒤로 넉넉히 엽니다.
     */
    private static final int DEFAULT_MINUTES = 15;

    private static final int DEFAULT_DAYS = 3;

    private final AdminChatService adminChatService;

    /**
     * 신고의 경기 키가 가리키는 방에서, 신고 시각 앞뒤의 대화.
     *
     * <p>키가 없는 신고(옛 클라이언트)는 어느 방인지 알 수 없어 빈 목록입니다. 화면은 그것을
     * "기록 없음" 이 아니라 "찾을 수 없음" 으로 구분해 보여줘야 합니다 - 대화가 없었던 것과
     * 어느 방인지 모르는 것은 다릅니다.
     */
    @GetMapping("/around")
    public AdminChatLine.ListResponse around(
            @RequestParam String contextKey,
            @RequestParam
            @Pattern(regexp = "^[0-9]{14}$", message = "reportedAt은 yyyyMMddHHmmss 형식이어야 합니다.")
            String reportedAt,
            @RequestParam(defaultValue = "" + DEFAULT_MINUTES) int minutes) {
        return adminChatService.around(contextKey, reportedAt, Math.clamp(minutes, 1, AdminChatService.MAX_WINDOW_MINUTES));
    }

    /** 이 사람이 최근에 한 말 전부. 여러 방에서 반복하는지가 여기서 보입니다. */
    @GetMapping("/by/{userId}")
    public AdminChatLine.ListResponse bySpeaker(
            @PathVariable String userId,
            @RequestParam(defaultValue = "" + DEFAULT_DAYS) int days) {
        return adminChatService.bySpeaker(userId, Math.clamp(days, 1, 30));
    }
}
