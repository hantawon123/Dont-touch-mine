package com.ssafy.d205.domain.feedback.service;

import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import com.ssafy.d205.domain.feedback.dto.FeedbackListResponse;
import com.ssafy.d205.domain.feedback.repository.UserFeedbackRepository;

/**
 * 운영자가 피드백을 읽습니다.
 *
 * <p>{@link FeedbackService} 와 나눈 이유는 신고에서와 같습니다. 접수는 게임
 * 클라이언트가 인증 없이 부르고 이쪽은 로그인한 운영자만 부릅니다. 한 서비스에 두면
 * "이 메서드는 누가 부를 수 있는가"를 매번 따져야 합니다.
 */
@Service
@RequiredArgsConstructor
public class FeedbackReadService {

    /** 개수를 주지 않았을 때. 한 화면에서 훑어볼 만한 양입니다. */
    public static final int DEFAULT_LIMIT = 50;

    /**
     * 한 번에 받을 수 있는 최대. 이보다 크게 요청하면 이 값으로 깎습니다.
     *
     * <p>거절하지 않고 깎는 이유는, 운영자가 200 을 넘겨 부르는 상황은 "다 보고 싶다"
     * 이고 그때 400 을 주면 답이 되지 않기 때문입니다. 상한 자체를 두는 이유는 피드백이
     * 지워지지 않고 쌓이기만 해서, 상한이 없으면 응답이 시간에 비례해 자라기
     * 때문입니다.
     */
    public static final int MAX_LIMIT = 200;

    private final UserFeedbackRepository userFeedbackRepository;

    @Transactional(readOnly = true)
    public FeedbackListResponse recent(Integer limit) {
        int size = limit == null ? DEFAULT_LIMIT : Math.min(Math.max(limit, 1), MAX_LIMIT);

        return new FeedbackListResponse(
                userFeedbackRepository.findRecent(size).stream()
                        .map(row -> new FeedbackListResponse.FeedbackItem(
                                row.getId(),
                                row.getAuthorUserId(),
                                row.getAuthorNickname(),
                                row.getMessage(),
                                row.getBuildVer(),
                                row.getPlatform(),
                                row.getCreatedAt()))
                        .toList());
    }
}
