#!/usr/bin/env bash
# 플레이 로그 초기화. EC2에서 실행합니다.
#
#   scp backend/deploy/reset-analytics.sh d205:/tmp/
#   ssh -t d205 'bash /tmp/reset-analytics.sh'
#
# -t 가 필요합니다. 확인 프롬프트가 tty 를 읽고, 없으면 진행하지 않습니다.
# 무엇을 지울지만 보려면 --dry-run, 프롬프트를 건너뛰려면 --yes 입니다.
#
# SQL을 명령줄에 인라인하지 않는 이유는 verify.sh 와 같습니다. PowerShell에서 ssh로
# 넘기면 홑따옴표 안의 겹따옴표가 벗겨져 bash 가 SELECT COUNT(*) 를 두 단어로 읽습니다.
#
# 지우는 것은 d205_analytics.game_event 한 테이블뿐입니다(컨테이너 d205-mysql-analytics, S15P21D205-980 이후). 같은 스키마의
# flyway_schema_history 는 건드리지 않습니다 - 지우면 다음 배포가 V1~V4 를 처음부터
# 다시 실행하려다 실패합니다. 뷰(match_analysis_*)는 실체화가 아니라 원본을 그때그때
# 읽으므로 따로 비울 것이 없습니다.
set -uo pipefail

ENV_FILE=/home/ubuntu/d205/.env
BACKUP_DIR=/home/ubuntu/d205-backups
SCHEMA=d205_analytics
ERR=/tmp/reset-analytics-sql.err

DRY_RUN=0
ASSUME_YES=0
for arg in "$@"; do
    case "$arg" in
        --dry-run) DRY_RUN=1 ;;
        --yes|-y)  ASSUME_YES=1 ;;
        *) echo "모르는 인자: $arg (쓸 수 있는 것: --dry-run, --yes)" >&2; exit 2 ;;
    esac
done

if [ ! -r "$ENV_FILE" ]; then
    echo "$ENV_FILE 을 읽을 수 없습니다. 이 스크립트는 d205 서버에서 실행합니다." >&2
    exit 1
fi

# -f2- 입니다. verify.sh 는 -f2 를 쓰는데 비밀번호에 = 가 들어가면 거기서 잘립니다.
# 여기서는 잘린 비밀번호로 붙지 못하는 것이 백업 실패로 이어지므로 뒤를 다 가져옵니다.
PW=$(grep '^ANALYTICS_MYSQL_ROOT_PASSWORD=' "$ENV_FILE" | cut -d= -f2-)
if [ -z "$PW" ]; then
    echo "$ENV_FILE 에 ANALYTICS_MYSQL_ROOT_PASSWORD 가 없습니다." >&2
    exit 1
fi

# 조회 하나. 실패하면 서버가 준 말을 그대로 보여줍니다(verify.sh 와 같은 이유로,
# 실패 사유를 지어내지 않습니다). SQL 안에 한글을 쓰지 않습니다 - docker exec 로 넘긴
# UTF-8 식별자를 컨테이너 안 클라이언트가 다른 문자셋으로 읽어 구문 오류가 납니다.
ask() {
    docker exec d205-mysql-analytics mysql -uroot -p"$PW" --default-character-set=utf8mb4 \
        -t "$SCHEMA" -e "$1" 2>"$ERR" && return 0
    echo "  조회 실패:"
    grep -v 'Using a password' "$ERR" | sed 's/^/    /'
    return 1
}

# 값 하나만 받습니다. -N -B 라 헤더도 표 테두리도 없습니다.
value() {
    docker exec d205-mysql-analytics mysql -uroot -p"$PW" -N -B "$SCHEMA" -e "$1" 2>/dev/null
}

echo "=== 지금 쌓인 것 ($SCHEMA.game_event) ==="
ask 'SELECT schema_ver, COUNT(*) AS rows_in, COUNT(DISTINCT match_id) AS matches,
            MIN(occurred_at) AS first_at, MAX(occurred_at) AS last_at
       FROM game_event GROUP BY schema_ver ORDER BY schema_ver;' \
  || { echo "게임 로그 테이블을 읽지 못했습니다. 중단합니다." >&2; exit 1; }

echo "--- 용량 ---"
docker exec d205-mysql-analytics mysql -uroot -p"$PW" -t -e \
  "SELECT table_name, table_rows, ROUND((data_length+index_length)/1024/1024,1) AS mb
     FROM information_schema.tables WHERE table_schema='$SCHEMA';" 2>/dev/null

# 숫자인지 봅니다. 조회가 어긋나 엉뚱한 문자열이 오면 아래 -eq 비교가 조용히 실패하고
# "이미 비어 있습니다" 를 건너뛴 채 백업까지 갑니다. 세지 못했으면 세지 못했다고 합니다.
TOTAL=$(value 'SELECT COUNT(*) FROM game_event;')
case "$TOTAL" in
    ''|*[!0-9]*)
        echo "행 수를 세지 못했습니다(받은 값: '${TOTAL}'). 중단합니다." >&2
        exit 1
        ;;
esac

if [ "$TOTAL" -eq 0 ]; then
    echo
    echo "이미 비어 있습니다. 백업도 삭제도 하지 않습니다."
    exit 0
fi

if [ "$DRY_RUN" -eq 1 ]; then
    echo
    echo "--dry-run 입니다. ${TOTAL}행을 지울 수 있고, 아무것도 건드리지 않았습니다."
    exit 0
fi

