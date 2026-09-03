package com.ssafy.d205.domain.friend.controller;

import lombok.RequiredArgsConstructor;
import org.springframework.http.HttpStatus;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RequestHeader;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;

import com.ssafy.d205.domain.friend.service.FriendshipService;
import com.ssafy.d205.domain.user.entity.User;

/**
 * 성립된 친구 관계를 다룹니다.
 *
 * <p>목록 조회(GET)는 접속 상태와 함께 돌려줘야 해서 S15P21D205-432 에서 붙입니다.
 * 지금은 삭제만 있습니다.
 */
@RestController
@RequestMapping("/api/v1/friends")
@RequiredArgsConstructor
public class FriendController {

    private static final String USER_ID_HEADER = "X-User-Id";

    private final FriendshipService friendshipService;

    /** 친구 끊기. 관계 행을 지우므로 나중에 다시 요청할 수 있습니다. */
    @DeleteMapping("/{userId}")
    @ResponseStatus(HttpStatus.NO_CONTENT)
    public void unfriend(@RequestHeader(USER_ID_HEADER) String callerUserId,
                         @PathVariable String userId) {
        friendshipService.unfriend(callerUserId, userId);
    }
}
