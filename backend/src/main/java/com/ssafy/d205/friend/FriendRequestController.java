package com.ssafy.d205.friend;

import jakarta.validation.Valid;
import jakarta.validation.constraints.Pattern;
import lombok.RequiredArgsConstructor;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestHeader;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;

/**
 * 대기 중인 친구 요청을 다룹니다. 성립된 관계는 /api/v1/friends 쪽입니다.
 * 같은 friendships 행이지만 상태가 달라 자원을 나눴습니다.
 */
@RestController
@RequestMapping("/api/v1/friend-requests")
@RequiredArgsConstructor
public class FriendRequestController {

    private static final String USER_ID_HEADER = "X-User-Id";

    private final FriendshipService friendshipService;

    /**
     * 요청 보내기.
     *
     * <p>상대가 이미 나에게 요청을 보내둔 상태면 응답의 status 가 ACCEPTED 입니다.
     * 그때는 요청이 아니라 친구가 된 것입니다.
     */
    @PostMapping
    public ResponseEntity<SendFriendRequestResponse> send(
            @RequestHeader(USER_ID_HEADER) String userId,
            @Valid @RequestBody SendFriendRequestRequest request) {
        SendFriendRequestResponse response = friendshipService.send(userId, request.userId());

        // 자동 수락은 새 요청을 만든 것이 아니라 상대가 보내둔 요청을 성립시킨 것이므로
        // 201 이 아닙니다. 상태 코드로도 "요청을 보냈다"와 "친구가 됐다"가 갈립니다.
        return ResponseEntity
                .status(response.status() == FriendshipStatus.ACCEPTED ? HttpStatus.OK : HttpStatus.CREATED)
                .body(response);
    }

    /** 대기 중인 요청 목록. 기본은 받은 요청입니다. */
    @GetMapping
    public FriendRequestListResponse list(
            @RequestHeader(USER_ID_HEADER) String userId,

            @Pattern(regexp = "incoming|outgoing",
                    message = "direction은 incoming 또는 outgoing이어야 합니다.")
            @RequestParam(defaultValue = "incoming") String direction
    ) {
        return friendshipService.list(userId, direction);
    }

    /** 받은 요청 수락. */
    @PostMapping("/{userId}/accept")
    @ResponseStatus(HttpStatus.NO_CONTENT)
    public void accept(@RequestHeader(USER_ID_HEADER) String callerUserId,
                       @PathVariable String userId) {
        friendshipService.accept(callerUserId, userId);
    }

    /**
     * 요청 없애기. 받은 쪽이 부르면 거절, 보낸 쪽이 부르면 취소입니다.
     * 둘이 같은 연산이라 엔드포인트도 하나입니다.
     */
    @DeleteMapping("/{userId}")
    @ResponseStatus(HttpStatus.NO_CONTENT)
    public void delete(@RequestHeader(USER_ID_HEADER) String callerUserId,
                       @PathVariable String userId) {
        friendshipService.deleteRequest(callerUserId, userId);
    }
}
