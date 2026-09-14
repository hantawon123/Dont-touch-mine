-- 플레이어가 설정 화면에서 보내는 피드백.
--
-- 신고(V9)와 성질이 비슷하면서 다릅니다. 둘 다 아무 동작을 일으키지 않고 사람이 나중에
-- 읽습니다. 다른 점은 대상입니다. 신고는 특정 사용자에 대한 진술이라 그 사람을 가리키는
-- 열이 있어야 하지만, 피드백은 게임에 대한 진술이라 가리킬 대상이 없습니다. 그래서
-- 검토 상태도 두지 않았습니다. 판단할 것이 없고 읽을 것만 있습니다.
CREATE TABLE user_feedback
(
    user_feedback_seq INT UNSIGNED NOT NULL AUTO_INCREMENT,

    -- 쓴 사람. 탈퇴하면 NULL 이 됩니다(아래 FK).
    --
    -- 누구인지 받아 두는 이유는 두 가지입니다. 같은 사람이 같은 말을 열 번 보낸 것과
    -- 열 명이 같은 말을 보낸 것은 전혀 다른 신호이고, 내용이 심각하면 그 사람의 다른
    -- 기록(신고·경기)을 같이 봐야 합니다.
    author_seq        INT UNSIGNED NULL,

    -- 본문. 500자는 클라이언트 화면의 상한(SettingsStyle.Feedback.MaxLength)과 같은
    -- 값입니다. 서버가 더 작으면 다 쓴 글이 400 으로 버려지고, 더 크면 화면이 막는
    -- 값이 서버 계약과 달라져 나중에 화면 상한을 올릴 때 서버를 안 고쳐도 되는 것처럼
    -- 보입니다. 둘을 같게 두고 문서에 그 사실을 적습니다.
    --
    -- utf8mb4 라 이모지가 들어와도 500자는 500자입니다. 바이트가 아니라 글자 수입니다.
    message           VARCHAR(500) NOT NULL,

    -- 어느 빌드에서 나온 말인가. 클라이언트가 보내지 않으면 NULL 입니다.
    --
    -- 이것이 없으면 "튕긴다"는 피드백을 받아도 재현할 빌드를 모릅니다. 그래서 받지만,
    -- 필수로 두지는 않았습니다. 이 값이 빠졌다고 피드백을 버리는 것은 손해입니다.
    build_ver         VARCHAR(32)  NULL,

    -- WebGL / Windows 같은 실행 환경. 위와 같은 이유로 선택입니다.
    platform          VARCHAR(16)  NULL,

    created_at        CHAR(14)     NOT NULL,

    PRIMARY KEY (user_feedback_seq),

    -- 운영자의 기본 질문은 "최근에 뭐가 들어왔나"입니다. 그 정렬이 인덱스로 끝나게
    -- 둡니다. 사람 단위로 묶어 보는 화면은 없으므로 author_seq 인덱스는 두지 않습니다.
    -- 필요해지면 그때 넣습니다.
    KEY ix_user_feedback_recent (created_at),

    -- 신고와 같은 선택입니다. 쓴 사람이 떠나도 내용은 남깁니다. 피드백은 게임에 대한
    -- 진술이지 그 사람에 대한 기록이 아니고, 떠난 사람이 남긴 "이 부분이 지루하다"는
    -- 말은 그 사람이 떠난 뒤에도 사실입니다.
    --
    -- 대가는 신고와 같습니다. 탈퇴한 사람들의 피드백은 전부 NULL 이라 몇 명이 같은
    -- 말을 했는지 셀 수 없습니다.
    CONSTRAINT fk_user_feedback_author
        FOREIGN KEY (author_seq) REFERENCES users (users_seq) ON DELETE SET NULL
) ENGINE = InnoDB;
