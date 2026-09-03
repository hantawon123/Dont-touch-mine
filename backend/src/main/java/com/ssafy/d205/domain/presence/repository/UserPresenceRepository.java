package com.ssafy.d205.domain.presence.repository;

import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import com.ssafy.d205.domain.presence.entity.UserPresence;

public interface UserPresenceRepository extends JpaRepository<UserPresence, Integer> {

    /**
     * 하트비트가 끊긴 행을 OFFLINE 으로 내립니다. 스윕이 부릅니다.
     *
     * <p>조건을 status IN ('ONLINE', 'IN_GAME') 으로 쓰는 것이 중요합니다.
     * ix_user_presence_sweep 이 (status, heartbeat_at) 순서라 선두 컬럼이 동등 비교여야
     * 인덱스를 탑니다. 부등호로 "OFFLINE 이 아닌 것"을 쓰면 선두가 범위 조건이 되어
     * 제대로 타지 못합니다. V3 주석에 같은 내용이 적혀 있습니다.
     *
     * <p>session_id 도 함께 비웁니다. IN_GAME 이 아니면 NULL 이라는 규칙을 지켜야
     * 나중에 그 값을 읽는 쪽이 상태를 함께 확인하지 않아도 됩니다.
     */
    @Modifying
    @Query(value = """
            UPDATE user_presence
               SET status = 'OFFLINE',
                   session_id = NULL,
                   updated_at = :now
             WHERE status IN ('ONLINE', 'IN_GAME')
               AND heartbeat_at < :threshold
            """, nativeQuery = true)
    int markStaleOffline(@Param("threshold") String threshold, @Param("now") String now);
}
