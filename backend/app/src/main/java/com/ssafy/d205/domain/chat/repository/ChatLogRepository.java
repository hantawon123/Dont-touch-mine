package com.ssafy.d205.domain.chat.repository;

import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.util.List;

import com.ssafy.d205.domain.chat.entity.ChatLog;

public interface ChatLogRepository extends JpaRepository<ChatLog, Integer> {

    /**
     * 한 방의 한 시간대 대화. 신고 상세가 여는 조회입니다.
     *
     * <p>말한 순서대로 돌려줍니다. 도착 순서(seq)가 아니라 말한 시각(sent_at)으로 정렬하는 이유는
     * 묶음 전송이라 도착 순서가 뒤바뀔 수 있기 때문입니다. 같은 초에 여러 줄이면 seq 로 가릅니다.
     *
     * <p>ix_chat_logs_room_time 이 그대로 받습니다.
     */
    List<ChatLog> findByRoomCodeAndSentAtBetweenOrderBySentAtAscSeqAsc(
            String roomCode, String from, String to);

    /**
     * 이 사람이 최근에 한 말. 한 방에서 한 번은 실수일 수 있어도 여러 방에서 반복하면 다른
     * 판단이 되므로, 운영자가 정지를 누르기 전에 반드시 보게 됩니다.
     */
    List<ChatLog> findBySenderSeqAndSentAtGreaterThanEqualOrderBySentAtDescSeqDesc(
            Integer senderSeq, String from);

    /**
     * 한 방의 한 시간대 대화. 닉네임을 붙여 돌려줍니다 (S15P21D205-1030).
     *
     * <p>LEFT JOIN 입니다. 계정을 모르는 줄과 탈퇴한 사람의 줄도 남아야 대화가 이어집니다.
     * 한쪽 말만 남으면 뜻이 뒤집힙니다.
     *
     * <p>정렬은 말한 시각이 먼저입니다. 도착 순서(seq)로 정렬하면 묶음 전송이 늦게 도착한
     * 줄을 뒤로 밀어 대화 순서가 어긋납니다. 같은 초에 여러 줄이면 seq 로 가릅니다.
     */
    @Query(value = """
            SELECT c.chat_logs_seq AS id,
                   c.scope         AS scope,
                   c.room_code     AS roomCode,
                   u.public_id     AS userId,
                   u.nickname      AS nickname,
                   c.sender_ref    AS senderRef,
                   c.message       AS message,
                   c.masked        AS masked,
                   c.sent_at       AS sentAt
              FROM chat_logs c
              LEFT JOIN users u ON u.users_seq = c.sender_seq
             WHERE c.room_code = :roomCode
               AND c.sent_at BETWEEN :from AND :to
             ORDER BY c.sent_at, c.chat_logs_seq
            """, nativeQuery = true)
    List<AdminChatRow> findConversation(@Param("roomCode") String roomCode,
                                        @Param("from") String from,
                                        @Param("to") String to);

    /**
     * 한 사람이 최근에 한 말 전부. 방을 가리지 않습니다.
     *
     * <p>한 방에서 한 번은 실수일 수 있어도 여러 방에서 반복하면 다른 판단이 됩니다. 정지를
     * 누르기 전에 운영자가 보게 되는 자리입니다.
     */
    @Query(value = """
            SELECT c.chat_logs_seq AS id,
                   c.scope         AS scope,
                   c.room_code     AS roomCode,
                   u.public_id     AS userId,
                   u.nickname      AS nickname,
                   c.sender_ref    AS senderRef,
                   c.message       AS message,
                   c.masked        AS masked,
                   c.sent_at       AS sentAt
              FROM chat_logs c
              JOIN users u ON u.users_seq = c.sender_seq
             WHERE u.public_id = :userId
               AND c.sent_at >= :from
             ORDER BY c.sent_at DESC, c.chat_logs_seq DESC
            """, nativeQuery = true)
    List<AdminChatRow> findBySpeaker(@Param("userId") String userId, @Param("from") String from);

    /**
     * 보관 기간이 지난 채팅을 지웁니다. 아직 판단하지 않은 신고가 가리키는 구간은 남깁니다
     * (S15P21D205-1031).
     *
     * <p>신고의 context_key 는 {@code 7K2M9P#2} 꼴이고 '#' 앞이 방 코드입니다. 뒤의 숫자는
     * 피어마다 자기가 본 경기 수를 센 값이라 전역 식별자가 아니므로, 방 코드와 신고 시각
     * 앞뒤 구간으로 겹치는 줄을 지킵니다.
     *
     * <p><b>구간 폭은 관리 화면이 열 수 있는 가장 넓은 값과 같아야 합니다.</b> 좁게 잡으면
     * 운영자가 구간을 넓혀 읽던 줄이 다음 청소에 사라집니다.
     *
     * <p>검토가 끝난 신고는 지키지 않습니다. 판단이 끝났으면 근거를 계속 들고 있을 이유가
     * 개인정보 최소화보다 앞서지 않습니다. 정지까지 간 건의 근거는 정지 사유에 옮겨 적는
     * 것으로 남깁니다(S15P21D205-1036).
     *
     * <p>키가 없는 옛 신고는 어느 방인지 알 수 없어 지킬 수 없습니다. 지금 클라이언트는 항상
     * 보냅니다.
     *
     * @param threshold 이보다 오래된 줄이 대상 (yyyyMMddHHmmss, UTC)
     * @param minutes   신고 시각 앞뒤로 지킬 구간
     * @return 지운 행 수
     */
    @Modifying(clearAutomatically = true, flushAutomatically = true)
    @Query(value = """
            DELETE c FROM chat_logs c
             WHERE c.sent_at < :threshold
               AND NOT EXISTS (
                   SELECT 1
                     FROM user_reports r
                    WHERE r.status = 'PENDING'
                      AND r.deleted_at IS NULL
                      AND r.context_key IS NOT NULL
                      AND SUBSTRING_INDEX(r.context_key, '#', 1) = c.room_code
                      AND c.sent_at BETWEEN
                          DATE_FORMAT(STR_TO_DATE(r.created_at, '%Y%m%d%H%i%s')
                              - INTERVAL :minutes MINUTE, '%Y%m%d%H%i%s')
                          AND DATE_FORMAT(STR_TO_DATE(r.created_at, '%Y%m%d%H%i%s')
                              + INTERVAL :minutes MINUTE, '%Y%m%d%H%i%s')
               )
            """, nativeQuery = true)
    int deleteExpired(@Param("threshold") String threshold, @Param("minutes") int minutes);
}
