package com.ssafy.d205.domain.ops.entity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;

import java.math.BigDecimal;

/**
 * 1분에 한 번 남기는 운영 지표 한 행. 접속자와 서버 자원을 같은 순간에 잽니다.
 *
 * <p>한 번 쓰고 고치지 않습니다. 그래서 수정 메서드가 없고 시각이 곧 키입니다. 컬럼의 뜻은
 * V18 마이그레이션에 있습니다.
 */
@Entity
@Table(name = "ops_samples")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class OpsSample {

    @Id
    @Column(name = "sampled_at", length = 14)
    private String sampledAt;

    @Column(name = "online", nullable = false)
    private int online;

    @Column(name = "in_lobby", nullable = false)
    private int inLobby;

    @Column(name = "in_game", nullable = false)
    private int inGame;

    @Column(name = "socket_connections", nullable = false)
    private int socketConnections;

    @Column(name = "signups", nullable = false)
    private int signups;

    @Column(name = "deletions", nullable = false)
    private int deletions;

    @Column(name = "cpu_pct", nullable = false, precision = 5, scale = 1)
    private BigDecimal cpuPct;

    @Column(name = "heap_used_mb", nullable = false)
    private int heapUsedMb;

    @Column(name = "heap_max_mb", nullable = false)
    private int heapMaxMb;

    @Column(name = "db_pool_active", nullable = false)
    private int dbPoolActive;

    @Column(name = "db_pool_max", nullable = false)
    private int dbPoolMax;

    public OpsSample(String sampledAt, int online, int inLobby, int inGame, int socketConnections,
                     int signups, int deletions, double cpuPct, int heapUsedMb, int heapMaxMb,
                     int dbPoolActive, int dbPoolMax) {
        this.sampledAt = sampledAt;
        this.online = online;
        this.inLobby = inLobby;
        this.inGame = inGame;
        this.socketConnections = socketConnections;
        this.signups = signups;
        this.deletions = deletions;
        this.cpuPct = BigDecimal.valueOf(cpuPct).setScale(1, java.math.RoundingMode.HALF_UP);
        this.heapUsedMb = heapUsedMb;
        this.heapMaxMb = heapMaxMb;
        this.dbPoolActive = dbPoolActive;
        this.dbPoolMax = dbPoolMax;
    }
}
