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
        return recent(limit, null);
    }

    /**
     * 최근 피드백. keyword 를 주면 본문에 그 말이 들어 있는 것만 (S15P21D205-1004).
     *
     * <p>keyword 가 비었거나 공백뿐이면 검색이 아니라 전체 목록입니다. 빈 검색에 400 을 주지 않는
     * 이유는 limit 과 같습니다 - 운영자가 검색칸을 비우고 누르는 것은 "다 보여 달라"입니다.
     *
     * <p>LIKE 의 특수문자(퍼센트·밑줄·백슬래시)는 글자 그대로 찾습니다. 이스케이프하지 않으면
     * "100%" 를 찾는 운영자가 전체 목록을 받고 왜 그런지 알 수 없습니다.
     */
    @Transactional(readOnly = true)
    public FeedbackListResponse recent(Integer limit, String keyword) {
        int size = limit == null ? DEFAULT_LIMIT : Math.min(Math.max(limit, 1), MAX_LIMIT);
        String trimmed = keyword == null ? "" : keyword.trim();

        var rows = trimmed.isEmpty()
                ? userFeedbackRepository.findRecent(size)
                : userFeedbackRepository.searchRecent("%" + escapeLike(trimmed) + "%", size);

        return new FeedbackListResponse(
                rows.stream()
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

    /** MySQL LIKE 의 기본 이스케이프 문자는 백슬래시입니다. 그것 자체도 이스케이프해야 합니다. */
    static String escapeLike(String raw) {
        return raw.replace("\\", "\\\\").replace("%", "\\%").replace("_", "\\_");
    }
}
