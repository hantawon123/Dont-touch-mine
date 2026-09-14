package com.ssafy.d205.domain.report.entity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;

/**
 * 한 사람이 한 번 신고한 기록.
 *
 * <p><b>이 행은 아무 동작도 일으키지 않습니다.</b> 신고당한 사람은 알 수 없고, 검색이나
 * 친구 요청이나 초대가 이 테이블을 읽지 않습니다. 차단과 다른 점이 여기이고, 설계의
 * 대부분이 여기서 나옵니다 - 읽는 경로가 없으니 조회 성능도, 경합도 문제가 되지
 * 않습니다.
 *
 * <p>신고자는 이 행을 고칠 수 없습니다. 취소하는 API 를 두지 않았습니다 - 취소를
 * 허용하면 "신고했다가 지운 기록"이 남을지 말지를 정해야 하는데, 그 질문에 답할 근거가
 * 아직 없습니다.
 *
 * <p>바뀌는 것은 검토 상태 하나뿐이고 그것은 운영자가 바꿉니다. 다만 <b>그 API 는 아직
 * 없습니다.</b> 인증이 없는 채로 열면 아무나 신고를 기각 처리해 숨길 수 있습니다.
 */
@Entity
@Table(name = "user_reports")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class UserReport {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    @Column(name = "user_reports_seq")
    private Integer seq;

    /**
     * 신고한 사람. <b>탈퇴하면 NULL 이 됩니다.</b>
     *
     * <p>기록을 지우지 않고 신고자만 비우는 이유는 V9 주석에 있습니다 - 신고는
     * 신고당한 사람에 대한 기록이지 신고자에 대한 기록이 아닙니다.
     */
    @Column(name = "reporter_seq")
    private Integer reporterSeq;

    @Column(name = "reported_seq", nullable = false)
    private Integer reportedSeq;

    /**
     * 이름으로 담습니다. ORDINAL 은 값의 순서를 바꾸거나 중간에 하나 넣는 순간
     * 이미 쌓인 행의 뜻이 전부 달라집니다.
     */
    @Enumerated(EnumType.STRING)
    @Column(name = "reason", nullable = false, length = 32)
    private ReportReason reason;

    /** 신고자가 적은 한 줄. 없을 수 있습니다. 상한 500자는 V17 과 SendReportRequest 와 같아야 합니다. */
    @Column(name = "memo", length = 500)
    private String memo;

    /** 신고한 시각. yyyyMMddHHmmss, UTC. */
    @Column(name = "created_at", nullable = false, length = 14)
    private String createdAt;

    /** 검토 상태. 들어올 때는 늘 {@link ReportStatus#PENDING} 입니다. */
    @Enumerated(EnumType.STRING)
    @Column(name = "status", nullable = false, length = 16)
    private ReportStatus status;

    /** 검토한 시각. 아직 안 봤으면 null 입니다. */
    @Column(name = "reviewed_at", length = 14)
    private String reviewedAt;

    /**
     * 검토한 관리자의 이름. 아직 안 봤으면 null 입니다.
     *
     * <p>지금은 계정이 하나라 값이 늘 같습니다. 사람마다 계정을 나눌 일이 생겼을 때
     * 스키마를 다시 고치지 않으려고 미리 둡니다.
     */
    @Column(name = "reviewed_by", length = 32)
    private String reviewedBy;

    /**
     * 운영자가 숨긴 시각. 보이는 신고는 null 입니다.
     *
     * <p>숨김은 완전 삭제와 다릅니다. 행이 남아 있어서 무고성 신고를 세는 집계가 비지
     * 않고, 잘못 숨긴 것을 DB 에서 되찾을 수 있습니다. 대신 <b>화면에서 되돌릴 방법은
     * 없습니다</b> - 숨긴 것만 보는 조회를 두지 않았습니다.
     *
     * <p>불리언이 아니라 시각인 이유는 되찾을 때 시각이 유일한 단서이기 때문입니다.
     * "어제 잘못 눌렀다"는 말에서 행을 찾으려면 언제 숨겼는지가 있어야 합니다.
     */
    @Column(name = "deleted_at", length = 14)
    private String deletedAt;

    private UserReport(Integer reporterSeq, Integer reportedSeq,
                       ReportReason reason, String memo, String now) {
        this.reporterSeq = reporterSeq;
        this.reportedSeq = reportedSeq;
        this.reason = reason;
        this.memo = memo;
        this.createdAt = now;
        this.status = ReportStatus.PENDING;
    }

    public static UserReport of(Integer reporterSeq, Integer reportedSeq,
                                ReportReason reason, String memo, String now) {
        return new UserReport(reporterSeq, reportedSeq, reason, memo, now);
    }

    /**
     * 운영자가 이 신고를 마무리합니다.
     *
     * <p>상태와 시각을 함께 씁니다. 둘을 따로 두면 "처리했다는데 언제인지 모르는" 행이
     * 생기고, 그 행은 검토 이력으로 쓸 수 없습니다.
     *
     * <p>다시 부르면 덮어씁니다. 판단을 뒤집는 일은 실제로 일어나고, 막으면 잘못 누른
     * 것을 고칠 방법이 없어집니다. 대신 이전 판단이 무엇이었는지는 남지 않으므로,
     * 이력이 필요해지면 이 행이 아니라 별도 기록이 있어야 합니다.
     */
    public void review(ReportStatus decision, String reviewer, String now) {
        this.status = decision;
        this.reviewedBy = reviewer;
        this.reviewedAt = now;
    }

    /**
     * 운영자가 이 신고를 목록에서 치웁니다.
     *
     * <p>검토 상태를 건드리지 않습니다. 숨기는 것과 판단하는 것은 다른 일입니다 -
     * 미검토인 채로 치운 신고를 나중에 DB 에서 되찾으면 그때 판단하면 되고, 여기서
     * 임의로 DISMISSED 를 찍으면 운영자가 내리지 않은 판단이 기록에 남습니다.
     *
     * <p>이미 숨긴 것을 다시 숨겨도 시각을 덮어쓰지 않습니다. 처음 치운 시각이 되찾을
     * 때의 단서이고, 두 번째 클릭이 그것을 지울 이유가 없습니다.
     */
    public void hide(String now) {
        if (this.deletedAt == null) {
            this.deletedAt = now;
        }
    }
}
