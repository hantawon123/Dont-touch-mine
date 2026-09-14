-- 접속 상태.
-- users와 분리한 이유는 하트비트가 30초마다 UPDATE를 내기 때문입니다.
-- 거의 바뀌지 않는 계정 데이터와 같은 행에 두면 users 페이지와 인덱스가 계속 더러워집니다.
CREATE TABLE user_presence
(
    -- 유저당 한 행이므로 PK가 곧 FK입니다. 대리키가 할 일이 없습니다.
    user_seq     INT UNSIGNED NOT NULL,

    -- OFFLINE | ONLINE | IN_GAME
    -- IN_GAME은 Photon에서 룸 정보를 받아오는 연동이 붙기 전까지 쓰이지 않습니다.
    status       VARCHAR(16)  NOT NULL,

    -- Photon 룸 식별자. IN_GAME이 아니면 NULL입니다.
    session_id   VARCHAR(64)  NULL,

    -- 마지막 생존 신호 (30초 주기).
    -- 클라이언트가 크래시로 죽으면 종료 신호가 오지 않으므로, 스케줄러가 이 값이
    -- 오래된 행을 OFFLINE으로 바꿉니다. 이 스윕이 없으면 유령 온라인이 쌓입니다.
    heartbeat_at CHAR(14)     NOT NULL,

    -- 상태가 마지막으로 바뀐 시각. heartbeat_at과 구분합니다.
    -- 하트비트는 30초마다 갱신되지만 상태 변화는 드물게 일어납니다.
    updated_at   CHAR(14)     NOT NULL,

    PRIMARY KEY (user_seq),

    -- 스윕 쿼리용. 컬럼 순서가 status 먼저인 것은 의도된 것이며,
    -- 그래서 스윕은 반드시 아래 형태로 작성해야 합니다.
    --
    --   WHERE status IN ('ONLINE', 'IN_GAME') AND heartbeat_at < :threshold
    --
    -- status <> 'OFFLINE'으로 쓰면 선두 컬럼이 부등호가 되어 이 인덱스를 제대로
    -- 타지 못합니다. IN 목록이어야 선두 컬럼이 동등 비교로 처리됩니다.
    KEY ix_user_presence_sweep (status, heartbeat_at),

    CONSTRAINT fk_user_presence_user
        FOREIGN KEY (user_seq) REFERENCES users (users_seq) ON DELETE CASCADE
) ENGINE = InnoDB;
