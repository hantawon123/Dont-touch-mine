package com.ssafy.d205.domain.chat.repository;

/**
 * 관리 화면이 읽는 대화 한 줄 (S15P21D205-1030).
 *
 * <p>닉네임은 표에 담겨 있지 않고 조회할 때 붙입니다. 바뀌는 값이라 담아 두면 지금 이름과 달라
 * 운영자를 헷갈리게 합니다.
 */
public interface AdminChatRow {

    Integer getId();

    /** LOBBY 또는 MATCH. */
    String getScope();

    /** 말한 사람의 공개 식별자. 계정을 모르는 줄이면 null 입니다. */
    String getUserId();

    /** 지금 닉네임. 계정을 모르거나 탈퇴했으면 null 입니다. */
    String getNickname();

    /**
     * 그 방에서만 뜻이 있는 발화자 표식.
     *
     * <p>계정이 없는 줄들을 서로 구분하는 유일한 수단입니다. 이것이 없으면 대화록이
     * "알 수 없음" 여러 줄로 뭉개져 읽을 수가 없습니다.
     */
    String getSenderRef();

    /** 가리기 전의 말. */
    String getMessage();

    /** 금칙어에 걸렸는가. 저장할 때 백엔드가 판정한 값입니다. */
    Boolean getMasked();

    /** yyyyMMddHHmmss, UTC. */
    String getSentAt();

    String getRoomCode();
}
