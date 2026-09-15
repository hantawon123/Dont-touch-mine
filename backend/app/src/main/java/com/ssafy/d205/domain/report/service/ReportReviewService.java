package com.ssafy.d205.domain.report.service;

import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.HashMap;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

import com.ssafy.d205.domain.report.dto.ReportDetail;
import com.ssafy.d205.domain.report.dto.ReportedUserListResponse;
import com.ssafy.d205.domain.report.dto.ReportedUserSummary;
import com.ssafy.d205.domain.report.entity.ReportStatus;
import com.ssafy.d205.domain.report.entity.UserReport;
import com.ssafy.d205.domain.report.repository.ReasonCountRow;
import com.ssafy.d205.domain.report.repository.UserReportRepository;
import com.ssafy.d205.domain.user.entity.User;
import com.ssafy.d205.domain.user.repository.UserRepository;
import com.ssafy.d205.global.common.TimeProvider;
import com.ssafy.d205.global.exception.TargetUserNotFoundException;

/**
 * 운영자가 신고를 읽고 마무리합니다.
 *
 * <p>신고를 접수하는 {@link ReportService} 와 나눈 이유는 부르는 사람이 다르기
 * 때문입니다. 접수는 게임 클라이언트가 인증 없이 부르고, 이쪽은 로그인한 운영자만
 * 부릅니다. 한 서비스에 두면 "이 메서드는 누가 부를 수 있는가"를 매번 따져야 합니다.
 */
@Service
@RequiredArgsConstructor
public class ReportReviewService {

    private final UserReportRepository userReportRepository;
    private final UserRepository userRepository;
    private final TimeProvider timeProvider;

    /**
     * 신고당한 사람들을 묶어서 돌려줍니다.
     *
     * <p>사유별 건수를 따로 조회해 붙입니다. 한 쿼리로 합치면 사람마다 사유 수만큼
     * 행이 늘어나 건수와 인원수가 부풀려집니다.
     */
    @Transactional(readOnly = true)
    public ReportedUserListResponse list(ReportStatus status) {
        Map<String, Map<String, Integer>> reasons = reasonsByUser(status);

        return new ReportedUserListResponse(
                userReportRepository.summarizeByStatus(status.name()).stream()
                        .map(row -> new ReportedUserSummary(
                                row.getUserId(),
                                row.getNickname(),
                                row.getReportCount(),
                                row.getReporterCount(),
                                row.getFromDeletedAccounts(),
                                reasons.getOrDefault(row.getUserId(), Map.of()),
                                row.getLastReportedAt(),
                                row.getSuspendedAt() != null))
                        .toList());
    }

    /**
     * 한 사람에 대한 신고를 하나씩. 최근 순입니다.
     *
     * <p>status 를 주면 그 상태만 봅니다. 목록에서 미검토를 보다가 펼쳤는데 예전에
     * 처리한 것까지 섞여 나오면, 지금 무엇을 판단해야 하는지가 흐려집니다.
     *
     * <p>비워 두면 전부 봅니다. "이 사람 그동안 어땠나"를 볼 때 필요합니다.
     */
    @Transactional(readOnly = true)
    public ReportDetail.ListResponse detail(String userId, ReportStatus status) {
        // 신고가 하나도 없는 사람과 없는 계정을 구분합니다. 전자는 빈 목록이고
        // 후자는 404 입니다. 구분하지 않으면 오타로 부른 것과 정상 조회가 같아 보입니다.
        target(userId);

        return new ReportDetail.ListResponse(
                userReportRepository
                        .findByReportedUserId(userId, status == null ? null : status.name())
                        .stream()
                        .map(row -> new ReportDetail(
                                row.getId(), row.getReason(), row.getMemo(),
                                row.getCreatedAt(), row.getStatus()))
                        .toList());
    }

    /**
     * 한 사람의 미검토 신고를 한 번에 마무리합니다.
     *
     * <p>신고 한 건이 아니라 사람 단위로 처리하는 이유는 운영자가 사람을 보고 판단하기
     * 때문입니다. 다섯 건 쌓인 사람을 다섯 번 누르게 할 이유가 없습니다.
     *
     * <p><b>이미 검토한 것은 건드리지 않습니다.</b> 어제 기각한 것을 오늘 조치함으로
     * 덮으면 그때 무슨 판단을 했는지가 사라집니다. 검토 뒤에 새로 들어온 신고만
     * 미검토로 남아 다음 차례에 다시 올라옵니다.
     *
     * <p>처리할 것이 없어도 성공입니다. 두 사람이 같은 화면을 보고 있다가 둘 다 눌렀을
     * 때, 뒤에 누른 쪽에게 오류를 주면 무엇이 잘못됐는지 알 수 없습니다. 원하는 결과는
     * 이미 이루어져 있습니다.
     *
     * @return 이번에 마무리한 건수
     */
    @Transactional
    public int review(String userId, ReportStatus decision, String reviewer) {
        User target = target(userId);

        List<UserReport> pending = userReportRepository.findPendingAbout(target.getSeq());
        String now = timeProvider.now();

        for (UserReport report : pending) {
            report.review(decision, reviewer, now);
        }

        return pending.size();
    }

