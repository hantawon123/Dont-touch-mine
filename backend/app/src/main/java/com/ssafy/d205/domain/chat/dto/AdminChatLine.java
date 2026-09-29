package com.ssafy.d205.domain.chat.dto;

import java.util.List;

/**
 * 관리 화면에 보이는 대화 한 줄 (S15P21D205-1030).
 *
 * @param speaker    화면에 쓸 이름. 계정을 아는 줄이면 지금 닉네임이고, 모르면 "참가자 A" 처럼
 *                   그 대화 안에서만 통하는 딱지입니다. 누구인지 모르는 줄들도 서로 구분돼야
 *                   대화가 읽힙니다
 * @param userId     말한 사람의 공개 식별자. 정지 버튼이 가리킬 대상이고, 모르면 null 입니다
 * @param message    <b>가리기 전의 말.</b> 플레이어들이 본 것은 가려진 문장이지만 판단에
 *                   필요한 것은 원문입니다
 * @param masked     금칙어에 걸렸는가. 몇 번 그랬는지가 한눈에 보여야 합니다
 * @param sentAt     yyyyMMddHHmmss, UTC
 */
public record AdminChatLine(
        Integer id,
        String scope,
        String roomCode,
        String speaker,
        String userId,
        String message,
        boolean masked,
        String sentAt
) {

    /**
     * @param lines      말한 순서대로
     * @param from       조회한 구간의 시작 (yyyyMMddHHmmss, UTC)
     * @param to         조회한 구간의 끝
     * @param maskedCount 그 구간에서 금칙어에 걸린 줄 수. 운영자가 먼저 보는 숫자입니다
     */
    public record ListResponse(List<AdminChatLine> lines, String from, String to, int maskedCount) {
    }
}
