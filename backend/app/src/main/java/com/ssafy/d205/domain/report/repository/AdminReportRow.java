package com.ssafy.d205.domain.report.repository;

/**
 * 관리 화면 사용자 상세가 보는 신고 한 건. 상대편(신고자 또는 신고당한 사람)이 함께 옵니다.
 *
 * <p>{@link ReportDetailRow} 와 달리 신고자를 담습니다. 그쪽은 "누가 신고했는가"를 일부러
 * 빼 두었지만(보복 여지), 운영자가 사용자 한 명을 열어 보는 자리에서는 신고자를 알아야
 * 무고성 신고인지 판단할 수 있습니다(S15P21D205-973).
 *
 * <p>상대편이 탈퇴했으면 둘 다 NULL 입니다. 화면은 "탈퇴한 계정" 으로 보여줍니다.
 */
public interface AdminReportRow {

    Integer getId();

    String getReason();

    String getMemo();

    String getCreatedAt();

    String getStatus();

    String getCounterpartUserId();

    String getCounterpartNickname();
}
