-- 플레이 로그. 명세는 docs/analytics-events.md, 결정 근거는 지라 S15P21D205-785.
--
-- 게임 스키마(d205)와 다른 스키마에 있고 Flyway 인스턴스도 다릅니다. 이 디렉터리의
-- 마이그레이션은 수집 서비스(analytics 모듈)의 Flyway 가 돌리고, db/migration 의 것은 계정 서비스가
-- 돌립니다. 버전 번호가 겹쳐도 서로 무관합니다.
--
-- JPA 엔티티가 없습니다. 쓰기는 JdbcTemplate 배치 insert 하나뿐이고 읽기는 Metabase 가
-- 합니다. 엔티티를 두면 ddl-auto=validate 가 검증해 주는 대신 행 하나마다 객체를 만들어야
-- 하는데, 이 테이블은 초당 수십 행이 들어오는 곳입니다.
CREATE TABLE game_event
(
    game_event_seq    BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    -- 클라이언트 벽시계. UTC. 조작될 수 있어 서버가 범위(지금-7일 ~ 지금+5분)를 검증합니다.
    -- 파티션 키 후보이기도 해서 유니크 키마다 들어갑니다.
    occurred_at       DATETIME(3)     NOT NULL,

    -- 서버가 받은 시각. UTC. occurred_at 과 크게 벌어지면 스풀 재전송이거나 시계가 틀린 것.
    received_at       DATETIME(3)     NOT NULL,

    -- 보낸 앱 실행 단위. 호스트가 남을 대신해 보낸 이벤트면 호스트의 세션입니다.
    -- user_presence.session_id(Photon 방 코드)와 다른 것이라 이름을 다르게 둡니다.
    client_session_id CHAR(36)        NOT NULL,

    -- 세션 안에서 0부터 단조 증가. 스풀 재전송 중복을 아래 UNIQUE 키가 막습니다.
    client_seq        INT UNSIGNED    NOT NULL,

    -- Photon 방 코드. 매치 시작 전 로비 이탈을 묶는 유일한 열쇠입니다.
    room_code         CHAR(6)         NULL,
    match_id          CHAR(36)        NULL,

    -- 호스트의 Fusion ServerTime 기준 경기 시각. 경기 안 시간 계산은 전부 이 값으로 합니다.
    match_time_ms     INT UNSIGNED    NULL,

    -- 행동의 주체. users.public_id. FK 를 걸지 않습니다 - 게임 DB 와 스키마가 다르고,
    -- 탈퇴하면 이 값만 NULL 로 바꿔 행은 남깁니다(S15P21D205-871).
    user_public_id    CHAR(36)        NULL,

    event_name        VARCHAR(64)     NOT NULL,

    -- 코드의 MatchPhase 이름 그대로(Waiting/Hiding/Searching/Highlight/Result).
    phase             VARCHAR(16)     NULL,
    map_id            VARCHAR(32)     NULL,

    -- 좌표는 params 가 아니라 컬럼입니다. 히트맵 집계가 이 로그의 핵심이라 JSON_EXTRACT 를
    -- 붙일 수 없습니다.
    pos_x             FLOAT           NULL,
    pos_y             FLOAT           NULL,
    pos_z             FLOAT           NULL,

    from_host         BOOLEAN         NOT NULL,
    schema_ver        SMALLINT        NOT NULL,
    params            JSON            NULL,

    -- 복합 PK 는 나중에 날짜 파티셔닝을 켤 문을 열어 두기 위한 것입니다. MySQL 은 파티션
    -- 키가 모든 유니크 키에 들어가야 합니다. 지금 켜라는 것이 아닙니다.
    PRIMARY KEY (game_event_seq, occurred_at),

    -- 재전송 중복 제거. INSERT IGNORE 가 이 키에 걸린 행을 조용히 버립니다.
    -- received_at 은 재전송마다 달라져 키에 못 쓰고, occurred_at 은 파티션 규칙 때문에 들어갑니다.
    UNIQUE KEY uk_game_event_client (client_session_id, client_seq, occurred_at),

    KEY ix_game_event_match (match_id, occurred_at),
    KEY ix_game_event_name  (event_name, map_id, occurred_at)
) ENGINE = InnoDB;
