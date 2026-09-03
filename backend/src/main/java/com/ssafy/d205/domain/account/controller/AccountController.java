package com.ssafy.d205.domain.account.controller;

import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PatchMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestHeader;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import com.ssafy.d205.domain.account.dto.AccountResponse;
import com.ssafy.d205.domain.account.dto.IssueAccountRequest;
import com.ssafy.d205.domain.account.dto.IssuedAccount;
import com.ssafy.d205.domain.account.dto.UpdateNicknameRequest;
import com.ssafy.d205.domain.account.service.AccountService;
import com.ssafy.d205.domain.user.entity.User;

@RestController
@RequestMapping("/api/v1/accounts")
@RequiredArgsConstructor
public class AccountController {

    /**
     * 호출자를 알아내는 헤더입니다.
     *
     * <p><b>이것은 인증이 아니라 식별입니다.</b> 여기 들어가는 값은 users.public_id이고
     * 그건 공개 식별자입니다. 남의 id를 아는 사람은 그 계정으로 요청할 수 있습니다.
     * 지금 단계에서 감수하는 것이고, 실제 인증(토큰)이 붙으면 이 헤더만 바뀝니다.
     */
    private static final String USER_ID_HEADER = "X-User-Id";

    private final AccountService accountService;

    /**
     * 계정 발급. 멱등합니다.
     *
     * <p>새로 만들었으면 201, 이미 있던 계정을 돌려줬으면 200입니다. 클라이언트가
     * "첫 실행인지"를 이 상태 코드로 구분할 수 있습니다.
     */
    @PostMapping
    public ResponseEntity<AccountResponse> issue(@Valid @RequestBody IssueAccountRequest request) {
        IssuedAccount issued = accountService.issue(request.deviceId());
        return ResponseEntity
                .status(issued.created() ? HttpStatus.CREATED : HttpStatus.OK)
                .body(issued.account());
    }

    @GetMapping("/me")
    public AccountResponse me(@RequestHeader(USER_ID_HEADER) String userId) {
        return accountService.get(userId);
    }

    @PatchMapping("/me")
    public AccountResponse rename(@RequestHeader(USER_ID_HEADER) String userId,
                                  @Valid @RequestBody UpdateNicknameRequest request) {
        return accountService.rename(userId, request.nickname());
    }
}
