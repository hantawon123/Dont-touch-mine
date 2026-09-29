package com.ssafy.d205.domain.admin.controller;

import lombok.RequiredArgsConstructor;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import com.ssafy.d205.domain.admin.dto.AdminOverview;
import com.ssafy.d205.domain.admin.service.AdminOverviewService;

/**
 * 관리 화면 첫 탭. 지금 접속자·플레이어·운영 숫자와 최근 접속자·서버 자원 추이.
 *
 * <p><b>이 경로는 SecurityConfig 의 adminChain 이 로그인을 요구합니다.</b> 서버 자원과 접속자
 * 수는 바깥에 보일 값이 아닙니다 - Actuator 메트릭을 열지 않고 여기서만 내보내는 이유입니다.
 */
@RestController
@RequestMapping("/api/v1/admin/overview")
@RequiredArgsConstructor
public class AdminOverviewController {

    private final AdminOverviewService adminOverviewService;

    /**
     * @param range "24h"(기본) 또는 "7d". 그 외 값은 24h 로 읽습니다. 운영자 화면이 고른 값이라
     *              400 을 낼 이유가 없습니다
     */
    @GetMapping
    public AdminOverview overview(@RequestParam(defaultValue = "24h") String range) {
        return adminOverviewService.overview(range);
    }
}
