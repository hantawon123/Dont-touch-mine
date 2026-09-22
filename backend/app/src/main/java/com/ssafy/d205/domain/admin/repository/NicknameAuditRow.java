package com.ssafy.d205.domain.admin.repository;

/**
 * 개명 감사 로그 한 행의 투영입니다. getter 이름이 쿼리의 컬럼 별칭과 같아야 채워집니다.
 */
public interface NicknameAuditRow {

    String getUserId();

    /** 바꾸기 전 이름. */
    String getBeforeNickname();

    /** 바꾼 뒤 이름. 지금 닉네임이 아닙니다 - 그 뒤에 또 바뀌었을 수 있습니다. */
    String getAfterNickname();

    String getReason();

    String getAdminUsername();

    String getActedAt();

    /**
     * 대상 계정의 내부 키. 탈퇴했으면 null 입니다.
     *
     * <p>"탈퇴했는가"를 SQL 에서 계산하지 않는 이유는 {@link SuspensionAuditRow#getUserSeq()}
     * 주석과 같습니다 - MySQL 의 {@code IS NULL} 을 boolean 투영에 넣을 수 없습니다.
     * 이 값은 응답 DTO 에서 null 여부만 남고 버려집니다.
     */
    Integer getUserSeq();
}
