package com.ssafy.d205.domain.report.repository;

/**
 * 신고<b>한</b> 사람 하나에 대한 요약. 신고자 관점 목록의 한 줄입니다 (S15P21D205-1004).
 *
 * <p>{@link ReportedUserRow} 를 뒤집은 것입니다. 그쪽은 "이 사람이 얼마나 신고당했나"이고
 * 이쪽은 "이 사람이 남을 얼마나 신고하나"입니다. 기각된 것이 많으면 무고성 신고를 반복하는
 * 사람이고, 그것은 신고당한 사람 목록에서는 보이지 않습니다.
 */
public interface ReporterRow {

    /** 신고자의 공개 식별자. <b>탈퇴했으면 null 입니다</b> - 그 행은 탈퇴한 신고자 전부를 묶은 것입니다. */
    String getUserId();

    /** 신고자의 닉네임. 탈퇴했으면 null 입니다. */
    String getNickname();

    /** 이 사람이 한 신고 건수. */
    int getReportCount();

    /** 아직 검토하지 않은 건수. */
    int getPendingCount();

    /** 운영자가 문제 있다고 본 건수. */
    int getActionedCount();

    /** 운영자가 문제 없다고 본 건수. 검토된 것 중 이 비율이 높으면 신고가 근거 없다는 뜻입니다. */
    int getDismissedCount();

    /**
     * 신고한 상대의 수.
     *
     * <p>건수와 함께 봐야 합니다. "10건 1명"은 한 사람을 집요하게 신고한 것이고 "10건 8명"은
     * 여기저기 신고하는 사람입니다. 둘은 다른 문제입니다.
     */
    int getTargetCount();

    /** 가장 최근 신고 시각. yyyyMMddHHmmss, UTC. */
    String getLastReportedAt();
}
