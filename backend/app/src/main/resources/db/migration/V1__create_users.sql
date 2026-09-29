-- 게임 계정.
-- 로그인 자격증명은 user_identities가 담당하므로 이 테이블에는 없습니다.
CREATE TABLE users
(
    users_seq  INT UNSIGNED NOT NULL AUTO_INCREMENT,

    -- 외부에 노출하는 식별자. Photon UserId와 API 응답에 이 값을 씁니다.
    -- users_seq를 내보내면 가입자 수가 드러나고 다른 계정을 순회할 수 있습니다.
    -- 애플리케이션이 UUIDv4로 생성하며, 한 번 정해지면 바뀌지 않습니다.
    public_id  CHAR(36)     NOT NULL,

    -- 32자는 클라이언트의 NetworkString<_32> 제약에서 온 값이라 늘릴 수 없습니다.
    -- UNIQUE는 사칭을 막기 위한 것이며, 서버 콜레이션이 대소문자를 구분하지 않으므로
    -- Player와 player는 같은 닉네임으로 취급됩니다.
    nickname   VARCHAR(32)  NOT NULL,

    -- yyyyMMddHHmmss (UTC 고정).
    -- 고정 길이라 사전순 정렬이 시간순 정렬과 일치하므로 범위 조회와 인덱스가 정상 동작합니다.
    -- 문자열에는 타임존 정보가 없으니 UTC 규칙을 어기면 조용히 어긋납니다.
    created_at CHAR(14)     NOT NULL,
    updated_at CHAR(14)     NOT NULL,

    PRIMARY KEY (users_seq),
    UNIQUE KEY uk_users_public_id (public_id),

    -- 닉네임 검색은 접두사 일치(LIKE 'query%')이므로 이 인덱스를 그대로 사용합니다.
    UNIQUE KEY uk_users_nickname (nickname)
) ENGINE = InnoDB;


-- 계정에 연결된 외부 신원.
-- 지금은 DEVICE 하나뿐이고, Steam이 붙으면 같은 유저에 STEAM 행이 추가됩니다.
-- users에 steam_id 컬럼을 두지 않는 이유는 플랫폼이 늘 때마다 컬럼이 늘어나고
-- 한 계정에 기기를 여러 대 연결할 수 없기 때문입니다.
CREATE TABLE user_identities
(
    user_identities_seq INT UNSIGNED NOT NULL AUTO_INCREMENT,
    user_seq            INT UNSIGNED NOT NULL,

    -- DEVICE | STEAM | EPIC
    -- MySQL ENUM을 쓰지 않는 이유는 값을 추가할 때마다 ALTER TABLE이 필요하고
    -- JPA의 @Enumerated(STRING)과도 맞지 않기 때문입니다.
    -- 20자는 PLAYSTATION, NINTENDO_SWITCH 같은 이름까지 담기 위한 여유입니다.
    provider            VARCHAR(20)  NOT NULL,

    -- DEVICE는 클라이언트가 첫 실행에 생성한 UUID(36자), STEAM은 SteamID64(17자)입니다.
    -- 길이가 서로 다르므로 CHAR이 아니라 VARCHAR입니다.
    --
    -- 주의: 이 값은 users.public_id와 성질이 정반대입니다. public_id는 공개 식별자지만
    -- 이 값은 자격증명입니다. 어떤 API 응답에도 담아서는 안 됩니다.
    provider_user_id    VARCHAR(36)  NOT NULL,

    linked_at           CHAR(14)     NOT NULL,

    PRIMARY KEY (user_identities_seq),

    -- 한 기기나 한 Steam 계정이 두 유저에 붙는 것을 DB가 막습니다.
    UNIQUE KEY uk_user_identities_provider (provider, provider_user_id),

    -- "이 유저에 연결된 신원 전부" 조회용.
    KEY ix_user_identities_user (user_seq),

    CONSTRAINT fk_user_identities_user
        FOREIGN KEY (user_seq) REFERENCES users (users_seq) ON DELETE CASCADE
) ENGINE = InnoDB;
