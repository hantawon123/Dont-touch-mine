package com.ssafy.d205.domain.report.service;

import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import com.ssafy.d205.domain.report.dto.SendReportRequest;
import com.ssafy.d205.domain.report.entity.UserReport;
import com.ssafy.d205.domain.report.repository.UserReportRepository;
import com.ssafy.d205.domain.user.entity.User;
import com.ssafy.d205.domain.user.repository.UserRepository;
import com.ssafy.d205.global.common.TimeProvider;
import com.ssafy.d205.global.exception.TargetUserNotFoundException;
import com.ssafy.d205.global.exception.UnknownCallerException;

/**
 * 신고를 받아 적습니다. 그것뿐입니다.
 *
 * <p>자동 조치가 없습니다. 신고당한 사람은 알 수 없고 검색에서도 사라지지 않습니다.
 * 판단은 사람이 하고, 그 사람이 쓸 도구는 아직 없습니다.
 *
 * <p><b>친구가 아니어도 신고할 수 있습니다.</b> 주된 쓰임이 같은 방에서 만난 사람을
 * 신고하는 것이라, 친구 관계를 요구하면 정작 필요한 자리에서 쓸 수 없습니다.
 */
@Service
@RequiredArgsConstructor
public class ReportService {

    private final UserReportRepository userReportRepository;
    private final UserRepository userRepository;
    private final TimeProvider timeProvider;

    @Transactional
    public void report(String callerUserId, SendReportRequest request) {
        User me = caller(callerUserId);
        User target = target(request.userId());

        if (me.getSeq().equals(target.getSeq())) {
            // 스키마의 CHECK 가 막지만 여기서 먼저 걸러 제약 위반 대신 뜻이 있는 응답을
            // 줍니다. 초대가 자기 자신을 다루는 방식과 같게 맞췄습니다.
            throw new TargetUserNotFoundException(request.userId());
        }

        // 빈 문자열과 없음을 같게 다룹니다. 클라이언트가 비운 칸을 "" 로 보낼지 생략할지는
        // 화면 사정이고, 저장된 뒤에는 둘을 구분할 이유가 없습니다.
        String memo = request.memo() == null || request.memo().isBlank() ? null : request.memo().strip();

        userReportRepository.save(
                UserReport.of(me.getSeq(), target.getSeq(), request.reason(), memo, timeProvider.now()));
    }

    /** 부르는 사람. 없으면 클라이언트가 계정 발급을 다시 불러야 하므로 코드를 구분합니다. */
    private User caller(String userId) {
        return userRepository.findByPublicId(userId)
                .orElseThrow(() -> new UnknownCallerException(userId));
    }

    private User target(String userId) {
        return userRepository.findByPublicId(userId)
                .orElseThrow(() -> new TargetUserNotFoundException(userId));
    }
}
