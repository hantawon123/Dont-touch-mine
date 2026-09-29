package com.ssafy.d205.domain.report.service;

import lombok.RequiredArgsConstructor;
import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.time.Duration;

import com.ssafy.d205.domain.report.dto.SendReportRequest;
import com.ssafy.d205.domain.report.entity.UserReport;
import com.ssafy.d205.domain.report.repository.UserReportRepository;
import com.ssafy.d205.domain.user.entity.User;
import com.ssafy.d205.domain.user.repository.UserRepository;
import com.ssafy.d205.global.common.TimeProvider;
import com.ssafy.d205.global.exception.ReportAlreadySentException;
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
 *
 * <p><b>한 경기에 같은 상대는 한 번입니다</b>(S15P21D205-1017). 클라이언트가 보내는 경기 키로 가르고,
 * 키가 없으면 24시간으로 대신 가릅니다. 여러 경기에 걸친 횟수는 여전히 쌓이고 그것이 운영자에게
 * 신호입니다.
 */
@Service
@RequiredArgsConstructor
public class ReportService {

    /** 경기 키 없이 온 신고에 적용하는 창. 한 방에서 하루에 같은 사람과 두 판 이상 뛰는 일은 드뭅니다. */
    static final Duration WITHOUT_CONTEXT_WINDOW = Duration.ofHours(24);

    private final UserReportRepository userReportRepository;
    private final UserRepository userRepository;
    private final TimeProvider timeProvider;

    @Transactional
    public void report(String callerUserId, SendReportRequest request) {
        User me = caller(callerUserId);
        User target = target(request.userId());
        String contextKey = request.contextKey() == null || request.contextKey().isBlank()
                ? null : request.contextKey().strip();

        if (me.getSeq().equals(target.getSeq())) {
            // 스키마의 CHECK 가 막지만 여기서 먼저 걸러 제약 위반 대신 뜻이 있는 응답을
            // 줍니다. 초대가 자기 자신을 다루는 방식과 같게 맞췄습니다.
            throw new TargetUserNotFoundException(request.userId());
        }

        // 빈 문자열과 없음을 같게 다룹니다. 클라이언트가 비운 칸을 "" 로 보낼지 생략할지는
        // 화면 사정이고, 저장된 뒤에는 둘을 구분할 이유가 없습니다.
        String memo = request.memo() == null || request.memo().isBlank() ? null : request.memo().strip();

        // 미리 확인해서 뜻이 있는 응답을 줍니다. 아래 catch 는 두 요청이 동시에 이 줄을 지난 경우입니다.
        boolean already = contextKey != null
                ? userReportRepository.existsPairInContext(me.getSeq(), target.getSeq(), contextKey)
                : userReportRepository.existsPairWithoutContextSince(
                        me.getSeq(), target.getSeq(), timeProvider.minus(WITHOUT_CONTEXT_WINDOW));
        if (already) {
            throw new ReportAlreadySentException();
        }

        try {
            // save 가 아니라 saveAndFlush 인 이유는 제약 위반을 여기서 받으려는 것입니다. 트랜잭션 끝에서
            // 터지면 이 메서드 밖이라 잡을 수 없고, 화면은 CONFLICT 라는 뜻 없는 코드를 받습니다.
            userReportRepository.saveAndFlush(UserReport.of(
                    me.getSeq(), target.getSeq(), request.reason(), memo, contextKey, timeProvider.now()));
        } catch (DataIntegrityViolationException e) {
            // 같은 쌍·같은 키의 두 요청이 겹친 것입니다. 다른 제약(FK)일 수도 있지만 상대는 위에서 이미
            // 확인했으므로 사실상 유니크 키뿐이고, 어느 쪽이든 두 번째 요청이 원한 결과는 이미 있습니다.
            throw new ReportAlreadySentException();
        }
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
