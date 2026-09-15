package com.ssafy.d205.domain.admin.repository;

/**
 * 감사 로그 한 행의 투영입니다. getter 이름이 쿼리의 컬럼 별칭과 같아야 채워집니다.
 */
public interface SuspensionAuditRow {

    String getUserId();

    /** 누른 시점의 닉네임입니다. 지금 닉네임이 아닙니다. */
    String getNickname();

    /** SUSPEND 또는 LIFT. */
    String getAction();

    /** 정지 사유. 해제는 null 입니다. */
    String getReason();

    String getAdminUsername();

    String getActedAt();

    /**
     * 대상 계정의 내부 키. 탈퇴했으면 null 입니다.
     *
     * <p>"탈퇴했는가"를 SQL 에서 계산하지 않는 이유가 있습니다. MySQL 의 {@code IS NULL} 은
     * 0/1 정수를 돌려주고, 스프링 데이터는 그것을 boolean 투영에 넣지 못해
     * "Cannot project java.lang.Long to boolean" 으로 요청이 통째로 실패합니다.
     *
     * <p>이 값은 여기서 끝납니다. 응답 DTO 가 null 여부만 boolean 으로 바꿔 담으므로 내부 키가
     * 밖으로 나가지 않습니다. 이 투영에서 이 값을 그대로 응답에 싣는 변경은 하지 마세요.
     */
    Integer getUserSeq();
}
