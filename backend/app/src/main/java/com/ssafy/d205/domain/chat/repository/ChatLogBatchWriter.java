package com.ssafy.d205.domain.chat.repository;

import lombok.RequiredArgsConstructor;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.stereotype.Repository;

import java.sql.Types;
import java.util.List;

import com.ssafy.d205.domain.chat.entity.ChatScope;

/**
 * chat_logs 배치 insert (S15P21D205-1077).
 *
 * <p>JPA 를 거치지 않습니다. {@code chat_logs_seq} 가 AUTO_INCREMENT 이고 엔티티가
 * {@code GenerationType.IDENTITY} 라, Hibernate 는 생성된 키를 행마다 받아야 해서 <b>insert 배치를
 * 아예 끕니다.</b> {@code hibernate.jdbc.batch_size} 를 넣어도 달라지지 않습니다. 실제로 세어 보면
 * 200줄 묶음 하나가 문장 200개로 나갔습니다.
 *
 * <p>같은 이유로 분석 모듈이 먼저 이 길을 택했습니다
 * ({@code com.ssafy.d205.domain.analytics.repository.GameEventWriter}). 그쪽은 이벤트라 양이 훨씬
 * 많았을 뿐 구조는 같습니다.
 *
 * <p>운영 JDBC URL 에 {@code rewriteBatchedStatements=true} 가 있어 이 배치는 multi-row INSERT 한
 * 문장으로 나갑니다. 없으면 배치를 써도 행마다 왕복합니다 - 같은 200줄을 재 보면 12.8ms 와
 * 125ms 로 갈렸습니다.
 *
 * <p>키를 돌려받지 않습니다. 넣고 나서 그 행을 다시 쓸 일이 없고, 조회는 방 코드와 시각으로 합니다.
 */
@Repository
@RequiredArgsConstructor
public class ChatLogBatchWriter {

    private static final String INSERT = """
            INSERT INTO chat_logs
                (room_code, scope, sender_seq, sender_ref, message, masked, sent_at, created_at)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?)
            """;

    private final JdbcTemplate jdbcTemplate;

    /**
     * 넣을 한 줄.
     *
     * <p>줄마다 값을 함께 들고 다닙니다. 원래 요청 목록과 별도 배열을 위치로 맞추면, 같은 말을 두 번
     * 한 사람이 있을 때 자리를 찾다가 엉뚱한 줄의 발신자와 판정을 붙일 수 있습니다.
     *
     * @param senderSeq 계정 seq. 모르거나 탈퇴했으면 null
     */
    public record Row(String roomCode, ChatScope scope, Integer senderSeq, String senderRef,
                      String message, boolean masked, String sentAt, String createdAt) {
    }

    /** 한 묶음을 넣고 넣은 줄 수를 돌려줍니다. */
    public int insertAll(List<Row> rows) {
        if (rows.isEmpty()) {
            return 0;
        }
        jdbcTemplate.batchUpdate(INSERT, rows, rows.size(), (statement, row) -> {
            statement.setString(1, row.roomCode());
            statement.setString(2, row.scope().name());
            if (row.senderSeq() == null) {
                statement.setNull(3, Types.INTEGER);
            } else {
                statement.setInt(3, row.senderSeq());
            }
            statement.setString(4, row.senderRef());
            statement.setString(5, row.message());
            statement.setBoolean(6, row.masked());
            statement.setString(7, row.sentAt());
            statement.setString(8, row.createdAt());
        });
        return rows.size();
    }
}
