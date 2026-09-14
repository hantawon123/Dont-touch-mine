package com.ssafy.d205.domain.admin.service;

import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.List;

import com.ssafy.d205.domain.admin.dto.AdminUserDetail;
import com.ssafy.d205.domain.admin.dto.AdminUserListResponse;
import com.ssafy.d205.domain.admin.dto.AdminUserSummary;
import com.ssafy.d205.domain.feedback.repository.UserFeedbackRepository;
import com.ssafy.d205.domain.report.repository.UserReportRepository;
import com.ssafy.d205.domain.user.repository.AdminUserRow;
import com.ssafy.d205.domain.user.repository.UserRepository;
import com.ssafy.d205.global.exception.TargetUserNotFoundException;

/**
 * 운영자가 사용자를 찾고 한 명을 들여다보는 조회 (S15P21D205-972, 973).
 *
 * <p>읽기만 합니다. 정지·해제는 {@link AccountSuspensionService} 가 맡고, 이 클래스는 그 결과를
 * 보여줄 뿐입니다. 한곳에 두지 않은 이유는 성질이 다르기 때문입니다 - 저쪽은 되돌릴 수 있어도
 * 사람에게는 서비스가 멈추는 일이고, 이쪽은 아무것도 바꾸지 않습니다.
 *
 * <p>세 도메인(user, report, feedback)의 리포지토리를 직접 읽습니다. 각 도메인 서비스를 거치지
 * 않는 이유는 그 서비스들이 게임 API 의 규칙(자기 자신 제외, searchable 필터, 신고자 숨김)을
 * 갖고 있어서입니다. 운영자에게는 그 규칙이 반대로 방해가 됩니다.
 */
@Service
@RequiredArgsConstructor
public class AdminUserQueryService {

    /** 한 번에 돌려주는 최대 인원. 화면이 스크롤 없이 훑을 수 있는 양이고, 상관 서브쿼리 비용의 상한이기도 합니다. */
    static final int MAX_LIMIT = 50;

    private final UserRepository userRepository;
    private final UserReportRepository userReportRepository;
    private final UserFeedbackRepository userFeedbackRepository;

    /**
     * 닉네임 부분 일치 또는 userId 정확 일치로 찾습니다. q 가 비면 최근 가입순입니다.
     *
     * <p>limit 은 1~50 으로 자릅니다. 없거나 범위 밖이면 50 입니다. 400 을 내지 않는 이유는
     * 운영자 화면이 고른 값이라 잘못될 일이 없고, 잘못됐더라도 "너무 많이 달라고 했으니 안 준다"
     * 보다 "50 까지만 준다"가 운영자에게 유용하기 때문입니다.
     */
    @Transactional(readOnly = true)
    public AdminUserListResponse search(String q, Integer limit) {
        String trimmed = q == null ? "" : q.strip();
        int size = limit == null || limit < 1 || limit > MAX_LIMIT ? MAX_LIMIT : limit;

        List<AdminUserRow> rows = trimmed.isEmpty()
                ? userRepository.searchForAdmin("%", "", size)
                : userRepository.searchForAdmin("%" + escapeLike(trimmed) + "%", trimmed, size);

        return new AdminUserListResponse(rows.stream().map(AdminUserSummary::from).toList());
    }

    /**
     * 사용자 한 명의 요약과 받은 신고, 한 신고, 보낸 피드백.
     *
     * <p>없는 계정은 TARGET_NOT_FOUND 입니다. ACCOUNT_NOT_FOUND 가 아닙니다 - 그쪽은 부르는
     * 사람이 없다는 뜻이고, 여기서 부르는 사람은 로그인한 운영자입니다.
     */
    @Transactional(readOnly = true)
    public AdminUserDetail detail(String userId) {
        // 패턴을 비우면 LIKE 에 아무것도 걸리지 않으므로 public_id 정확 일치 하나만 남습니다.
        AdminUserRow row = userRepository.searchForAdmin("", userId, 1).stream()
                .findFirst()
                .orElseThrow(() -> new TargetUserNotFoundException(userId));

        return new AdminUserDetail(
                AdminUserSummary.from(row),
                userReportRepository.findReceivedForAdmin(userId).stream()
                        .map(AdminUserDetail.ReportEntry::from).toList(),
                userReportRepository.findMadeForAdmin(userId).stream()
                        .map(AdminUserDetail.ReportEntry::from).toList(),
                userFeedbackRepository.findByAuthorForAdmin(userId).stream()
                        .map(AdminUserDetail.FeedbackEntry::from).toList());
    }

    /**
     * LIKE 의 와일드카드를 글자 그대로 만듭니다. 이스케이프 문자는 쿼리의 ESCAPE '!' 와 같아야 합니다.
     *
     * <p>안 하면 "%" 를 검색한 운영자가 전체 목록을 받고, "_" 한 글자가 아무 글자와 맞습니다.
     * 닉네임에 % 나 _ 가 들어갈 수 있는지와 무관하게, 검색어를 패턴으로 해석하지 않는 것이
     * 검색의 뜻입니다.
     */
    static String escapeLike(String raw) {
        return raw.replace("!", "!!").replace("%", "!%").replace("_", "!_");
    }
}
