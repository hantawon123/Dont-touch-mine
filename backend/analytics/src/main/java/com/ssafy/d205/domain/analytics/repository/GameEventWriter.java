package com.ssafy.d205.domain.analytics.repository;

import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Repository;

import java.sql.Types;
import java.time.LocalDateTime;
import java.time.ZoneOffset;
import java.util.List;

import org.springframework.jdbc.core.JdbcTemplate;
import com.ssafy.d205.domain.analytics.entity.GameEventRow;

/**
 * game_event 배치 insert. 이 프로젝트에서 JPA 를 거치지 않는 유일한 쓰기입니다.
 *
 * <p>INSERT IGNORE 인 이유는 uk_game_event_client 입니다. 클라이언트가 202 를 못 받아 스풀에서
 * 다시 보낸 이벤트는 같은 (client_session_id, client_seq, occurred_at) 으로 들어오고, IGNORE 가
 * 그 행만 조용히 버립니다. 나머지 행은 정상으로 들어갑니다.
 *
 * <p>시각은 UTC LocalDateTime 으로 바인딩합니다. Instant 를 그대로 넘기면 드라이버가 세션
 * 타임존으로 바꾸는데, 그 값이 게임 DB(Asia/Seoul)와 분석 DB(UTC) 에서 다릅니다. LocalDateTime
 * 은 타임존 변환 없이 그대로 들어가므로 "game_event 의 DATETIME 은 전부 UTC" 가 코드로 보장됩니다.
 *
 * <p>rewriteBatchedStatements=true 가 URL 에 있어 이 배치는 multi-row INSERT 한 문장으로 나갑니다.
 * 없으면 행마다 왕복해서 5,000행이 5,000번이 됩니다.
 */
@Repository
@RequiredArgsConstructor
public class GameEventWriter {

    private static final String INSERT = """
            INSERT IGNORE INTO game_event
                (occurred_at, received_at, client_session_id, client_seq, room_code, match_id,
                 match_time_ms, user_public_id, event_name, phase, map_id, pos_x, pos_y, pos_z,
                 from_host, schema_ver, params)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """;

    private final JdbcTemplate jdbcTemplate;

    /** 넣습니다. 예외는 그대로 올립니다. 삼키는 것은 플러셔의 일입니다. */
    public void insertAll(List<GameEventRow> rows) {
        jdbcTemplate.batchUpdate(INSERT, rows, rows.size(), (ps, r) -> {
            ps.setObject(1, utc(r.occurredAt()));
            ps.setObject(2, utc(r.receivedAt()));
            ps.setString(3, r.clientSessionId());
            ps.setLong(4, r.clientSeq());
            ps.setString(5, r.roomCode());
            ps.setString(6, r.matchId());
            if (r.matchTimeMs() == null) {
                ps.setNull(7, Types.BIGINT);
            } else {
                ps.setLong(7, r.matchTimeMs());
            }
            ps.setString(8, r.userPublicId());
            ps.setString(9, r.eventName());
            ps.setString(10, r.phase());
            ps.setString(11, r.mapId());
            setFloat(ps, 12, r.posX());
            setFloat(ps, 13, r.posY());
            setFloat(ps, 14, r.posZ());
            ps.setBoolean(15, r.fromHost());
            ps.setShort(16, r.schemaVer());
            ps.setString(17, r.params());
        });
    }

    private static LocalDateTime utc(java.time.Instant instant) {
        return LocalDateTime.ofInstant(instant, ZoneOffset.UTC);
    }

    private static void setFloat(java.sql.PreparedStatement ps, int index, Float value)
            throws java.sql.SQLException {
        if (value == null) {
            ps.setNull(index, Types.FLOAT);
        } else {
            ps.setFloat(index, value);
        }
    }
}
