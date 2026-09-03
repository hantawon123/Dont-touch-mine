package com.ssafy.d205.domain.friend.controller;

import lombok.RequiredArgsConstructor;
import org.springframework.http.HttpStatus;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestHeader;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;

import com.ssafy.d205.domain.friend.dto.BlockListResponse;
import com.ssafy.d205.domain.friend.service.FriendshipService;

/**
 * 사용자 차단.
 *
 * <p>차단은 <b>양방향으로 적용됩니다.</b> A가 B를 차단하면 B도 A를 검색하거나 요청을
 * 보낼 수 없습니다. 차단당한 쪽이 계속 요청을 보낼 수 있으면 차단의 의미가 없습니다.
 *
 * <p>다만 <b>저장은 방향이 있습니다.</b> A가 B를 차단한 것과 B가 A를 차단한 것은 별개
 * 행이고, 각자 자기 것만 해제할 수 있습니다.
 */
@RestController
@RequestMapping("/api/v1/blocks")
@RequiredArgsConstructor
public class BlockController {

    private static final String USER_ID_HEADER = "X-User-Id";

    private final FriendshipService friendshipService;

    /**
     * 차단하기.
     *
     * <p>PUT 인 이유는 멱등해야 하기 때문입니다. 이미 차단한 상대를 다시 차단하는 것은
     * "차단 상태로 두라"는 요청이 이미 달성된 상황이라 오류가 아닙니다.
     */
    @PutMapping("/{userId}")
    @ResponseStatus(HttpStatus.NO_CONTENT)
    public void block(@RequestHeader(USER_ID_HEADER) String callerUserId,
                      @PathVariable String userId) {
        friendshipService.block(callerUserId, userId);
    }

    /**
     * 차단 해제.
     *
     * <p>차단하지 않은 상대를 해제해도 204 입니다. DELETE 는 멱등해야 하고, 원하는
     * 결과(차단되지 않은 상태)가 이미 달성돼 있습니다.
     */
    @DeleteMapping("/{userId}")
    @ResponseStatus(HttpStatus.NO_CONTENT)
    public void unblock(@RequestHeader(USER_ID_HEADER) String callerUserId,
                        @PathVariable String userId) {
        friendshipService.unblock(callerUserId, userId);
    }

    /** 내가 차단한 목록. */
    @GetMapping
    public BlockListResponse list(@RequestHeader(USER_ID_HEADER) String callerUserId) {
        return friendshipService.listBlocked(callerUserId);
    }
}
