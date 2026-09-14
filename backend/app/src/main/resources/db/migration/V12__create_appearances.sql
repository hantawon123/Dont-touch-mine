-- 캐릭터 외형. 옷장에서 고른 파츠를 계정에 남깁니다.
--
-- users 에 컬럼 넷을 더하지 않고 유저당 한 행으로 뗀 이유가 있습니다. 행이 있으면 저장한
-- 것이고 없으면 아직 고르지 않은 것입니다. users 에 nullable 컬럼 넷으로 두면 "넷 다
-- NULL 이거나 넷 다 값"이라는 불변식을 코드가 지켜야 하는데, 행의 존재로 표현하면 그
-- 불변식이 스키마에 있습니다. user_presence 와 같은 자리입니다.
--
-- 서버는 파츠 목록을 모릅니다. 카탈로그는 클라이언트 자산이고, 서버가 목록을 알면 파츠를
-- 하나 늘릴 때마다 배포가 필요해집니다. 여기 들어오는 값은 클라이언트가 정한 파츠 id 이고
-- 서버는 길이와 문자만 봅니다. 클라이언트가 모르는 id 를 읽으면 기본 파츠로 대체해야 합니다.
CREATE TABLE user_appearances
(
    -- 유저당 한 행이므로 PK 가 곧 FK 입니다.
    user_seq   INT UNSIGNED NOT NULL,

    -- 32 는 저장 길이일 뿐입니다. 파츠 id 문자열은 네트워크를 타지 않습니다. 로비와 경기에서
    -- 외형은 카탈로그 인덱스(byte) 로 복제하고, 문자열 id 는 서버와 클라이언트 사이에서만
    -- 오갑니다. 그래서 이 길이는 Photon 쪽 한도와 무관하고, 늘려도 복제에 영향이 없습니다.
    -- 닉네임(NetworkString<_32>) 과 같은 숫자인 것은 우연입니다.
    -- AppearancePolicy 가 같은 값을 들고 있고, 요청 검증이 그것을 씁니다.
    body_color VARCHAR(32)  NOT NULL,
    hood       VARCHAR(32)  NOT NULL,
    shoes      VARCHAR(32)  NOT NULL,
    face       VARCHAR(32)  NOT NULL,

    updated_at CHAR(14)     NOT NULL,

    PRIMARY KEY (user_seq),

    -- 탈퇴하면 함께 사라집니다. "탈퇴는 흔적을 남기지 않는다"는 약속의 일부입니다.
    CONSTRAINT fk_user_appearances_user
        FOREIGN KEY (user_seq) REFERENCES users (users_seq) ON DELETE CASCADE
) ENGINE = InnoDB;
