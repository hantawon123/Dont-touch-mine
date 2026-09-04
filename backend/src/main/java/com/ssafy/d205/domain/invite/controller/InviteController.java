package com.ssafy.d205.domain.invite.controller;

import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.http.HttpStatus;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestHeader;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;

import com.ssafy.d205.domain.invite.dto.InviteListResponse;
import com.ssafy.d205.domain.invite.dto.SendInviteRequest;
import com.ssafy.d205.domain.invite.service.InviteService;

/**
 * 방 초대.
 *
 * <p>서버는 실시간 통신을 하지 않으므로 초대를 밀어주지 못합니다. 받는 쪽이 로비에
 * 있는 동안 주기적으로 조회해서 가져갑니다.
 *
 * <p>초대는 <b>3분 뒤 사라집니다.</b> 방 코드는 그 방이 없어지면 죽은 값인데 서버는
 * Photon 의 방 목록을 몰라서 방이 사라진 것을 알 수 없습니다. 대신 초대에 수명을
 * 둬서 오래된 코드로 입장을 시도하는 일을 줄입니다.
 */
@RestController
@RequestMapping("/api/v1/invites")
@RequiredArgsConstructor
public class InviteController {

    private static final String USER_ID_HEADER = "X-User-Id";

    private final InviteService inviteService;

    /**
     * 친구를 방으로 부릅니다.
     *
     * <p>응답 본문이 없습니다. 클라이언트가 돌려받아 쓸 것이 없고, 결과는 상대의 받은
     * 초대 목록에 나타나는 것으로 드러납니다.
     *
     * <p>같은 사람을 같은 방으로 다시 불러도 201 입니다. 새 행이 생기지는 않고 만료
     * 시계만 처음부터 다시 갑니다. 두 번 눌렀다고 초대가 둘이 되지는 않습니다.
     */
    @PostMapping
    @ResponseStatus(HttpStatus.CREATED)
    public void send(@RequestHeader(USER_ID_HEADER) String userId,
                     @Valid @RequestBody SendInviteRequest request) {
        inviteService.send(userId, request.userId(), request.roomCode());
    }

    /** 내가 받은 초대. 만료된 것은 빠집니다. */
    @GetMapping
    public InviteListResponse inbox(@RequestHeader(USER_ID_HEADER) String userId) {
        return inviteService.listInbox(userId);
    }

    /**
     * 받은 초대 없애기. 거절할 때와 입장한 뒤 정리할 때 둘 다 이것입니다.
     *
     * <p>경로가 부른 사람의 id 입니다. 초대에는 따로 식별자를 두지 않았습니다 — 화면이
     * 목록에서 고르는 것은 사람이고, 같은 사람이 여러 방으로 불렀다면 함께 정리하는
     * 편이 화면과 맞습니다.
     *
     * <p>없는 초대를 지워도 204 입니다. DELETE 는 멱등해야 하고, 이미 만료돼 사라진
     * 초대를 거절하는 것도 원하는 결과가 달성된 상태입니다.
     */
    @DeleteMapping("/{userId}")
    @ResponseStatus(HttpStatus.NO_CONTENT)
    public void decline(@RequestHeader(USER_ID_HEADER) String callerUserId,
                        @PathVariable String userId) {
        inviteService.decline(callerUserId, userId);
    }
}
