-- 친구관계.
-- 하나의 관계를 한 행으로만 저장하기 위해 두 users_seq를 크기 순으로 정렬해서 넣습니다.
-- 이렇게 하면 A가 B에게 요청한 뒤 B가 A에게 요청해도 UNIQUE 제약이 막습니다.
-- 애플리케이션에서 양방향을 조회해 검사하는 방식은 동시 요청에 뚫립니다.
CREATE TABLE friendships
(
    friendships_seq  INT UNSIGNED NOT NULL AUTO_INCREMENT,

    -- 항상 작은 users_seq
    user_low_seq     INT UNSIGNED NOT NULL,
    -- 항상 큰 users_seq
    user_high_seq    INT UNSIGNED NOT NULL,

    -- 누가 요청을 보냈는지.
    -- 관계를 대칭으로 저장하므로 이 값이 없으면 수락 버튼을 어느 쪽에 보여줄지 알 수 없습니다.
    requested_by_seq INT UNSIGNED NOT NULL,

    -- PENDING | ACCEPTED
    -- 거절과 친구 삭제는 상태가 아니라 행 삭제로 처리합니다.
    -- REJECTED를 남기면 uk_friendships_pair 때문에 같은 상대에게 다시 요청할 수 없게 됩니다.
    status           VARCHAR(16)  NOT NULL,

    created_at       CHAR(14)     NOT NULL,
    -- PENDING 동안 NULL
    accepted_at      CHAR(14)     NULL,

    PRIMARY KEY (friendships_seq),

    UNIQUE KEY uk_friendships_pair (user_low_seq, user_high_seq),

    -- 정렬 규칙을 DB가 강제합니다.
    -- 등호가 없으므로 자기 자신과 친구를 맺는 것도 함께 막힙니다.
    CONSTRAINT ck_friendships_order CHECK (user_low_seq < user_high_seq),

    -- uk_friendships_pair가 user_low_seq로 시작하므로 낮은 쪽 조회는 그 인덱스를 씁니다.
    -- 친구 목록은 양방향을 OR로 조회하므로 높은 쪽 단독 인덱스가 따로 필요합니다.
    KEY ix_friendships_high (user_high_seq),

    -- "내가 보낸 요청" / "내가 받은 요청" 조회용. FK도 이 인덱스를 사용합니다.
    KEY ix_friendships_requested_by (requested_by_seq),

    CONSTRAINT fk_friendships_low
        FOREIGN KEY (user_low_seq) REFERENCES users (users_seq) ON DELETE CASCADE,
    CONSTRAINT fk_friendships_high
        FOREIGN KEY (user_high_seq) REFERENCES users (users_seq) ON DELETE CASCADE,
    CONSTRAINT fk_friendships_requested_by
        FOREIGN KEY (requested_by_seq) REFERENCES users (users_seq) ON DELETE CASCADE
) ENGINE = InnoDB;


-- 차단.
-- 친구관계와 달리 방향이 있습니다. (10, 25)는 "10번이 25번을 차단했다"이고
-- (25, 10)은 별개 행입니다. 대칭인 friendships.status에 담을 수 없는 이유입니다.
--
-- 차단은 양방향으로 적용합니다. 즉 A가 B를 차단하면 B도 A를 볼 수 없고 요청도 보낼 수
-- 없습니다. 차단당한 쪽이 계속 요청을 보낼 수 있으면 차단의 의미가 없습니다.
-- 그래서 검사 쿼리는 두 방향을 모두 확인합니다.
--
--   WHERE NOT EXISTS (
--     SELECT 1 FROM user_blocks
--      WHERE (blocker_seq = :me    AND blocked_seq = :other)
--         OR (blocker_seq = :other AND blocked_seq = :me))
CREATE TABLE user_blocks
(
    -- 차단한 사람
    blocker_seq INT UNSIGNED NOT NULL,
    -- 차단당한 사람
    blocked_seq INT UNSIGNED NOT NULL,
    created_at  CHAR(14)     NOT NULL,

    -- 두 컬럼의 조합이 자연키이므로 대리키를 두지 않습니다.
    PRIMARY KEY (blocker_seq, blocked_seq),

    CONSTRAINT ck_user_blocks_not_self CHECK (blocker_seq <> blocked_seq),

    -- 위 쿼리의 두 번째 줄을 위한 인덱스입니다.
    -- "내가 차단한 사람"은 PK 선두 컬럼으로 조회되지만 "나를 차단한 사람"은
    -- 두 번째 컬럼 단독 조회여서 이 인덱스가 없으면 전체 스캔이 됩니다.
    KEY ix_user_blocks_blocked (blocked_seq),

    CONSTRAINT fk_user_blocks_blocker
        FOREIGN KEY (blocker_seq) REFERENCES users (users_seq) ON DELETE CASCADE,
    CONSTRAINT fk_user_blocks_blocked
        FOREIGN KEY (blocked_seq) REFERENCES users (users_seq) ON DELETE CASCADE
) ENGINE = InnoDB;
