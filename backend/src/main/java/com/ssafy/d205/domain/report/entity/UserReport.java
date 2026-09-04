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
 * <p>고칠 일이 없어서 상태를 바꾸는 메서드가 없습니다. 신고를 취소하는 API 도 두지
 * 않았습니다. 취소를 허용하면 "신고했다가 지운 기록"이 남을지 말지를 정해야 하는데,
 * 운영자 도구가 없는 지금은 그 질문에 답할 근거가 없습니다.
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

    @Column(name = "reporter_seq", nullable = false)
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

    /** 신고자가 적은 한 줄. 없을 수 있습니다. */
    @Column(name = "memo", length = 200)
    private String memo;

    /** 신고한 시각. yyyyMMddHHmmss, UTC. */
    @Column(name = "created_at", nullable = false, length = 14)
    private String createdAt;

    private UserReport(Integer reporterSeq, Integer reportedSeq,
                       ReportReason reason, String memo, String now) {
        this.reporterSeq = reporterSeq;
        this.reportedSeq = reportedSeq;
        this.reason = reason;
        this.memo = memo;
        this.createdAt = now;
    }

    public static UserReport of(Integer reporterSeq, Integer reportedSeq,
                                ReportReason reason, String memo, String now) {
        return new UserReport(reporterSeq, reportedSeq, reason, memo, now);
    }
}
