package com.ssafy.d205.domain.friend.dto;

import java.util.List;

/**
 * @param blocked 내가 차단한 사람들. 없으면 빈 배열입니다.
 *                <p>내가 차단당한 목록은 주지 않습니다. 그걸 알려주면 누가 나를
 *                차단했는지 드러나고, 차단이 조용히 적용된다는 성질이 깨집니다.
 */
public record BlockListResponse(
        List<BlockedUserSummary> blocked
) {
}
