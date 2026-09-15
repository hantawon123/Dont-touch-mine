package com.ssafy.d205.domain.ops.service;

import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Component;
import org.springframework.transaction.annotation.Transactional;

import java.time.Duration;

import com.ssafy.d205.domain.ops.repository.OpsSampleRepository;
import com.ssafy.d205.global.common.TimeProvider;

/**
 * 보관 기간을 넘긴 운영 지표 샘플을 지웁니다.
 *
 * <p>기본 30일. 1분에 한 행이라 30일이면 43,200행이고, 개요 탭이 보는 것은 최근 7일까지입니다.
 * 더 오래 보고 싶으면 Grafana(865) 같은 시계열 도구가 맞는 자리이고, 여기는 관리 화면이 스스로
 * 그릴 수 있는 만큼만 둡니다.
 *
 * <p>한 시간마다 돕니다. 하루 한 번이면 재시작 시각에 따라 이틀치가 남을 수 있고, 매 분은 지울
 * 것이 없는 DELETE 를 하루 1,440번 하는 일입니다.
 */
@Component
@Slf4j
public class OpsSweeper {

    private final OpsSampleRepository samples;
    private final TimeProvider timeProvider;
    private final Duration retention;

    public OpsSweeper(OpsSampleRepository samples,
                      TimeProvider timeProvider,
                      @Value("${ops.retention-days:30}") int retentionDays) {
        this.samples = samples;
        this.timeProvider = timeProvider;
        this.retention = Duration.ofDays(retentionDays);
    }

    @Scheduled(fixedDelayString = "${ops.sweep-interval-ms:3600000}", initialDelayString = "${ops.sweep-interval-ms:3600000}")
    @Transactional
    public int sweep() {
        int swept = samples.deleteOlderThan(timeProvider.minus(retention));
        if (swept > 0) {
            log.info("{}일 지난 운영 지표 샘플 {}행을 지웠습니다.", retention.toDays(), swept);
        }
        return swept;
    }
}
