package com.ssafy.d205.domain.user.controller;

import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PatchMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestHeader;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;

import com.ssafy.d205.domain.user.dto.AccountResponse;
import com.ssafy.d205.domain.user.dto.IssueAccountRequest;
import com.ssafy.d205.domain.user.dto.IssuedAccount;
import com.ssafy.d205.domain.user.dto.UpdateAppearanceRequest;
import com.ssafy.d205.domain.user.dto.UpdateNicknameRequest;
import com.ssafy.d205.domain.user.dto.UpdateSearchableRequest;
import com.ssafy.d205.domain.user.service.AccountService;

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

    /**
     * 파괴적인 연산에서만 요구하는 자격증명 헤더입니다.
     *
     * <p>X-User-Id 는 공개 식별자라 인증이 아닙니다. 계정 삭제는 되돌릴 수 없으므로
     * 실제 자격증명인 기기 식별자를 함께 확인합니다.
     */
    private static final String DEVICE_ID_HEADER = "X-Device-Id";

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

    /**
     * 닉네임 검색에 나올지 정합니다.
     *
     * <p>PUT 인 이유는 멱등해야 하기 때문입니다. 이미 꺼 둔 것을 또 끄는 것은
     * "꺼진 상태로 두라"는 요청이 이미 달성된 상황이라 오류가 아닙니다.
     *
     * <p>닉네임 변경(PATCH /me)에 합치지 않은 이유가 둘입니다. 합치려면 nickname 이
     * 필수에서 선택이 되어야 하는데, 그러면 <b>빈 요청이 지금의 400 대신 조용한 200</b>
     * 이 됩니다. 클라이언트에서 닉네임 변수가 비었을 때 화면은 성공이라고 뜨고 아무것도
     * 바뀌지 않아, 원인을 찾기 어렵습니다.
     *
     * <p>다른 하나는 닉네임이 중복이라 409 가 날 때 이 설정까지 함께 롤백된다는
     * 것입니다. 사용자는 체크박스만 껐는데 그것도 안 먹힙니다.
     *
     * <p>바뀐 계정을 그대로 돌려줍니다. 화면이 응답으로 체크박스를 다시 그리면 되고,
     * 따로 조회할 필요가 없습니다.
     */
    @PutMapping("/me/searchable")
    public AccountResponse setSearchable(@RequestHeader(USER_ID_HEADER) String userId,
                                         @Valid @RequestBody UpdateSearchableRequest request) {
        return accountService.setSearchable(userId, request.searchable());
    }

    /**
     * 옷장에서 고른 외형을 저장합니다. 파츠 넷이 모두 필수입니다.
     *
     * <p>PUT 인 이유는 검색 허용 설정과 같습니다. 처음 저장과 덮어쓰기가 한 요청이고, 같은 값을
     * 다시 보내도 성공입니다.
     *
     * <p>서버는 파츠 id 의 뜻을 모릅니다. 형식만 보고 그대로 저장하며, 클라이언트가 모르는
     * id 를 돌려받으면 기본 파츠로 대체해야 합니다.
     *
     * <p>바뀐 계정을 그대로 돌려줍니다. 화면이 응답으로 캐릭터를 다시 그리면 됩니다.
     */
    @PutMapping("/me/appearance")
    public AccountResponse setAppearance(@RequestHeader(USER_ID_HEADER) String userId,
                                         @Valid @RequestBody UpdateAppearanceRequest request) {
        return accountService.setAppearance(userId, request);
    }

    /**
     * 외형을 초기화합니다. "아직 고르지 않은" 상태로 돌아가고 appearance 는 null 이 됩니다.
     *
     * <p>클라이언트가 기본 파츠로 PUT 하는 것과 다릅니다. 그러면 기본 파츠가 나중에 바뀌어도
     * 초기화한 사람은 옛 값에 남습니다. 지우면 그때의 기본값이 쓰입니다.
     *
     * <p>이미 없는 것을 지워도 200 입니다. 초기화 버튼을 두 번 눌러도 오류가 아닙니다.
     * 204 가 아니라 계정을 돌려주는 것은 다른 설정 변경과 같은 모양을 유지해 화면이 응답으로
     * 다시 그릴 수 있게 하기 위해서입니다.
     */
    @DeleteMapping("/me/appearance")
    public AccountResponse clearAppearance(@RequestHeader(USER_ID_HEADER) String userId) {
        return accountService.clearAppearance(userId);
    }

    /**
     * 계정 삭제. 되돌릴 수 없습니다.
     *
     * <p>경로를 /me 로 두는 이유는 <b>남을 지정하는 길을 막기 위해서</b>입니다.
     * /{userId} 로 만들면 남의 계정을 대상으로 부를 수 있게 됩니다.
     *
     * <p>자격증명을 헤더로 받습니다. DELETE 에 본문을 실으면 프록시나 클라이언트에서
     * 걸리는 경우가 있고, UnityWebRequest 로 DELETE + 본문은 번거롭습니다. 자격증명을
     * 헤더로 보내는 것은 Authorization 과 같은 관례입니다. 쿼리 파라미터는 안 됩니다 —
     * nginx 접근 로그에 자격증명이 남습니다.
     *
     * <p><b>감사 기록을 남기지 않습니다.</b> 계정이 사라지면 "언제 누가 탈퇴했나"를 알
     * 방법이 없습니다. 의도한 것입니다 — public_id 만 남겨도 그건 식별자라 "삭제했다"는
     * 약속과 상충합니다. 탈퇴자 수 같은 집계가 필요해지면 날짜와 개수만 남기는 별도
     * 테이블을 검토해야 하고, 식별자를 남기는 방식은 안 됩니다.
     *
     * <p>두 번 부르면 404 입니다. DELETE 가 멱등해야 한다는 원칙에 어긋나 보이지만,
     * 삭제된 계정으로는 자격증명을 확인할 수 없으므로 호출자 자체가 없습니다.
     */
    @DeleteMapping("/me")
    @ResponseStatus(HttpStatus.NO_CONTENT)
    public void delete(@RequestHeader(USER_ID_HEADER) String userId,
                       @RequestHeader(DEVICE_ID_HEADER) String deviceId) {
        accountService.delete(userId, deviceId);
    }
}
