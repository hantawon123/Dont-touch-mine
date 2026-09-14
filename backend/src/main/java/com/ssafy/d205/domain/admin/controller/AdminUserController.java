package com.ssafy.d205.domain.admin.controller;

import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;
import lombok.RequiredArgsConstructor;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import com.ssafy.d205.domain.admin.dto.AdminUserDetail;
import com.ssafy.d205.domain.admin.dto.AdminUserListResponse;
import com.ssafy.d205.domain.admin.service.AccountSuspensionService;
import com.ssafy.d205.domain.admin.service.AdminUserQueryService;

/**
 * 운영자의 사용자 조회와 계정 제재.
 *
 * <p><b>이 경로는 SecurityConfig 의 adminChain 이 로그인을 요구합니다.</b>
 * {@code /api/v1/admin/**} 전체가 그 체인에 잡히므로 메서드마다 권한을 적지 않습니다.
 * 반대로 이 경로 밖에 제재 기능을 만들면 아무나 부를 수 있습니다.
 *
 * <p>신고를 다루는 {@code AdminReportController} 와 나눈 이유는 자원이 다르기
 * 때문입니다. 그쪽은 신고 기록을 검토하고 숨기고 지웁니다. 신고를 완전 삭제해도 정지는
 * 남아야 하는데, 한 컨트롤러에 두면 그 경계가 흐려집니다.
 *
 * <p>조회는 S15P21D205-972 에서 생겼습니다. 그 전에는 정지 버튼이 신고 목록 안에만 있어서
 * 신고가 없는 사람은 운영자가 찾을 길이 없었습니다.
 */
@RestController
@RequestMapping("/api/v1/admin/users")
@RequiredArgsConstructor
public class AdminUserController {

    private final AccountSuspensionService accountSuspensionService;
    private final AdminUserQueryService adminUserQueryService;

    /**
     * 사용자를 찾습니다. 닉네임 부분 일치(대소문자 구분) 또는 userId 정확 일치.
     *
     * <p>{@code q} 를 비우면 최근 가입순입니다. 탭을 열자마자 빈 화면이 아니라 사람이 보이게
     * 하려는 것입니다. {@code limit} 은 1~50 이고 벗어나면 50 입니다.
     *
     * <p>게임 클라이언트의 유저 검색(정확 일치, searchable 존중)과 규칙이 다릅니다. 운영자는
     * 검색을 꺼 둔 사람도 찾아야 하므로, 이 조회는 반드시 이 경로(관리자 세션) 뒤에 있어야
     * 합니다.
     */
    @GetMapping
    public AdminUserListResponse search(@RequestParam(required = false) String q,
                                        @RequestParam(required = false) Integer limit) {
        return adminUserQueryService.search(q, limit);
    }

    /**
     * 사용자 한 명의 요약과 받은 신고, 한 신고, 보낸 피드백.
     *
     * <p>없는 계정은 404 TARGET_NOT_FOUND 입니다.
     */
    @GetMapping("/{userId}")
    public AdminUserDetail detail(@PathVariable String userId) {
        return adminUserQueryService.detail(userId);
    }

    /**
     * 계정을 정지합니다.
     *
     * <p>PUT 입니다. 같은 요청을 두 번 보내면 같은 상태가 되는 일이라 POST 보다 맞습니다.
     * 사유를 고쳐 적으려고 다시 부르는 경우도 그대로 처리됩니다.
     *
     * @return 이번 요청이 새로 정지했는지. false 면 이미 정지돼 있었다는 뜻입니다.
     */
    @PutMapping("/{userId}/suspension")
    public SuspensionResult suspend(@PathVariable String userId,
                                    @Valid @RequestBody SuspendRequest request) {
        return new SuspensionResult(accountSuspensionService.suspend(userId, request.reason()));
    }

    /**
     * 정지를 해제합니다. 정지 상태가 아니어도 200 입니다.
     *
     * @return 이번 요청이 실제로 해제했는지. false 면 이미 정상이었다는 뜻입니다.
     */
    @DeleteMapping("/{userId}/suspension")
    public SuspensionResult lift(@PathVariable String userId) {
        return new SuspensionResult(accountSuspensionService.lift(userId));
    }

    /**
     * @param reason 정지 사유. 필수입니다. 나중에 이 정지를 본 사람이 해제해도 되는지
     *               판단할 유일한 근거라, 비워둘 수 있게 하면 그 판단이 불가능해집니다.
     *               길이는 users.suspended_reason 컬럼과 같은 200 자입니다.
     */
    public record SuspendRequest(
            @NotBlank(message = "reason은 필수입니다.")
            @Size(max = 200, message = "reason은 200자를 넘을 수 없습니다.")
            String reason
    ) {
    }

    /** @param changed 이번 요청이 상태를 바꿨는지. false 면 이미 그 상태였습니다. */
    public record SuspensionResult(boolean changed) {
    }
}
