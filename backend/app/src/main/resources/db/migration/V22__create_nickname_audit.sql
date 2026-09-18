-- 운영자가 남의 닉네임을 바꾼 기록 (S15P21D205-1047).
--
-- users.nickname 은 바뀐 뒤 값만 들고 있습니다. 부적절한 닉네임을 치우는 것이 이 기능의 목적인데,
-- 치우고 나면 무엇을 치웠는지가 사라집니다. 그러면 나중에 "내 이름을 누가 왜 바꿨나"라는 물음에
-- 아무도 답할 수 없고, 운영자가 실수로 엉뚱한 사람을 바꿨을 때도 되돌릴 근거가 없습니다.
--
-- suspension_audit 과 같은 규칙입니다. 행은 추가만 하고 고치거나 지우지 않습니다.
--
-- 사용자가 스스로 바꾼 개명은 여기에 남지 않습니다. 이 테이블은 계정의 이름 변경 이력이 아니라
-- 운영자가 한 행위의 기록입니다. 본인 개명까지 담으려면 /accounts/me/nickname 도 여기에 써야
-- 하는데, 그건 감사가 아니라 이력이고 지금 그것을 볼 화면이 없습니다.

CREATE TABLE nickname_audit
(
    nickname_audit_seq INT UNSIGNED NOT NULL AUTO_INCREMENT,

    -- 대상 계정. 탈퇴하면 NULL 이 됩니다(아래 FK 주석).
    user_seq           INT UNSIGNED NULL,

    -- 탈퇴해도 남는 표시용 값입니다. 이력은 이 값으로 묶습니다.
    user_public_id     CHAR(36)     NOT NULL,

    -- 바꾸기 전 이름과 바꾼 뒤 이름. 둘을 다 남깁니다.
    --
    -- before_nickname 이 이 테이블의 핵심입니다. 부적절한 닉네임으로 눌렀을 때 운영자에게
    -- 사유를 따로 묻지 않는 것은, 치운 이름 자체가 그 판단의 근거이기 때문입니다.
    --
    -- after_nickname 도 남깁니다. users 를 조인해 지금 이름을 보여주면, 그 뒤에 또 바뀐 계정의
    -- 이력이 운영자가 그때 붙인 이름과 달라집니다.
    before_nickname    VARCHAR(32)  NOT NULL,
    after_nickname     VARCHAR(32)  NOT NULL,

    -- 운영자가 적은 사유. 부적절한 닉네임 버튼은 고정 문구가 들어갑니다. 길이는
    -- users.suspended_reason 과 같은 200 자입니다.
    reason             VARCHAR(200) NOT NULL,

    -- 누른 운영자의 계정 이름. suspension_audit.admin_username 과 같습니다.
    admin_username     VARCHAR(64)  NOT NULL,

    -- yyyyMMddHHmmss (UTC 고정). 다른 시각 컬럼과 같은 규칙입니다.
    acted_at           CHAR(14)     NOT NULL,

    PRIMARY KEY (nickname_audit_seq),

    -- 사용자 상세의 "이 사람의 개명 이력"입니다. user_seq 가 아니라 public_id 로 찾습니다 -
    -- 탈퇴해서 user_seq 가 NULL 이 된 행도 같은 사람의 이력으로 묶여야 합니다.
    KEY ix_nickname_audit_user (user_public_id, nickname_audit_seq),

    -- 탈퇴하면 대상만 NULL 이 되고 기록은 남습니다. 이유는 fk_suspension_audit_user 주석과
    -- 같습니다 - 대상이 떠났다고 운영자가 한 일이 없던 일이 되지 않습니다.
    CONSTRAINT fk_nickname_audit_user
        FOREIGN KEY (user_seq) REFERENCES users (users_seq) ON DELETE SET NULL
) ENGINE = InnoDB;
