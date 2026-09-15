package com.ssafy.d205.domain.admin.controller;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Pattern;
import jakarta.validation.constraints.Size;
import lombok.RequiredArgsConstructor;
import org.springframework.util.LinkedMultiValueMap;
import org.springframework.util.MultiValueMap;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import com.ssafy.d205.domain.admin.dto.AdminAnalyticsTable;
import com.ssafy.d205.domain.ops.service.AnalyticsInternalClient;

/**
 * 관리 화면 분석 탭의 조회 (S15P21D205-976). 관리자 세션만 확인하고 분석 서비스의 내부 API 를 그대로 대신
 * 부릅니다.
 *
 * <p>브라우저가 분석 서비스를 직접 부르지 않는 이유는 관리자 세션이 이 서비스에만 있고, 분석 서비스의
 * 내부 경로는 nginx 에 노출하지 않기 때문입니다. 질문 이름과 필터는 검사만 하고 해석하지 않습니다 -
 * 질문 목록은 분석 서비스가 {@code docs/analytics-dashboards.md} 에서 읽습니다.
 *
 * <p><b>이 경로는 SecurityConfig 의 adminChain 이 로그인을 요구합니다.</b> 분석 서비스가 죽어 있으면 200 에
 * {@code unavailable: true} 와 빈 표입니다. 개요 탭의 경기 통계 카드와 같은 태도로, 분석이 없다고 관리
 * 화면이 오류를 내면 서비스를 나눈 이유가 없습니다.
 */
@RestController
@RequestMapping("/api/v1/admin/analytics")
@RequiredArgsConstructor
public class AdminAnalyticsController {

    private static final String UTC_14 = "\\d{14}";

    private final AnalyticsInternalClient analytics;

    /** 히트맵용 좌표. 경기 하나, 최대 20,000 행. */
    @GetMapping("/positions")
    public AdminAnalyticsTable positions(@RequestParam @NotBlank @Size(max = 64) String matchId) {
        MultiValueMap<String, String> query = new LinkedMultiValueMap<>();
        query.add("matchId", matchId);
        return analytics.fetchTable("positions", query).orElseGet(AdminAnalyticsTable::whenUnavailable);
    }

    /**
     * @param question matches, hiding-time, hideouts, dwell, seeking-time, combat, item-life, dropout
     * @param from     UTC yyyyMMddHHmmss. 이 시각 이후(포함)에 시작한 경기만
     * @param to       UTC yyyyMMddHHmmss. 이 시각 전(미포함)에 시작한 경기만
     * @param matchId  이 경기만
     */
    @GetMapping("/{question}")
    public AdminAnalyticsTable question(@PathVariable
                                        @Pattern(regexp = "[a-z][a-z-]{0,31}", message = "질문 이름은 소문자와 하이픈입니다.")
                                        String question,
                                        @RequestParam(required = false)
                                        @Pattern(regexp = UTC_14, message = "from 은 UTC yyyyMMddHHmmss 14자여야 합니다.")
                                        String from,
                                        @RequestParam(required = false)
                                        @Pattern(regexp = UTC_14, message = "to 는 UTC yyyyMMddHHmmss 14자여야 합니다.")
                                        String to,
                                        @RequestParam(required = false) @Size(max = 64) String matchId) {
        MultiValueMap<String, String> query = new LinkedMultiValueMap<>();
        if (from != null && !from.isBlank()) {
            query.add("from", from);
        }
        if (to != null && !to.isBlank()) {
            query.add("to", to);
        }
        if (matchId != null && !matchId.isBlank()) {
            query.add("matchId", matchId);
        }
        return analytics.fetchTable(question, query).orElseGet(AdminAnalyticsTable::whenUnavailable);
    }
}