# 백업이 먼저입니다. 실패하면 여기서 멈춥니다 - 백업 없이 지우는 경로를 두지 않습니다.
echo
echo "=== 백업 ==="
mkdir -p "$BACKUP_DIR" || { echo "$BACKUP_DIR 을 만들지 못했습니다." >&2; exit 1; }
BACKUP="$BACKUP_DIR/game_event_$(date +%Y%m%d_%H%M).sql.gz"

# --single-transaction 은 덤프 중 들어오는 이벤트가 쓰기를 기다리지 않게 합니다.
# InnoDB 라 일관된 스냅샷을 잠금 없이 뜹니다.
# pipefail 이 켜져 있어 mysqldump 가 죽으면 gzip 이 성공해도 실패로 잡힙니다.
docker exec d205-mysql-analytics mysqldump -uroot -p"$PW" --default-character-set=utf8mb4 \
    --single-transaction "$SCHEMA" game_event 2>"$ERR" | gzip > "$BACKUP"
if [ $? -ne 0 ]; then
    echo "덤프에 실패했습니다. 지우지 않고 멈춥니다:" >&2
    grep -v 'Using a password' "$ERR" | sed 's/^/  /' >&2
    rm -f "$BACKUP"
    exit 1
fi

# 파일이 생겼다고 온전한 것이 아닙니다. 디스크가 차면 잘린 gz 가 남습니다.
if ! gzip -t "$BACKUP" 2>/dev/null; then
    echo "백업 파일이 깨졌습니다($BACKUP). 지우지 않고 멈춥니다." >&2
    exit 1
fi
# 크기가 아니라 내용을 봅니다. 처음에는 "1KB 미만이면 실패"로 두었는데, SQL 은 gzip
# 압축률이 높아 경기 몇 판짜리 정상 덤프도 1KB 아래로 떨어집니다. 그 하한은 멀쩡한
# 백업을 깨졌다고 말했습니다. 여기까지 온 이상 행이 0 이 아니므로(위에서 걸러냅니다)
# 온전한 덤프에는 테이블 정의와 INSERT 가 반드시 있습니다. 그 둘을 확인합니다.
#
# grep -q 를 쓰지 않습니다. -q 는 첫 일치에서 즉시 끝나고, 그러면 아직 풀고 있던 gunzip 이
# SIGPIPE 로 죽어 pipefail 이 파이프라인 전체를 실패로 잡습니다. 일치를 찾았다는 이유로
# 실패가 되는 것이라, 백업이 정상일수록 확실하게 틀립니다. 2026-09-10 운영에서 3.2MB
# 덤프로 이 함정에 걸렸습니다(작은 덤프는 gunzip 이 먼저 끝나 SIGPIPE 가 안 나서 통과합니다).
# grep -c 는 끝까지 읽으므로 SIGPIPE 가 없습니다. 종료 코드가 아니라 센 값을 봅니다.
DDL=$(gunzip -c "$BACKUP" | grep -c 'CREATE TABLE .*game_event')
if [ "${DDL:-0}" -eq 0 ]; then
    echo "백업에 game_event 테이블 정의가 없습니다($BACKUP). 지우지 않고 멈춥니다." >&2
    exit 1
fi
INSERTS=$(gunzip -c "$BACKUP" | grep -c 'INSERT INTO')
if [ "${INSERTS:-0}" -eq 0 ]; then
    echo "백업에 ${TOTAL}행이 담기지 않았습니다($BACKUP). 지우지 않고 멈춥니다." >&2
    exit 1
fi

ls -lh "$BACKUP"
echo "복원: gunzip -c $BACKUP | docker exec -i d205-mysql-analytics mysql -uroot -p'<암호>' $SCHEMA"

if [ "$ASSUME_YES" -eq 0 ]; then
    if [ ! -t 0 ]; then
        echo
        echo "확인 프롬프트를 띄울 tty 가 없습니다. ssh -t 로 실행하거나 --yes 를 주세요." >&2
        echo "백업은 남겨 둡니다: $BACKUP" >&2
        exit 1
    fi
    echo
    echo "game_event ${TOTAL}행을 지웁니다. 되돌리려면 위 백업뿐입니다."
    printf "지우려면 yes 를 입력하세요: "
    read -r reply
    if [ "$reply" != "yes" ]; then
        echo "취소했습니다. 아무것도 지우지 않았습니다."
        exit 0
    fi
fi

echo
echo "=== 삭제 ==="
# TRUNCATE 입니다. DELETE 는 InnoDB 가 디스크를 돌려주지 않고 AUTO_INCREMENT 도 그대로입니다.
docker exec d205-mysql-analytics mysql -uroot -p"$PW" "$SCHEMA" -e 'TRUNCATE TABLE game_event;' 2>"$ERR"
if [ $? -ne 0 ]; then
    echo "삭제에 실패했습니다:" >&2
    grep -v 'Using a password' "$ERR" | sed 's/^/  /' >&2
    exit 1
fi

echo "=== 확인 (둘 다 0 이어야 합니다) ==="
ask 'SELECT (SELECT COUNT(*) FROM game_event)             AS game_event_rows,
            (SELECT COUNT(*) FROM match_analysis_summary) AS summary_view_rows;'

echo
echo "끝났습니다. 앱은 다시 띄우지 않아도 되고, Metabase 는 새로 고침만 하면 됩니다."
echo "대시보드와 질문은 metabase 스키마에 따로 있어 그대로 남아 있습니다."
echo
echo "백업을 로컬로 내려받으려면:"
echo "  scp d205:$BACKUP ."
