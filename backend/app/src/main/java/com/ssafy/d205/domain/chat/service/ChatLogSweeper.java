package com.ssafy.d205.domain.chat.service;

import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Component;
import org.springframework.transaction.annotation.Transactional;

import java.time.Duration;

import com.ssafy.d205.domain.chat.repository.ChatLogRepository;
import com.ssafy.d205.global.common.TimeProvider;

/**
 * 보관 기간이 지난 채팅을 지웁니다 (S15P21D205-1031).
 *
 * <p>이 표에는 <b>가리기 전의 원문</b>이 들어 있습니다. 목적은 신고가 들어왔을 때 확인하는
 * 것이고, 그 목적에 필요한 만큼만 둡니다. 기본 3일인 이유는 신고가 대개 경기 직후에 들어오기
 * 때문입니다. 이 작업이 멈추면 개인정보가 무한정 쌓입니다.
 *
 * <p><b>신고가 붙은 구간은 남깁니다.</b> 운영자가 아직 판단하지 않은 신고의 대화를 지우면
 * 판단할 근거가 사라진 채로 신고만 남습니다.
 *
 * <p>한 시간마다 돕니다. 하루 한 번이면 재시작 시각에 따라 하루치가 더 남고, 매 분은 지울
 * 것이 없는 DELETE 를 하루 1,440번 하는 일입니다. OpsSweeper 와 같은 판단입니다.
 */
@Component
@Slf4j
public class ChatLogSweeper {

    private final ChatLogRepository chatLogs;
    private final TimeProvider timeProvider;
    private final Duration retention;
    private final int protectWindowMinutes;

    public ChatLogSweeper(ChatLogRepository chatLogs,
                          TimeProvider timeProvider,
                          @Value("${chat.retention-days:3}") int retentionDays,
                          @Value("${chat.protect-window-minutes:180}") int protectWindowMinutes) {
        this.chatLogs = chatLogs;
        this.timeProvider = timeProvider;
        this.retention = Duration.ofDays(retentionDays);
        this.protectWindowMinutes = protectWindowMinutes;
    }

    /**
     * @return 지운 행 수
     */
    @Scheduled(fixedDelayString = "${chat.sweep-interval-ms:3600000}",
            initialDelayString = "${chat.sweep-interval-ms:3600000}")
    @Transactional
    public int sweep() {
        var swept = chatLogs.deleteExpired(timeProvider.minus(retention), protectWindowMinutes);
        if (swept > 0) {
            log.info("{}일 지난 채팅 {}행을 지웠습니다.", retention.toDays(), swept);
        }
        return swept;
    }
}
