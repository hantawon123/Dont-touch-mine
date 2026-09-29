package com.ssafy.d205.domain.report.controller;

import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.http.HttpStatus;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestHeader;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;

import com.ssafy.d205.domain.report.dto.SendReportRequest;
import com.ssafy.d205.domain.report.service.ReportService;

/**
 * 사용자 신고.
 *
 * <p>보내는 것만 있습니다. 자기가 낸 신고를 조회하는 API 도, 취소하는 API 도 두지
 * 않았습니다. 신고는 상대에게 아무 영향이 없고 결과가 화면에 나타나지 않으므로,
 * 클라이언트가 다시 읽을 이유가 없습니다.
 *
 * <p>같은 사람을 여러 번 신고해도 그때마다 새 기록이 됩니다. 횟수가 운영자에게 신호가
 * 되기 때문인데, 그래서 화면이 "이미 신고했습니다" 같은 상태를 보여줄 수는 없습니다.
 * 서버가 그 상태를 알려주지 않습니다.
 */
@RestController
@RequestMapping("/api/v1/reports")
@RequiredArgsConstructor
public class ReportController {

    private static final String USER_ID_HEADER = "X-User-Id";

    private final ReportService reportService;

    /**
     * 신고하기.
     *
     * <p>본문이 없는 201 입니다. 만들어진 기록을 가리킬 곳이 없어서 Location 도 두지
     * 않습니다. 클라이언트가 할 일은 "신고했습니다"를 띄우는 것뿐입니다.
     */
    @PostMapping
    @ResponseStatus(HttpStatus.CREATED)
    public void report(@RequestHeader(USER_ID_HEADER) String callerUserId,
                       @Valid @RequestBody SendReportRequest request) {
        reportService.report(callerUserId, request);
    }
}
