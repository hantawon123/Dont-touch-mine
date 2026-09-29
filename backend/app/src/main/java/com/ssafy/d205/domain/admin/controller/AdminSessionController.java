package com.ssafy.d205.domain.admin.controller;

import org.springframework.security.core.Authentication;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

/**
 * 관리자 세션.
 *
 * <p>로그인과 로그아웃은 여기 없습니다. Spring Security 의 필터가 POST /session 과
 * POST /logout 을 직접 받습니다. 손으로 로그인 컨트롤러를 쓰면 SecurityContext 를
 * 세션에 저장하는 단계를 빠뜨리기 쉬운데, 그러면 <b>로그인은 200 인데 다음 요청이
 * 401</b> 이 됩니다. 필터에 맡기면 그 함정 자체가 없습니다.
 *
 * <p>그래서 이 컨트롤러에는 조회 하나만 있습니다.
 */
@RestController
@RequestMapping("/api/v1/admin")
public class AdminSessionController {

    /**
     * 지금 로그인한 관리자.
     *
     * <p>화면이 새로고침될 때 세션이 아직 살아 있는지 물어보는 자리입니다. 살아 있으면
     * 이름을 주고, 아니면 이 메서드에 닿기 전에 401 입니다.
     */
    @GetMapping("/session")
    public AdminSession current(Authentication authentication) {
        return new AdminSession(authentication.getName());
    }

    /** @param username 로그인한 관리자의 이름. 신고를 누가 처리했는지 적을 때 씁니다. */
    public record AdminSession(String username) {
    }
}
