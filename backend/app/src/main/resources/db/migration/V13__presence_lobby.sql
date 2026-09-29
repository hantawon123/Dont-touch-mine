-- 로비와 경기 중을 나눕니다. IN_GAME 하나였던 자리가 IN_LOBBY | IN_GAME 둘이 됩니다.
--
-- 값이 늘어난 것뿐이라 컬럼 타입은 그대로입니다. VARCHAR(16)에 IN_LOBBY가 들어가고,
-- 저장된 데이터도 고칠 것이 없습니다 -- 기존 IN_GAME 행은 여전히 IN_GAME입니다.
-- 로비를 따로 보고하기 시작하는 것은 클라이언트가 sessionKind를 보내는 시점부터입니다.
--
-- 그래서 이 마이그레이션이 하는 일은 주석을 고치는 것뿐입니다. V3는 이미 적용됐으므로
-- 손댈 수 없고(체크섬), 스키마만 읽는 사람에게 값이 셋이라고 말하는 주석을 남겨두면
-- 그것이 유일한 기록인 채로 틀립니다. 컬럼에는 CHECK가 없어서 값의 목록을 지키는 것은
-- 애플리케이션의 PresenceStatus 뿐입니다.
ALTER TABLE user_presence
    MODIFY COLUMN status VARCHAR(16) NOT NULL
        COMMENT 'OFFLINE | ONLINE | IN_LOBBY | IN_GAME. 로비와 경기는 같은 Photon 룸이라 클라이언트가 sessionKind로 구분해 보고한다',
    MODIFY COLUMN session_id VARCHAR(64) NULL
        COMMENT 'Photon 룸 식별자. 룸 밖(OFFLINE, ONLINE)이면 NULL. 로비에서 경기로 넘어가도 같은 룸이라 값이 바뀌지 않는다';
