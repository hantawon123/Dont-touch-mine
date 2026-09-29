package com.ssafy.d205.domain.feedback.service;

import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import com.ssafy.d205.domain.feedback.dto.SendFeedbackRequest;
import com.ssafy.d205.domain.feedback.entity.UserFeedback;
import com.ssafy.d205.domain.feedback.repository.UserFeedbackRepository;
import com.ssafy.d205.domain.user.entity.User;
import com.ssafy.d205.domain.user.repository.UserRepository;
import com.ssafy.d205.global.common.TimeProvider;
import com.ssafy.d205.global.exception.UnknownCallerException;

/**
 * 피드백을 받아 적습니다. 그것뿐입니다.
 *
 * <p>신고와 같은 자리입니다. 자동 조치가 없고, 보낸 사람에게 답이 가지 않으며, 어느
 * 화면도 이 기록을 다시 읽지 않습니다. 그래서 여기에는 중복 검사도, 횟수 제한도
 * 없습니다. 같은 말을 열 번 보내면 열 건이 쌓이는데, <b>그 횟수 자체가 운영자에게
 * 신호</b>이기 때문입니다.
 *
 * <p>남용의 대가가 작다는 판단이 그 뒤에 있습니다. 한 건이 500자이고 아무 동작도
 * 일으키지 않으므로, 쏟아 넣어도 생기는 일은 목록이 지저분해지는 것입니다. 실제로
 * 문제가 되면 수집 API 가 쓰는 IpRateLimiter 를 붙이면 됩니다.
 */
@Service
@RequiredArgsConstructor
public class FeedbackService {

    private final UserFeedbackRepository userFeedbackRepository;
    private final UserRepository userRepository;
    private final TimeProvider timeProvider;

    @Transactional
    public void submit(String callerUserId, SendFeedbackRequest request) {
        User me = userRepository.findByPublicId(callerUserId)
                .orElseThrow(() -> new UnknownCallerException(callerUserId));

        userFeedbackRepository.save(UserFeedback.of(
                me.getSeq(),
                // 앞뒤 공백을 떼고 저장합니다. 화면의 여러 줄 입력에서는 줄바꿈만 남은
                // 꼬리가 흔하고, 그것까지 500자에 세면 정작 쓴 글이 잘립니다.
                request.message().strip(),
                blankToNull(request.buildVer()),
                blankToNull(request.platform()),
                timeProvider.now()));
    }

    /**
     * 빈 문자열과 없음을 같게 다룹니다.
     *
     * <p>클라이언트가 비운 칸을 "" 로 보낼지 생략할지는 화면 사정이고, 저장된 뒤에는
     * 둘을 구분할 이유가 없습니다. 신고의 memo 와 같은 처리입니다.
     */
    private static String blankToNull(String value) {
        return value == null || value.isBlank() ? null : value.strip();
    }
}
