package com.ssafy.d205.domain.presence.repository;

import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.util.Collection;

import com.ssafy.d205.domain.presence.entity.PresenceStatus;
import com.ssafy.d205.domain.presence.entity.UserPresence;

public interface UserPresenceRepository extends JpaRepository<UserPresence, Integer> {

    /** 그 상태인 사람 수. 운영 지표 샘플러와 개요 탭이 상태 셋에 대해 부릅니다. ix_user_presence_sweep 의 선두 컬럼입니다. */
    long countByStatus(PresenceStatus status);

    /**
     * 하트비트가 끊긴 행을 OFFLINE 으로 내립니다. 스윕이 부릅니다.
     *
     * <p>조건을 status IN ('ONLINE', 'IN_LOBBY', 'IN_GAME') 으로 쓰는 것이 중요합니다.
     * ix_user_presence_sweep 이 (status, heartbeat_at) 순서라 선두 컬럼이 동등 비교여야
     * 인덱스를 탑니다. 부등호로 "OFFLINE 이 아닌 것"을 쓰면 선두가 범위 조건이 되어
     * 제대로 타지 못합니다. V3 주석에 같은 내용이 적혀 있습니다.
     *
     * <p><b>PresenceStatus 에 값을 더하면 이 목록에도 넣어야 합니다.</b> 빠뜨리면 그 상태로
     * 남은 사람은 하트비트가 끊겨도 영원히 스윕되지 않습니다. 조회는 PresenceTimeout 이
     * 계산해서 맞게 나오므로 눈에 띄지 않고, 저장된 값만 조용히 틀립니다.
     *
     * <p>session_id 도 함께 비웁니다. 룸 안이 아니면 NULL 이라는 규칙을 지켜야
     * 나중에 그 값을 읽는 쪽이 상태를 함께 확인하지 않아도 됩니다.
     */
    @Modifying
    @Query(value = """
            UPDATE user_presence
               SET status = 'OFFLINE',
                   session_id = NULL,
                   updated_at = :now
             WHERE status IN ('ONLINE', 'IN_LOBBY', 'IN_GAME')
               AND heartbeat_at < :threshold
            """, nativeQuery = true)
    int markStaleOffline(@Param("threshold") String threshold, @Param("now") String now);

    /**
     * 알림 채널에 붙어 있는 사람들의 하트비트를 한 번에 밉니다. PresenceHeartbeat 가 부릅니다.
     *
     * <p><b>이 한 문장이 클라이언트의 주기 PUT 을 대체합니다.</b> 예전에는 접속자 천 명이면
     * 30초마다 요청 천 개가 들어와 각각 트랜잭션을 열고 users 와 user_presence 를 조회한 뒤
     * UPDATE 를 냈습니다. 지금은 접속자가 몇 명이든 문장 하나입니다.
     *
     * <p>status 는 건드리지 않습니다. 접속·종료·방 이동은 그 순간에 각자 씁니다. 여기서
     * 상태까지 손대면 방금 OFFLINE 이 된 사람을 한 틱 늦게 되살리는 경합이 생깁니다.
     *
     * <p><b>빈 목록으로 부르지 마세요.</b> {@code IN ()} 는 MySQL 문법 오류입니다. 붙어 있는
     * 사람이 없으면 호출부가 아예 건너뜁니다.
     */
    @Modifying
    @Query(value = """
            UPDATE user_presence
               SET heartbeat_at = :now
             WHERE user_seq IN (:userSeqs)
            """, nativeQuery = true)
    int refreshHeartbeats(@Param("userSeqs") Collection<Integer> userSeqs, @Param("now") String now);
}
