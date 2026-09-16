-- 신고 조사를 위한 채팅 기록 (S15P21D205-1027).
--
-- 신고가 들어와도 무슨 말이 오갔는지 확인할 방법이 없어서, 운영자가 사유와 한 줄 메모만 보고
-- 정지를 판단해야 했습니다. 이 표가 그 판단에 근거를 줍니다.
--
-- 분석 DB(d205_analytics)가 아니라 여기에 둡니다. 분석은 익명 집계용이라 장애 격리로 컨테이너와
-- DB 를 분리한 것이고, 채팅은 user_reports 와 users 에 붙어야 뜻이 생기는 운영 데이터입니다.
--
-- 오래 두지 않습니다. 목적이 "신고가 들어왔을 때 확인"이므로 기본 3일이고, 신고가 붙은 방과
-- 시간대만 처리가 끝날 때까지 남깁니다. 지우는 일은 S15P21D205-1031 이 합니다.

CREATE TABLE chat_logs
(
    chat_logs_seq INT UNSIGNED NOT NULL AUTO_INCREMENT,

    -- 어느 방에서 한 말인가. 신고의 context_key(예: 7K2M9P#2)에서 # 앞을 떼면 이 값입니다.
    -- context_key 를 통째로 두지 않는 이유는 그것이 전역 식별자가 아니기 때문입니다. 각 피어가
    -- 자기가 본 경기 수를 세어 만드는 값이라 늦게 들어온 사람은 같은 경기에 다른 번호를 붙입니다.
    -- 그래서 신고와는 방 코드와 시각 구간으로 잇습니다.
    room_code     VARCHAR(16)  NOT NULL,

    -- LOBBY 또는 MATCH. 신고는 로비에서도 할 수 있으므로 둘 다 담습니다.
    scope         VARCHAR(8)   NOT NULL,

    -- 말한 사람. 탈퇴하면 아래 FK 가 NULL 로 만듭니다. 탈퇴는 기록을 지워 달라는 뜻이므로
    -- suspension_audit 처럼 표시용 값을 따로 남기지 않습니다.
    --
    -- 계정은 기기 기반으로 자동 발급되고 계정 없이는 Photon 이 접속을 막으므로 보통은 값이 있습니다.
    -- 비는 경우는 하나뿐입니다 - 백엔드가 죽어 있을 때 저장된 크리덴셜로 들어오면(S15P21D205-925)
    -- 그 클라이언트의 Account 가 세션 내내 null 로 남습니다.
    sender_seq    INT UNSIGNED NULL,

    -- 그 방에서만 뜻이 있는 발화자 표식. 위의 경우에도 대화록이 "알 수 없음" 여러 줄로 뭉개지지
    -- 않게 합니다. 누구인지는 알 수 없고 몇 사람이 말했는지만 구분됩니다. 방이 끝나면 의미가 사라집니다.
    sender_ref    VARCHAR(64)  NOT NULL,

    -- 가리기 전의 말. 가린 것만 남기면 조사가 되지 않습니다. 원문을 담는 것이 이 표를 3일만
    -- 두는 이유이기도 합니다. 80자는 LobbyChatMessage.MaxTextLength 와 같아야 합니다.
    message       VARCHAR(80)  NOT NULL,

    -- 금칙어에 걸렸는가. 게임 서버가 보내는 값을 믿지 않고 저장할 때 백엔드가 다시 판정합니다.
    -- 게임 서버가 들고 있는 목록이 낡았어도 기록은 최신 기준으로 남습니다.
    masked        BOOLEAN      NOT NULL,

    -- 말한 시각. yyyyMMddHHmmss, UTC. 게임 서버가 찍습니다. 묶음으로 늦게 도착해도 순서가
    -- 유지되도록 도착 시각(created_at)과 나눠 둡니다.
    sent_at       CHAR(14)     NOT NULL,

    created_at    CHAR(14)     NOT NULL,

    PRIMARY KEY (chat_logs_seq),

    -- 신고 상세가 "이 방 이 시간대"를 여는 조회. 방 코드가 선두라 동등 비교로 시작하고
    -- 시각이 뒤라 범위와 정렬이 인덱스로 끝납니다.
    KEY ix_chat_logs_room_time (room_code, sent_at),

    -- "이 사람이 최근에 한 말 전부". 한 방에서 한 번은 실수일 수 있어도 여러 방에서 반복하면
    -- 다른 판단이 되므로 운영자가 반드시 보게 됩니다.
    KEY ix_chat_logs_sender (sender_seq, sent_at),

    CONSTRAINT fk_chat_logs_sender
        FOREIGN KEY (sender_seq) REFERENCES users (users_seq) ON DELETE SET NULL
);
