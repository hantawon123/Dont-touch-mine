-- 사용자 신고.
--
-- 차단(V8 에서 없앰)이 있던 자리지만 성질이 반대입니다. 차단은 즉시 효력이 있어서
-- 검색과 친구 요청과 초대가 모두 그 테이블을 읽어야 했습니다. 신고는 상대에게 아무
-- 일도 일으키지 않습니다. 아무 경로도 이 테이블을 읽지 않고, 사람이 나중에 봅니다.
-- 그래서 조회 인덱스도 운영자가 쓸 것 하나만 둡니다.
CREATE TABLE user_reports
(
    user_reports_seq INT UNSIGNED NOT NULL AUTO_INCREMENT,

    -- 신고자가 탈퇴하면 NULL 이 됩니다. 아래 FK 주석 참고.
    reporter_seq     INT UNSIGNED NULL,
    reported_seq     INT UNSIGNED NOT NULL,

    -- ReportReason 의 이름을 그대로 담습니다. 서버가 목록에 대해 검증하므로 이 컬럼에
    -- 목록에 없는 값이 들어오지 않습니다. ENUM 타입을 쓰지 않는 이유는 사유를 하나
    -- 늘릴 때마다 마이그레이션이 필요해지기 때문입니다.
    reason           VARCHAR(32)  NOT NULL,

    -- 신고자가 적는 한 줄. 없어도 됩니다.
    -- 사유만으로는 운영자가 판단할 맥락이 없고, 자유 텍스트만 받으면 분류할 수 없어서
    -- 둘을 함께 둡니다.
    memo             VARCHAR(200) NULL,

    created_at       CHAR(14)     NOT NULL,

    -- ReportStatus 의 이름. 운영자가 이미 본 신고를 다시 보지 않게 하는 표시입니다.
    --
    -- DEFAULT 는 실제로 쓰이지 않습니다. UserReport 생성자가 늘 PENDING 을 넣기 때문에
    -- JPA 가 보내는 INSERT 에는 이 컬럼이 항상 들어 있습니다. 그래도 남겨둔 이유는
    -- JPA 를 거치지 않는 삽입 - 이관 스크립트나 손으로 넣는 행 - 이 미검토로 떨어지게
    -- 하기 위해서입니다. 그런 행이 상태 없이 들어오면 운영자 목록에서 조용히 빠집니다.
    --
    -- 상태를 사람이 아니라 신고 한 건마다 두는 이유가 있습니다. 운영자는 사람을 보고
    -- 판단하므로 한 사람의 신고 다섯 건을 한 번에 표시하게 되는데, 그 뒤에 새로 들어온
    -- 신고는 PENDING 이라 저절로 다시 올라옵니다. 사람 단위로 두면 "언제부터 다시 봐야
    -- 하는가"를 따로 관리해야 합니다.
    status           VARCHAR(16)  NOT NULL DEFAULT 'PENDING',

    -- 검토한 시각. 아직 안 봤으면 NULL 입니다.
    --
    -- 누가 봤는지는 담지 않습니다. 지금 이 서버에는 인증이 없어서 어떤 값을 넣어도
    -- 스스로 신고한 이름일 뿐이고, 확인할 수 없는 "누가 결정했다"는 없느니만 못합니다.
    -- 인증이 생길 때 함께 넣어야 합니다.
    reviewed_at      CHAR(14)     NULL,

    PRIMARY KEY (user_reports_seq),

    -- 같은 사람을 여러 번 신고하는 것을 막지 않습니다. 횟수 자체가 운영자에게 신호가
    -- 되기 때문입니다. 대신 한 사람이 부풀릴 수 있으므로, 운영자 도구를 만들 때는
    -- 신고 건수와 신고한 사람 수를 반드시 나눠서 봐야 합니다.
    --
    -- 그래서 UNIQUE 키가 없고, 대신 "이 사람이 몇 건 신고당했나"를 위한 인덱스를 둡니다.
    KEY ix_user_reports_reported (reported_seq, created_at),

    -- 운영자 화면의 기본 질문은 "아직 안 본 신고를 오래된 순으로"입니다. status 가
    -- 선두라 그 조회가 동등 비교로 시작하고, created_at 이 뒤라 정렬이 인덱스로 끝납니다.
    KEY ix_user_reports_pending (status, created_at),

    -- 자기 신고를 막는 CHECK 가 여기 없습니다. 일부러 뺀 것이니 다시 넣지 마세요.
    --
    -- MySQL 은 CHECK 에 쓰인 컬럼을 ON DELETE SET NULL 의 대상으로 삼지 못합니다.
    -- 넣으면 이 마이그레이션이 "cannot be used in a check constraint ... needed in a
    -- foreign key constraint referential action" 으로 실패합니다. 둘 중 하나만 가질 수
    -- 있습니다.
    --
    -- 기록을 지키는 쪽을 골랐습니다. 자기 신고는 ReportService 가 막고 테스트가 그것을
    -- 고정합니다. 그 검사가 언젠가 뚫려도 결과는 뜻 없는 행 하나지만, 신고가 사라지는
    -- 것은 제재 근거가 사라지는 일입니다. room_invites 와 반대되는 선택인데, 거기는
    -- SET NULL 을 쓸 이유가 없어서 제약을 그대로 둘 수 있었습니다.

    -- 두 방향을 다르게 다룹니다.
    --
    -- 신고자가 탈퇴하면 기록은 남고 신고자만 NULL 이 됩니다. 신고는 신고당한 사람에
    -- 대한 기록이지 신고자에 대한 기록이 아닙니다. 목격자가 떠났다고 제3자에 대한
    -- 진술을 없앨 이유가 없습니다.
    --
    -- 대가가 있습니다. 떠난 신고자가 전부 NULL 이라 "세 명이 신고"인지 "한 명이 세 번"
    -- 인지 구분되지 않습니다. 운영자 도구는 남아 있는 신고자에 대해서만 그 구분을 할 수
    -- 있습니다. 기록 자체가 사라지는 것보다는 낫다고 봤습니다.
    --
    -- 신고당한 쪽은 CASCADE 입니다. 그 사람이 없으면 볼 대상이 없습니다. 다만 이건
    -- "탈퇴 후 재가입"으로 신고 기록을 세탁하는 경로이기도 합니다. 막으려면 탈퇴해도
    -- 남는 신원 고정점이 필요하고, 그것은 제재를 설계할 때 함께 정해야 합니다.
    CONSTRAINT fk_user_reports_reporter
        FOREIGN KEY (reporter_seq) REFERENCES users (users_seq) ON DELETE SET NULL,
    CONSTRAINT fk_user_reports_reported
        FOREIGN KEY (reported_seq) REFERENCES users (users_seq) ON DELETE CASCADE
) ENGINE = InnoDB;
