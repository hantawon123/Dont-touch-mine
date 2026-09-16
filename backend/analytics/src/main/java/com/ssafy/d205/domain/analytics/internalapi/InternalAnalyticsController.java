package com.ssafy.d205.domain.analytics.internalapi;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Pattern;
import jakarta.validation.constraints.Size;
import lombok.RequiredArgsConstructor;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import com.ssafy.d205.domain.analytics.query.AnalyticsQueryFilter;
import com.ssafy.d205.domain.analytics.query.AnalyticsQueryService;
import com.ssafy.d205.domain.analytics.query.AnalyticsTable;

/**
 * 관리 화면 분석 탭의 조회 (S15P21D205-976). 계정 서비스의 {@code /api/v1/admin/analytics/**} 가 관리자 세션을
 * 확인한 뒤 여기를 그대로 대신 부릅니다. 여기는 공유 키({@link InternalKeyFilter})만 봅니다.
 *
 * <p>질문 이름은 {@code docs/analytics-dashboards.md} 의 절 번호에 붙인 것이고, 그 목록은
 * {@code DashboardQueryDocument.SLUGS} 에 있습니다. 컬럼 이름이 한글 별칭 그대로인 이유는 화면이 헤더로
 * 쓰기 때문이고, 문서를 고치면 화면의 헤더도 같이 바뀝니다.
 */
@RestController
@RequestMapping("/internal/admin/analytics")
@RequiredArgsConstructor
public class InternalAnalyticsController {

    private static final String UTC_14 = "\\d{14}";

    private final AnalyticsQueryService queries;

    /**
     * 히트맵용 좌표. 한 경기의 위치 샘플을 시간순으로, 경기당 20,000 행까지.
     *
     * @param matchId 필수. 경기 전체 좌표를 한 번에 주는 경로는 없습니다 - 행 수가 경기 수에 비례해 커집니다
     */
    @GetMapping("/positions")
    public AnalyticsTable positions(@RequestParam @NotBlank @Size(max = 64) String matchId) {
        return queries.positions(matchId);
    }

    /**
     * @param question matches, hiding-time, hideouts, dwell, seeking-time, combat, item-life, dropout
     * @param from     UTC yyyyMMddHHmmss. 이 시각 이후(포함)에 시작한 경기만
     * @param to       UTC yyyyMMddHHmmss. 이 시각 전(미포함)에 시작한 경기만
     * @param matchId  이 경기만
     */
    @GetMapping("/{question}")
    public AnalyticsTable question(@PathVariable String question,
                                   @RequestParam(required = false)
                                   @Pattern(regexp = UTC_14, message = "from 은 UTC yyyyMMddHHmmss 14자여야 합니다.")
                                   String from,
                                   @RequestParam(required = false)
                                   @Pattern(regexp = UTC_14, message = "to 는 UTC yyyyMMddHHmmss 14자여야 합니다.")
                                   String to,
                                   @RequestParam(required = false) @Size(max = 64) String matchId) {
        return queries.question(question, AnalyticsQueryFilter.parse(from, to, matchId));
    }
}