    /**
     * 한 사람에 대한 신고를 목록에서 치웁니다.
     *
     * <p><b>{@code status} 는 화면이 보여준 범위입니다.</b> 목록은 상태로 걸러 보여주므로
     * 치우는 범위도 같아야 합니다. 가리지 않고 치우면 운영자가 ACTIONED 화면에서 "1건"을
     * 보고 누른 한 번에, 한 번도 보지 못한 PENDING 신고까지 함께 사라집니다. null 이면
     * 상태를 가리지 않습니다 - API 를 직접 부르는 쪽을 위한 것이고 화면은 늘 채웁니다.
     *
     * <p>검토 상태를 건드리지 않습니다. 숨김은 판단이 아닙니다 - 여기서 DISMISSED 를
     * 찍으면 운영자가 내리지 않은 판단이 기록에 남고, 무고성 신고를 세는 집계가
     * 조용히 틀어집니다.
     *
     * <p>치울 것이 없어도 성공입니다. {@link #review}와 같은 이유입니다.
     *
     * @return 이번에 치운 건수
     */
    @Transactional
    public int hide(String userId, ReportStatus status) {
        User target = target(userId);

        List<UserReport> visible = status == null
                ? userReportRepository.findVisibleAbout(target.getSeq())
                : userReportRepository.findVisibleAbout(target.getSeq(), status);
        String now = timeProvider.now();

        for (UserReport report : visible) {
            report.hide(now);
        }

        return visible.size();
    }

    /**
     * 신고 한 건을 목록에서 치웁니다.
     *
     * <p>없는 번호를 줘도 성공입니다. 두 사람이 같은 화면을 보다가 둘 다 눌렀을 때
     * 뒤에 누른 쪽이 받는 답이고, 원하는 결과는 이미 이루어져 있습니다. 404 를 주면
     * "내가 뭘 잘못했나"를 확인할 방법이 없습니다.
     *
     * @return 이번에 치웠으면 true, 이미 없거나 숨겨져 있었으면 false
     */
    @Transactional
    public boolean hideEntry(Integer reportId) {
        return userReportRepository.findById(reportId)
                .filter(report -> report.getDeletedAt() == null)
                .map(report -> {
                    report.hide(timeProvider.now());
                    return true;
                })
                .orElse(false);
    }

    /**
     * 한 사람에 대한 신고를 지웁니다. <b>되돌릴 수 없습니다.</b>
     *
     * <p><b>{@code status} 는 화면이 보여준 범위입니다.</b> {@link #hide} 와 같은 이유로
     * 받습니다. 여기서는 그 이유가 더 무겁습니다 - 숨김은 DB 에서 되찾을 수 있지만 이쪽은
     * 되찾을 수 없어서, 보지 못한 신고가 함께 지워지면 그것으로 끝입니다. null 이면
     * 상태를 가리지 않습니다.
     *
     * <p>범위 안의 숨긴 것까지 함께 지웁니다. 숨긴 것만 남기면 나중에 그 행들의 출처를
     * 아무도 설명하지 못합니다.
     *
     * <p>지우면 그 사람이 신고당한 이력이 사라집니다. 무고성 신고를 세는 근거도 함께
     * 사라지므로, 눈앞에서 치우는 것이 목적이라면 {@link #hide} 가 맞습니다.
     *
     * @return 지운 건수
     */
    @Transactional
    public int purge(String userId, ReportStatus status) {
        Integer seq = target(userId).getSeq();

        return status == null
                ? userReportRepository.deleteByReportedSeq(seq)
                : userReportRepository.deleteByReportedSeqAndStatus(seq, status);
    }

    /**
     * 신고 한 건을 지웁니다. <b>되돌릴 수 없습니다.</b>
     *
     * <p>없는 번호를 줘도 성공입니다. {@link #hideEntry} 와 같은 이유입니다.
     *
     * @return 이번에 지웠으면 true, 이미 없었으면 false
     */
    @Transactional
    public boolean purgeEntry(Integer reportId) {
        if (!userReportRepository.existsById(reportId)) {
            return false;
        }

        userReportRepository.deleteById(reportId);
        return true;
    }

    /**
     * 사용자별 사유 분포를 미리 모아 둡니다.
     *
     * <p>목록에 스무 명이 있을 때 사람마다 따로 물으면 쿼리가 스물한 번 나갑니다.
     */
    private Map<String, Map<String, Integer>> reasonsByUser(ReportStatus status) {
        Map<String, Map<String, Integer>> byUser = new HashMap<>();

        for (ReasonCountRow row : userReportRepository.countReasonsByStatus(status.name())) {
            byUser.computeIfAbsent(row.getUserId(), key -> new LinkedHashMap<>())
                    .put(row.getReason(), row.getCount());
        }

        return byUser;
    }

    private User target(String userId) {
        return userRepository.findByPublicId(userId)
                .orElseThrow(() -> new TargetUserNotFoundException(userId));
    }
}
