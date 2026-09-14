package com.ssafy.d205.domain.admin.controller;

import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.Authentication;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PatchMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import com.ssafy.d205.domain.report.dto.ReportDetail;
import com.ssafy.d205.domain.report.dto.ReportedUserListResponse;
import com.ssafy.d205.domain.report.dto.ReviewReportRequest;
import com.ssafy.d205.domain.report.entity.ReportStatus;
import com.ssafy.d205.domain.report.service.ReportReviewService;

/**
 * 운영자가 신고를 보고 마무리합니다.
 *
 * <p>경로가 /api/v1/admin 아래인 것이 곧 접근 규칙입니다. SecurityConfig 의 첫 체인이
 * 이 경로 전체에 로그인을 요구하므로, 여기 메서드마다 권한을 적을 필요가 없습니다.
 * 반대로 <b>이 경로 밖에 관리 기능을 만들면 아무나 부를 수 있습니다.</b>
 *
 * <p>신고를 접수하는 것은 /api/v1/reports 이고 인증이 없습니다. 게임 클라이언트가
 * 부르는 자리라 성격이 반대입니다.
 */
@RestController
@RequestMapping("/api/v1/admin/reports")
@RequiredArgsConstructor
public class AdminReportController {

    private final ReportReviewService reportReviewService;

    /**
     * 신고당한 사람 목록. 기본은 아직 보지 않은 것입니다.
     *
     * <p>신고 한 건씩이 아니라 <b>사람 단위로 묶어서</b> 돌려줍니다. 한 건씩 나열하면
     * 같은 사람에 대한 다섯 건이 흩어져 나와, 그 사람이 문제인지 신고한 사람이
     * 문제인지 구분되지 않습니다.
     *
     * <p>status 로 이미 검토한 것도 볼 수 있습니다. 지난 판단을 확인할 방법이 없으면
     * "그때 왜 기각했지"를 DB 를 직접 열어야 알 수 있습니다.
     */
    @GetMapping
    public ReportedUserListResponse list(
            @RequestParam(defaultValue = "PENDING") ReportStatus status) {
        return reportReviewService.list(status);
    }

    /**
     * 한 사람에 대한 신고를 하나씩. 목록의 사유 분포로 부족할 때 봅니다.
     *
     * <p>status 를 주면 그 상태만 봅니다. 미검토 목록에서 펼쳤는데 예전에 처리한 것까지
     * 섞여 나오면 지금 무엇을 판단해야 하는지가 흐려집니다.
     *
     * <p>비워 두면 전부 봅니다. "이 사람 그동안 어땠나"를 볼 때 씁니다.
     */
    @GetMapping("/{userId}")
    public ReportDetail.ListResponse detail(@PathVariable String userId,
                                            @RequestParam(required = false) ReportStatus status) {
        return reportReviewService.detail(userId, status);
    }

    /**
     * 그 사람의 미검토 신고를 한 번에 마무리합니다.
     *
     * <p>경로가 신고 번호가 아니라 사용자입니다. 운영자는 신고 한 건이 아니라 사람을
     * 보고 판단하므로, 다섯 건 쌓인 사람을 다섯 번 누르게 할 이유가 없습니다.
     *
     * <p>이미 검토한 신고는 그대로 둡니다. 검토 뒤에 새로 들어온 것만 미검토로 남아
     * 다음 차례에 다시 올라옵니다.
     *
     * <p>처리할 것이 없어도 200 입니다. 두 사람이 같은 화면을 보다가 둘 다 눌렀을 때
     * 뒤에 누른 쪽에게 오류를 주면 무엇이 잘못됐는지 알 수 없는데, 원하는 결과는 이미
     * 이루어져 있습니다. 응답의 reviewed 가 0 이면 그런 경우입니다.
     */
    @PatchMapping("/{userId}")
    public ReviewResult review(@PathVariable String userId,
                               @Valid @RequestBody ReviewReportRequest request,
                               Authentication authentication) {
        int reviewed = reportReviewService.review(
                userId, request.status(), authentication.getName());

        return new ReviewResult(reviewed);
    }

