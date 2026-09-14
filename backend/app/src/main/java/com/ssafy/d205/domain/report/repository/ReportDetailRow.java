package com.ssafy.d205.domain.report.repository;

/**
 * 신고 한 건. 상세 화면의 한 줄입니다.
 *
 * <p>신고자가 누구인지는 담지 않습니다. 운영자가 판단할 때 필요한 것은 무엇이 몇 번
 * 일어났는가이고, 신고자를 드러내면 보복의 여지가 생깁니다.
 */
public interface ReportDetailRow {

    /**
     * 신고 한 건의 식별자(user_reports_seq).
     *
     * <p>건별 숨김·삭제가 가리킬 대상입니다(S15P21D205-900). 그 전까지는 상세가 읽기
     * 전용이라 무엇을 가리킬 필요가 없었습니다.
     *
     * <p>사람의 public_id 와 달리 이 값은 순번이 드러나는 내부 키입니다. 로그인한
     * 운영자만 보는 화면이라 그대로 씁니다.
     */
    Integer getId();

    String getReason();

    /** 신고자가 적은 한 줄. 없을 수 있습니다. */
    String getMemo();

    /** yyyyMMddHHmmss, UTC. */
    String getCreatedAt();

    String getStatus();
}
