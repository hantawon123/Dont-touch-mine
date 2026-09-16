package com.ssafy.d205.domain.chat.repository;

import org.springframework.data.jpa.repository.JpaRepository;

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
}
