package com.ssafy.d205.domain.admin.dto;

import java.util.List;

/**
 * 정지 탭이 한 번에 받는 것.
 *
 * <p>둘을 한 응답에 넣는 이유는 운영자가 같이 보기 때문입니다. 지금 정지된 목록만 보면 "왜
 * 풀렸나"에 답할 수 없고, 해제 이력만 보면 지금 누가 막혀 있는지 모릅니다.
 *
 * @param suspended   지금 정지된 계정. 정지 시각 최근순, 최대 200건
 * @param recentLifts 최근 해제 이력. 최신순, 최대 50건
 */
public record AdminSuspensionListResponse(
        List<AdminSuspendedAccount> suspended,
        List<AdminSuspensionEntry> recentLifts
) {
}
