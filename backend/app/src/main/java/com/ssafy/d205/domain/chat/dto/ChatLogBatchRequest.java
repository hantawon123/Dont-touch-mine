package com.ssafy.d205.domain.chat.dto;

import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotEmpty;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Pattern;
import jakarta.validation.constraints.Size;

import java.util.List;

import com.ssafy.d205.domain.chat.entity.ChatScope;

/**
 * 게임 서버가 2~3초 모아서 보내는 채팅 묶음 (S15P21D205-1027).
 *
 * <p>한 줄씩 보내지 않는 이유는 요청 수를 줄이려는 것이고, 경기 종료 시 한 번에 보내지 않는
 * 이유는 그 방식이 아웃박스를 만들기 때문입니다. 아웃박스는 컨테이너가 죽으면 같이 사라집니다
 * (S15P21D205-1021 에서 실제로 겪은 것). 몇 초 단위면 잃어도 몇 초치입니다.
 *
 * @param messages 한 묶음. 상한 200은 6인이 2~3초 안에 낼 수 있는 양보다 한참 큽니다.
 *                 상한이 없으면 잘못된 호출 하나가 트랜잭션을 길게 잡습니다.
 */
public record ChatLogBatchRequest(
        @NotEmpty(message = "messages는 비어 있을 수 없습니다.")
        @Size(max = 200, message = "messages는 한 번에 200개를 넘을 수 없습니다.")
        @Valid
        List<Entry> messages
) {

    /**
     * @param roomCode      어느 방인가. 신고의 context_key 에서 # 앞과 같은 값이다.
     * @param scope         LOBBY 또는 MATCH
     * @param userPublicId  말한 사람의 계정. <b>없을 수 있다.</b> 백엔드가 죽어 있을 때 저장된
     *                      크리덴셜로 들어온 클라이언트는 자기 계정을 모른다(S15P21D205-925).
     * @param senderRef     그 방에서만 뜻이 있는 발화자 표식. 계정을 모를 때도 몇 사람이 말했는지는
     *                      구분되게 한다. 게임 서버가 Fusion playerId 를 넣는다.
     * @param message       <b>가리기 전의 원문.</b> 게임 서버는 가린 말을 뿌리고 원문을 여기로 보낸다.
     * @param sentAt        말한 시각. yyyyMMddHHmmss, UTC. 게임 서버가 찍는다. 묶음이 늦게 도착해도
     *                      순서가 유지되도록 도착 시각과 따로 받는다.
     */
    public record Entry(
            @NotBlank(message = "roomCode는 필수입니다.")
            @Size(max = 16, message = "roomCode는 16자를 넘을 수 없습니다.")
            String roomCode,

            @NotNull(message = "scope는 필수입니다.")
            ChatScope scope,

            @Size(max = 36, message = "userPublicId는 36자를 넘을 수 없습니다.")
            String userPublicId,

            @NotBlank(message = "senderRef는 필수입니다.")
            @Size(max = 64, message = "senderRef는 64자를 넘을 수 없습니다.")
            String senderRef,

            @NotBlank(message = "message는 필수입니다.")
            @Size(max = 80, message = "message는 80자를 넘을 수 없습니다.")
            String message,

            @NotBlank(message = "sentAt은 필수입니다.")
            @Pattern(regexp = "^[0-9]{14}$", message = "sentAt은 yyyyMMddHHmmss 형식이어야 합니다.")
            String sentAt
    ) {
    }
}
