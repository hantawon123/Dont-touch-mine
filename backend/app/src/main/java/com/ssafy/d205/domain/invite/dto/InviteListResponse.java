package com.ssafy.d205.domain.invite.dto;

import java.util.List;

/**
 * @param invites 아직 살아있는 초대만. 없으면 빈 배열입니다.
 *                <p>배열을 그대로 주지 않고 감쌌습니다. 나중에 개수나 페이징을 붙일 때
 *                응답 형태가 바뀌면 클라이언트가 깨집니다.
 */
public record InviteListResponse(
        List<InviteSummary> invites
) {
}
