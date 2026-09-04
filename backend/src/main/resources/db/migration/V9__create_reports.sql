-- 사용자 신고.
--
-- 차단(V8 에서 없앰)이 있던 자리지만 성질이 반대입니다. 차단은 즉시 효력이 있어서
-- 검색과 친구 요청과 초대가 모두 그 테이블을 읽어야 했습니다. 신고는 상대에게 아무
-- 일도 일으키지 않습니다. 아무 경로도 이 테이블을 읽지 않고, 사람이 나중에 봅니다.
-- 그래서 조회 인덱스도 운영자가 쓸 것 하나만 둡니다.
CREATE TABLE user_reports
(
    user_reports_seq INT UNSIGNED NOT NULL AUTO_INCREMENT,

    reporter_seq     INT UNSIGNED NOT NULL,
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

    PRIMARY KEY (user_reports_seq),

    -- 같은 사람을 여러 번 신고하는 것을 막지 않습니다. 횟수 자체가 운영자에게 신호가
    -- 되기 때문입니다. 대신 한 사람이 부풀릴 수 있으므로, 운영자 도구를 만들 때는
    -- 신고 건수와 신고한 사람 수를 반드시 나눠서 봐야 합니다.
    --
    -- 그래서 UNIQUE 키가 없고, 대신 "이 사람이 몇 건 신고당했나"를 위한 인덱스를 둡니다.
    KEY ix_user_reports_reported (reported_seq, created_at),

    CONSTRAINT ck_user_reports_not_self CHECK (reporter_seq <> reported_seq),

    -- 양쪽 다 CASCADE 입니다. 신고자가 탈퇴하면 그 사람이 남긴 신고도 사라집니다.
    --
    -- 신고를 남겨두는 편이 운영에는 유리합니다. 하지만 이 프로젝트는 탈퇴가 흔적을
    -- 남기지 않는다고 약속했고(V7 주석), 신고 메모는 그 사람이 쓴 글입니다. 약속을
    -- 지키는 쪽을 택합니다. 신고당한 쪽이 탈퇴하면 볼 대상이 없으므로 함께 지웁니다.
    CONSTRAINT fk_user_reports_reporter
        FOREIGN KEY (reporter_seq) REFERENCES users (users_seq) ON DELETE CASCADE,
    CONSTRAINT fk_user_reports_reported
        FOREIGN KEY (reported_seq) REFERENCES users (users_seq) ON DELETE CASCADE
) ENGINE = InnoDB;
