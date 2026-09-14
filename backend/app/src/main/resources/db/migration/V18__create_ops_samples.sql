-- 운영 지표 1분 샘플 (S15P21D205-1003).
--
-- 관리 화면 개요 탭의 "시간대별 접속자"와 "서버 자원 추이"는 기록이 있어야 그릴 수 있는데,
-- 지금은 아무 데도 남지 않습니다. presence 는 현재 상태만 들고 있고, JVM 메트릭은 프로세스가
-- 죽으면 사라집니다. 그래서 1분마다 한 행을 남깁니다.
--
-- 한 행에 접속자와 서버 자원을 함께 둡니다. 따로 두면 두 그래프의 시각 축을 맞추려고 조인이
-- 필요한데, 둘은 늘 같은 순간에 같은 코드가 재는 값입니다.
--
-- 가입·탈퇴는 누적이 아니라 <b>직전 샘플 이후 증분</b>입니다. "오늘 가입"은 당일 행을 더해
-- 구합니다. 누적으로 두면 자정에 0 으로 돌리는 규칙이 코드와 SQL 두 곳에 필요해집니다.
-- 탈퇴는 users 행이 지워져 셀 수 없어서 앱이 이벤트를 세어 넣습니다. 앱이 재시작하면 그 사이
-- 탈퇴는 잃습니다 - 운영 지표라 그 정도는 받아들입니다.
--
-- 시각이 PK 입니다. 1분에 한 행이라 겹칠 일이 없고, 최근 N시간 조회와 오래된 행 삭제가 둘 다
-- 이 컬럼의 범위 스캔입니다. 형식은 다른 시각 컬럼과 같은 CHAR(14) UTC 입니다.
--
-- 30일 지난 행은 OpsSweeper 가 지웁니다. 1분에 한 행이면 하루 1,440행, 30일에 43,200행이라
-- 상한이 작습니다.

CREATE TABLE ops_samples
(
    sampled_at         CHAR(14)     NOT NULL,

    -- user_presence 의 상태별 인원. 서로 배타적이라 셋을 더하면 접속자 수입니다.
    online             INT UNSIGNED NOT NULL,
    in_lobby           INT UNSIGNED NOT NULL,
    in_game            INT UNSIGNED NOT NULL,

    -- 알림 WebSocket 연결 수. 한 사람이 두 기기면 2 이므로 접속자 수와 다를 수 있습니다.
    socket_connections INT UNSIGNED NOT NULL,

    -- 직전 샘플 이후 증분.
    signups            INT UNSIGNED NOT NULL,
    deletions          INT UNSIGNED NOT NULL,

    -- JVM 이 보는 시스템 CPU 사용률(0~100). 컨테이너 안에서는 cgroup 한도 기준입니다.
    cpu_pct            DECIMAL(5, 1) NOT NULL,
    heap_used_mb       INT UNSIGNED NOT NULL,
    heap_max_mb        INT UNSIGNED NOT NULL,

    -- 게임 DB 와 분석 DB 커넥션 풀을 합친 값입니다.
    db_pool_active     INT UNSIGNED NOT NULL,
    db_pool_max        INT UNSIGNED NOT NULL,

    PRIMARY KEY (sampled_at)
) ENGINE = InnoDB;
