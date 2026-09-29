package com.ssafy.d205.domain.feedback.service;

import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import com.ssafy.d205.domain.feedback.repository.UserFeedbackRepository;
import com.ssafy.d205.global.common.TimeProvider;

/**
 * 운영자가 피드백을 치웁니다 (S15P21D205-900).
 *
 * <p>{@link FeedbackReadService} 와 나눈 이유는 {@code ReportReviewService} 를
 * {@code ReportService} 와 나눈 것과 같습니다. 읽기만 하는 쪽과 지우는 쪽이 한 클래스에
 * 있으면 "이 메서드는 누가 부를 수 있는가"를 매번 따져야 합니다. 여기 있는 것은 전부
 * 로그인한 운영자만 부릅니다.
 *
 * <p><b>피드백에는 사람 단위 동작이 없습니다.</b> 신고는 "이 사람이 몇 번 신고당했나"가
 * 판단 단위라 사람 단위로 치우는 것이 자연스럽지만, 피드백 목록은 사람으로 묶여 있지
 * 않고 한 건 한 건이 읽을 내용입니다. 같은 사람의 피드백을 한 번에 치울 이유가 없습니다.
 */
@Service
@RequiredArgsConstructor
public class FeedbackCleanupService {

    private final UserFeedbackRepository userFeedbackRepository;
    private final TimeProvider timeProvider;

    /**
     * 피드백 한 건을 목록에서 치웁니다. 행은 남습니다.
     *
     * <p>없는 번호를 줘도 성공입니다. 두 사람이 같은 화면을 보다가 둘 다 눌렀을 때 뒤에
     * 누른 쪽이 받는 답이고, 원하는 결과는 이미 이루어져 있습니다.
     *
     * @return 이번에 치웠으면 true, 이미 없거나 숨겨져 있었으면 false
     */
    @Transactional
    public boolean hide(Integer feedbackId) {
        return userFeedbackRepository.findById(feedbackId)
                .filter(feedback -> feedback.getDeletedAt() == null)
                .map(feedback -> {
                    feedback.hide(timeProvider.now());
                    return true;
                })
                .orElse(false);
    }

    /**
     * 피드백 한 건을 지웁니다. <b>되돌릴 수 없습니다.</b>
     *
     * <p>내용이 사라집니다. 스팸이 아니라 읽기 싫은 지적이었다면 그것도 함께 사라지므로,
     * 눈앞에서 치우는 것이 목적이라면 {@link #hide} 가 맞습니다.
     *
     * <p>없는 번호를 줘도 성공입니다. {@link #hide} 와 같은 이유입니다.
     *
     * @return 이번에 지웠으면 true, 이미 없었으면 false
     */
    @Transactional
    public boolean purge(Integer feedbackId) {
        if (!userFeedbackRepository.existsById(feedbackId)) {
            return false;
        }

        userFeedbackRepository.deleteById(feedbackId);
        return true;
    }
}