    /**
     * 그 사람의 신고를 목록에서 치웁니다. 행은 남습니다.
     *
     * <p><b>status 는 목록 조회와 같은 뜻이고, 같은 값을 주어야 합니다.</b> 화면은 상태로
     * 걸러 보여주므로 치우는 범위도 거기 맞춰야 합니다. 빼면 상태를 가리지 않고 전부
     * 치우는데, 그러면 ACTIONED 화면에서 "1건"을 보고 누른 한 번에 보지도 못한 PENDING
     * 신고까지 사라집니다. 목록과 달리 기본값을 두지 않은 이유입니다 - 여기서 PENDING 을
     * 기본으로 삼으면 이번에는 반대로 조용히 좁혀집니다.
     *
     * <p>검토 상태를 바꾸지 않습니다. 숨기는 것과 판단하는 것은 다른 일이고, 여기서
     * 임의로 DISMISSED 를 찍으면 운영자가 내리지 않은 판단이 기록에 남습니다.
     *
     * <p>PATCH 인 이유는 행의 한 필드를 바꾸는 것이기 때문입니다. DELETE 는 아래 완전
     * 삭제가 씁니다. 둘을 같은 메서드로 두면 되돌릴 수 있는 것과 없는 것이 요청만
     * 보고는 구분되지 않습니다.
     */
    @PatchMapping("/{userId}/hidden")
    public HideResult hide(@PathVariable String userId,
                           @RequestParam(required = false) ReportStatus status) {
        return new HideResult(reportReviewService.hide(userId, status));
    }

    /**
     * 그 사람의 신고를 지웁니다. <b>되돌릴 수 없습니다.</b>
     *
     * <p>status 의 뜻은 위와 같습니다. 이쪽은 되돌릴 수 없으므로 범위를 넓게 잡은 실수의
     * 대가가 더 큽니다.
     *
     * <p>범위 안의 숨긴 것까지 함께 지웁니다. 숨긴 것만 남으면 나중에 그 행들의 출처를
     * 아무도 설명하지 못합니다.
     */
    @DeleteMapping("/{userId}")
    public HideResult purge(@PathVariable String userId,
                            @RequestParam(required = false) ReportStatus status) {
        return new HideResult(reportReviewService.purge(userId, status));
    }

    /**
     * 신고 한 건을 목록에서 치웁니다.
     *
     * <p>경로에 {@code entries} 를 둔 이유는 위의 {@code /{userId}} 와 갈라놓기
     * 위해서입니다. 사용자는 UUID 로, 신고는 순번으로 가리키므로 같은 자리에 두면
     * 무엇을 받는 경로인지가 값의 모양에 달리게 됩니다.
     *
     * <p>없는 번호를 줘도 200 입니다. 두 사람이 같은 화면을 보다가 둘 다 눌렀을 때
     * 뒤에 누른 쪽에게 404 를 주면 무엇이 잘못됐는지 알 수 없는데, 원하는 결과는 이미
     * 이루어져 있습니다. 응답의 affected 가 0 이면 그런 경우입니다.
     */
    @PatchMapping("/entries/{reportId}/hidden")
    public HideResult hideEntry(@PathVariable Integer reportId) {
        return new HideResult(reportReviewService.hideEntry(reportId) ? 1 : 0);
    }

    /** 신고 한 건을 지웁니다. <b>되돌릴 수 없습니다.</b> 없는 번호도 200 입니다. */
    @DeleteMapping("/entries/{reportId}")
    public HideResult purgeEntry(@PathVariable Integer reportId) {
        return new HideResult(reportReviewService.purgeEntry(reportId) ? 1 : 0);
    }

    /** @param reviewed 이번 요청이 마무리한 건수. 0 이면 이미 처리돼 있었다는 뜻입니다. */
    public record ReviewResult(int reviewed) {
    }

    /** @param affected 이번 요청이 치우거나 지운 건수. 0 이면 이미 그렇게 돼 있었다는 뜻입니다. */
    public record HideResult(int affected) {
    }
}
