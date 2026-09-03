package com.ssafy.d205.user;

import java.util.List;

/**
 * @param users 검색 결과. 없으면 빈 배열입니다.
 *              <p>배열을 그대로 응답 본문으로 주지 않고 객체로 감쌌습니다. 나중에
 *              페이징 정보(다음 커서, 전체 개수)를 붙일 때 응답 형태가 바뀌면
 *              클라이언트가 깨지는데, 감싸두면 필드만 늘리면 됩니다.
 */
public record UserSearchResponse(
        List<UserSummary> users
) {
}
