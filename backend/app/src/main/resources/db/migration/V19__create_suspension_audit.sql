-- 정지·해제 감사 로그 (S15P21D205-974).
--
-- users 는 지금 상태만 들고 있습니다. suspended_at 과 suspended_reason 은 해제하면 NULL 이 되고,
-- 다시 정지하면 덮어씁니다. 그래서 "누가 언제 왜 눌렀나"와 "전에도 정지된 적이 있나"에 답할 수
-- 없습니다. 계정을 막는 일은 그 사람에게 서비스가 통째로 멈추는 일이고, 나중에 반드시 물어보는
-- 사람이 생깁니다. 그 질문에 답하는 것이 이 테이블입니다.
--
-- 행은 추가만 하고 고치거나 지우지 않습니다. 고칠 수 있는 감사 로그는 감사 로그가 아닙니다.
--
-- 해제에도 한 행을 남깁니다. 정지만 남기면 "지금 정상인 계정"과 "한 번도 정지된 적 없는 계정"이
-- 구분되지 않습니다.

CREATE TABLE suspension_audit
(
    suspension_audit_seq INT UNSIGNED NOT NULL AUTO_INCREMENT,

    -- 대상 계정. 탈퇴하면 NULL 이 됩니다(아래 FK 주석).
    user_seq             INT UNSIGNED NULL,

    -- 탈퇴해도 남는 표시용 값입니다. user_seq 가 NULL 이 되어도 화면이 "누구를 정지했는지"를
    -- 말할 수 있어야 합니다.
    --
    -- 닉네임은 그 시점의 값입니다. users 를 조인해 지금 닉네임을 보여주면, 정지된 뒤 개명한
    -- 계정의 이력이 운영자가 그때 본 이름과 달라집니다. 감사 로그는 그때 무엇을 보고 눌렀는지를
    -- 남기는 것이라 그 시점 값이 맞습니다.
    user_public_id       CHAR(36)     NOT NULL,
    user_nickname        VARCHAR(32)  NOT NULL,

    -- SUSPEND 또는 LIFT. ENUM 을 쓰지 않는 이유는 user_reports.reason 과 같습니다 - 값을 하나
    -- 늘릴 때마다 마이그레이션이 필요해집니다.
    action               VARCHAR(8)   NOT NULL,

    -- 정지 사유. 해제에는 없으므로 NULL 입니다. 길이는 users.suspended_reason 과 같습니다.
    reason               VARCHAR(200) NULL,

    -- 누른 운영자의 계정 이름. 지금은 환경변수 계정 하나뿐이지만, 계정이 늘어도 이 컬럼은
    -- 그대로 씁니다.
    admin_username       VARCHAR(64)  NOT NULL,

    -- yyyyMMddHHmmss (UTC 고정). 다른 시각 컬럼과 같은 규칙입니다.
    acted_at             CHAR(14)     NOT NULL,

    PRIMARY KEY (suspension_audit_seq),

    -- 사용자 상세의 "이 사람의 정지 이력"입니다. user_seq 가 아니라 public_id 로 찾습니다.
    -- 탈퇴해서 user_seq 가 NULL 이 된 행도 같은 사람의 이력으로 묶여야 하기 때문입니다.
    KEY ix_suspension_audit_user (user_public_id, suspension_audit_seq),

    -- "최근 해제 이력"은 action 으로 거르고 최신순으로 봅니다. seq 가 뒤라 정렬이 인덱스로
    -- 끝납니다. acted_at 대신 seq 로 정렬하는 이유는 같은 초에 두 번 눌렀을 때도 순서가
    -- 정해지기 때문입니다.
    KEY ix_suspension_audit_action (action, suspension_audit_seq),

    -- 탈퇴하면 대상만 NULL 이 되고 기록은 남습니다. user_reports.reported_seq 의 CASCADE 와
    -- 반대인데, 그쪽은 신고당한 사람에 대한 기록이라 대상이 없으면 볼 이유가 없습니다.
    -- 이쪽은 대상에 대한 기록이 아니라 운영자가 한 행위의 기록입니다. 대상이 떠났다고
    -- "누가 무엇을 했다"가 없던 일이 되지 않습니다.
    --
    -- 그래서 탈퇴 후 재가입으로 정지 이력을 지울 수 없습니다. 다만 새 계정은 새 public_id 라
    -- 옛 이력과 이어지지 않습니다. 그것을 잇는 것은 탈퇴해도 남는 신원 고정점이 필요한 별개
    -- 문제이고, user_reports 주석이 말하는 것과 같은 문제입니다.
    CONSTRAINT fk_suspension_audit_user
        FOREIGN KEY (user_seq) REFERENCES users (users_seq) ON DELETE SET NULL
) ENGINE = InnoDB;
