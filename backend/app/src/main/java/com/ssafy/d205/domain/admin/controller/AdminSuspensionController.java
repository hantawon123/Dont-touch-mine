package com.ssafy.d205.domain.admin.controller;

import lombok.RequiredArgsConstructor;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import com.ssafy.d205.domain.admin.dto.AdminSuspensionListResponse;
import com.ssafy.d205.domain.admin.service.AdminSuspensionQueryService;

/**
 * 지금 정지된 계정과 최근 해제 이력 (S15P21D205-974).
 *
 * <p><b>이 경로는 SecurityConfig 의 adminChain 이 로그인을 요구합니다.</b>
 * {@code /api/v1/admin/**} 전체가 그 체인에 잡힙니다.
 *
 * <p>정지를 걸고 푸는 것은 {@link AdminUserController} 에 있습니다. 대상이 사용자 한 명이라
 * 그 자원 아래에 있어야 하고, 여기는 상태 전체를 훑는 목록이라 자원이 다릅니다.
 *
 * <p>조회만 있습니다. 이 화면에서 바로 해제하지 않고 사용자 상세로 보내는 이유는, 해제는
 * 그 사람의 신고 이력과 정지 이력을 보고 판단할 일이지 목록에서 이름만 보고 누를 일이
 * 아니기 때문입니다.
 */
@RestController
@RequestMapping("/api/v1/admin/suspensions")
@RequiredArgsConstructor
public class AdminSuspensionController {

    private final AdminSuspensionQueryService adminSuspensionQueryService;

    @GetMapping
    public AdminSuspensionListResponse list() {
        return adminSuspensionQueryService.list();
    }
}
