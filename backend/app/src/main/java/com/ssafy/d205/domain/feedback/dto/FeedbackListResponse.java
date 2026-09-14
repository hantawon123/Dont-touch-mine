package com.ssafy.d205.domain.feedback.dto;

import java.util.List;

/**
 * @param feedback 최근 피드백. 없으면 빈 배열입니다.
 *                 <p>배열을 그대로 본문으로 주지 않고 감쌌습니다. 신고 목록과 같은
 *                 이유입니다 - 나중에 필드를 늘려야 할 때 응답 형태가 바뀌면 화면이
 *                 깨지는데, 감싸두면 필드만 더하면 됩니다.
 */
public record FeedbackListResponse(
        List<FeedbackItem> feedback
) {

    /**
     * @param id        피드백 한 건의 식별자. 건별 숨김·삭제가 이 값을 씁니다.
     * @param userId    쓴 사람의 공개 식별자. <b>탈퇴했으면 null 입니다.</b>
     * @param nickname  쓴 사람의 닉네임. 탈퇴했으면 null 입니다. userId 와 함께 비므로
     *                  화면은 둘 중 하나만 검사하면 됩니다.
     * @param message   본문.
     * @param buildVer  어느 빌드에서 왔나. 클라이언트가 안 보냈으면 null 입니다.
     * @param platform  실행 환경. 클라이언트가 안 보냈으면 null 입니다.
     * @param createdAt 보낸 시각. yyyyMMddHHmmss, UTC.
     */
    public record FeedbackItem(
            Integer id,
            String userId,
            String nickname,
            String message,
            String buildVer,
            String platform,
            String createdAt
    ) {
    }
}
