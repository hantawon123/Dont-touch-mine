package com.ssafy.d205.domain.chat.entity;

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
 * 신고 조사를 위해 남기는 채팅 한 줄 (S15P21D205-1027).
 *
 * <p>표의 뜻과 보관 기간은 V21 마이그레이션 주석에 있습니다. 여기서는 엔티티가 지켜야 할 것만
 * 적습니다.
 *
 * <p><b>원문을 담습니다.</b> 가린 말만 남기면 조사가 되지 않습니다. 대신 오래 두지 않습니다.
 *
 * <p><b>닉네임을 담지 않습니다.</b> 바뀌는 값이라 조회할 때 {@code sender_seq} 로 조인해 붙입니다.
 * 담아 두면 개인정보가 하나 더 쌓이고, 그 값이 지금 닉네임과 달라 운영자를 헷갈리게 합니다.
 */
@Entity
@Table(name = "chat_logs")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class ChatLog {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    @Column(name = "chat_logs_seq")
    private Integer seq;

    @Column(name = "room_code", nullable = false, length = 16)
    private String roomCode;

    /**
     * 이름으로 담습니다. ORDINAL 은 값의 순서를 바꾸거나 중간에 하나 넣는 순간 이미 쌓인 행의
     * 뜻이 전부 달라집니다. user_reports 의 사유와 같은 이유입니다.
     */
    @Enumerated(EnumType.STRING)
    @Column(name = "scope", nullable = false, length = 8)
    private ChatScope scope;

    /** 말한 사람. 탈퇴하면 FK 가 NULL 로 만듭니다. */
    @Column(name = "sender_seq")
    private Integer senderSeq;

    /** 그 방에서만 뜻이 있는 발화자 표식. 계정을 모를 때 대화록이 뭉개지지 않게 합니다. */
    @Column(name = "sender_ref", nullable = false, length = 64)
    private String senderRef;

    /** 가리기 전의 말. 상한 80자는 클라이언트의 LobbyChatMessage.MaxTextLength 와 같아야 합니다. */
    @Column(name = "message", nullable = false, length = 80)
    private String message;

    /**
     * 금칙어에 걸렸는가. <b>게임 서버가 보낸 값이 아니라 저장할 때 백엔드가 판정한 값입니다.</b>
     * 게임 서버가 기동할 때 받아 간 목록은 그사이 낡을 수 있고, 기록은 최신 기준으로 남는 편이
     * 낫습니다.
     */
    @Column(name = "masked", nullable = false)
    private boolean masked;

    /** 말한 시각. yyyyMMddHHmmss, UTC. 게임 서버가 찍습니다. */
    @Column(name = "sent_at", nullable = false, length = 14)
    private String sentAt;

    /** 백엔드에 도착한 시각. 묶음 전송이라 sent_at 보다 몇 초 늦습니다. */
    @Column(name = "created_at", nullable = false, length = 14)
    private String createdAt;

    // 넣는 코드가 없는 것은 일부러입니다. 쓰기는
    // com.ssafy.d205.domain.chat.repository.ChatLogBatchWriter 가 JDBC 배치로 합니다 - 이 엔티티가
    // IDENTITY 라 JPA 로 넣으면 묶음 하나가 문장 수백 개로 나갑니다(S15P21D205-1077).
    // 이 클래스는 매핑 검증(ddl-auto: validate)과 리포지토리 타입으로 남습니다.
}
