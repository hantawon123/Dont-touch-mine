-- 방 초대.
-- 로비의 친구 목록에서 방 코드를 친구에게 건네는 용도입니다. 서버는 실시간 통신을
-- 하지 않으므로 전달은 받는 쪽이 주기적으로 조회하는 방식입니다.
--
-- 방 코드 자체는 비밀이 아닙니다. 코드는 어느 방인지만 가리키고 잠긴 방은 여전히
-- 비밀번호를 요구합니다. 그래도 친구에게만 보낼 수 있게 막는 이유는, 모르는 사람에게
-- 코드가 흘러가는 길을 아예 두지 않기 위해서입니다.
CREATE TABLE room_invites
(
    room_invites_seq INT UNSIGNED NOT NULL AUTO_INCREMENT,

    inviter_seq      INT UNSIGNED NOT NULL,
    invitee_seq      INT UNSIGNED NOT NULL,

    -- Photon 룸 코드. 클라이언트의 RoomCodeFormat 이 6자로 고정하고,
    -- 알파벳은 0-9A-Z 에서 헷갈리는 I O U L 을 뺀 32자입니다.
    -- 길이가 고정이라 VARCHAR 대신 CHAR 입니다.
    room_code        CHAR(6)      NOT NULL,

    -- 보낸 시각. yyyyMMddHHmmss UTC. 만료 판정의 기준입니다.
    -- 같은 방에 다시 초대하면 새 행을 만들지 않고 이 값만 새로 씁니다.
    created_at       CHAR(14)     NOT NULL,

    PRIMARY KEY (room_invites_seq),

    -- 같은 사람을 같은 방에 두 번 부르는 것은 새 초대가 아니라 같은 초대입니다.
    -- 애플리케이션에서 먼저 조회해 확인하는 방식은 동시 요청에 뚫리므로 제약으로 막습니다.
    UNIQUE KEY uk_room_invites_target (invitee_seq, inviter_seq, room_code),

    -- 받은 초대 조회와 만료 스윕이 함께 씁니다.
    -- invitee_seq 가 선두라 "내가 받은 초대"는 동등 비교로 시작하고,
    -- created_at 이 뒤라 그 안에서 만료 범위를 좁힙니다.
    -- 스윕은 invitee 를 가리지 않으므로 이 인덱스를 타지 못하고 created_at 만으로
    -- 훑습니다. 초대는 수명이 3분이라 테이블이 작게 유지되므로 그걸로 충분합니다.
    KEY ix_room_invites_inbox (invitee_seq, created_at),

    -- 자기 자신을 부르는 것은 의미가 없습니다. friendships 가 정렬 규칙으로 같은 것을
    -- 막는 것과 같은 자리입니다 — 애플리케이션 검증은 빠뜨릴 수 있고 제약은 아닙니다.
    CONSTRAINT ck_room_invites_not_self CHECK (inviter_seq <> invitee_seq),

    -- 계정이 사라지면 그 사람이 보낸 초대와 받은 초대가 함께 사라집니다.
    -- 탈퇴가 "되돌릴 수 없고 흔적을 남기지 않는다"는 약속을 지키는 부분입니다.
    CONSTRAINT fk_room_invites_inviter
        FOREIGN KEY (inviter_seq) REFERENCES users (users_seq) ON DELETE CASCADE,
    CONSTRAINT fk_room_invites_invitee
        FOREIGN KEY (invitee_seq) REFERENCES users (users_seq) ON DELETE CASCADE
) ENGINE = InnoDB;
