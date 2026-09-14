package com.ssafy.d205.domain.admin.dto;

import java.util.List;

import com.ssafy.d205.domain.feedback.repository.FeedbackRow;
import com.ssafy.d205.domain.report.repository.AdminReportRow;

/**
 * 관리 화면에서 사용자 한 명을 펼쳤을 때 보이는 것 전부.
 *
 * <p>목록의 한 줄({@link AdminUserSummary})을 그대로 안고, 그 사람이 받은 신고와 한 신고와
 * 보낸 피드백을 따로 담습니다. 세 목록을 한 응답에 넣는 이유는 운영자가 사람 하나를 판단할
 * 때 세 가지를 같이 보기 때문입니다. 따로 부르게 하면 화면이 세 번 묻고 세 번 기다립니다.
 *
 * @param user            목록과 같은 요약
 * @param receivedReports 이 사람이 받은 신고. 최근 순, 최대 200건
 * @param madeReports     이 사람이 한 신고. 최근 순, 최대 200건
 * @param feedback        이 사람이 보낸 피드백. 최근 순, 최대 100건
 */
public record AdminUserDetail(
        AdminUserSummary user,
        List<ReportEntry> receivedReports,
        List<ReportEntry> madeReports,
        List<FeedbackEntry> feedback
) {

    /**
     * 신고 한 건. 상대편은 받은 신고에서는 신고자, 한 신고에서는 신고당한 사람입니다.
     *
     * @param counterpartUserId   상대편 공개 식별자. 탈퇴했으면 null
     * @param counterpartNickname 상대편 닉네임. 탈퇴했으면 null
     */
    public record ReportEntry(
            Integer id,
            String reason,
            String memo,
            String createdAt,
            String status,
            String counterpartUserId,
            String counterpartNickname
    ) {
        public static ReportEntry from(AdminReportRow row) {
            return new ReportEntry(row.getId(), row.getReason(), row.getMemo(), row.getCreatedAt(),
                    row.getStatus(), row.getCounterpartUserId(), row.getCounterpartNickname());
        }
    }

    public record FeedbackEntry(
            Integer id,
            String message,
            String buildVer,
            String platform,
            String createdAt
    ) {
        public static FeedbackEntry from(FeedbackRow row) {
            return new FeedbackEntry(row.getId(), row.getMessage(), row.getBuildVer(),
                    row.getPlatform(), row.getCreatedAt());
        }
    }
}
