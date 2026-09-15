#!/bin/bash
# 분석 DB 의 Flyway 이력에 남은 분리 전 체크섬을 코드 기준으로 맞춥니다 (S15P21D205-980 후속).
#
# ## 왜 필요한가
#
# 서비스 분리(980) 커밋에서 V1__create_game_event.sql 의 주석 한 줄을 고쳤습니다. Flyway 는 주석까지
# 포함해 체크섬을 내므로 이것만으로 값이 바뀝니다. 그런데 migrate-analytics.sh 가 옛 DB 의
# flyway_schema_history 를 새 DB 에 덮어써서, 새 DB 에 분리 전 체크섬이 들어앉았습니다.
#
# 그래서 수집 서비스가 기동할 때마다 아래로 죽습니다.
#
#     Migration checksum mismatch for migration version 1
#     -> Applied to database : -1736482106
#     -> Resolved locally    : -1060353966
#
# 이미 떠 있는 컨테이너는 재검증을 안 하므로 살아 있지만, 재시작하면 못 뜹니다. 젠킨스의 기동 검증
# 단계도 운영 분석 DB 에 임시 컨테이너를 붙이므로 매번 여기서 실패합니다.
#
# ## 무엇을 하는가
#
# V1 의 SQL 본문은 그대로이고 주석만 달라졌습니다. 스키마는 이미 올바른 모양이므로 다시 돌릴 것이
# 없고, 이력의 체크섬만 코드값으로 바꾸면 됩니다. flyway repair 가 하는 일과 같습니다.
#
# 안전을 위해 분리 전 값이 들어 있을 때만 바꿉니다. 다른 값이면 우리가 아는 사고가 아니므로 멈춥니다.
# 바꾸기 전에 이력 테이블을 통째로 백업합니다.
#
# 서버의 배포 디렉터리(/home/ubuntu/d205)에서 한 번 실행합니다.
#
#     bash deploy/repair-analytics-flyway.sh
#
# 여러 번 돌려도 됩니다. 이미 맞으면 바꾸지 않고 대조만 합니다.
set -euo pipefail

cd "$(dirname "$0")/.."
if [ ! -f .env ]; then
    echo ".env 가 없습니다. 배포 디렉터리에서 실행하세요." >&2
    exit 1
fi
set -a; . ./.env; set +a

: "${ANALYTICS_MYSQL_ROOT_PASSWORD:?.env 에 ANALYTICS_MYSQL_ROOT_PASSWORD 가 없습니다}"
DB="${ANALYTICS_DB_NAME:-d205_analytics}"
C=d205-mysql-analytics
BACKUP_DIR=/home/ubuntu/d205-backups
STAMP=$(date -u +%Y%m%dT%H%M%SZ)

# 분리 전 파일의 체크섬. DB 에 이 값이 있을 때만 손댑니다.
BEFORE_SPLIT=-1736482106

# 현재 코드(backend/analytics/src/main/resources/db/migration-analytics)의 체크섬입니다.
# 파일을 고치면 이 표도 같이 고쳐야 합니다.
EXPECTED_1=-1060353966
EXPECTED_2=1137579712
EXPECTED_3=1044805988
EXPECTED_4=835858926

if ! docker inspect -f '{{.State.Running}}' "$C" 2>/dev/null | grep -q true; then
    echo "$C 가 떠 있지 않습니다." >&2
    exit 1
fi

sql() {
    docker exec -i "$C" sh -c "MYSQL_PWD='$ANALYTICS_MYSQL_ROOT_PASSWORD' mysql -uroot -N -B '$DB'"
}

echo "[1/4] 지금 이력"
sql <<'SQL' | sed 's/^/  /'
SELECT version, checksum, script FROM flyway_schema_history ORDER BY installed_rank;
SQL

current=$(sql <<'SQL'
SELECT checksum FROM flyway_schema_history WHERE version = '1';
SQL
)
current=$(echo "$current" | tr -d '[:space:]')

if [ -z "$current" ]; then
    echo "version 1 행이 없습니다. 이 스크립트가 다룰 상황이 아닙니다." >&2
    exit 1
fi

echo
echo "[2/4] 백업: flyway_schema_history"
mkdir -p "$BACKUP_DIR"
chmod 700 "$BACKUP_DIR"
backup="$BACKUP_DIR/${STAMP}-flyway-history-before-repair.sql.gz"
docker exec "$C" sh -c "MYSQL_PWD='$ANALYTICS_MYSQL_ROOT_PASSWORD' mysqldump -uroot --single-transaction '$DB' flyway_schema_history" \
    | gzip > "$backup"
chmod 600 "$backup"
echo "  $backup ($(du -h "$backup" | cut -f1))"

echo
echo "[3/4] version 1 체크섬"
if [ "$current" = "$EXPECTED_1" ]; then
    echo "  이미 코드값($EXPECTED_1)입니다. 바꾸지 않습니다."
elif [ "$current" = "$BEFORE_SPLIT" ]; then
    sql <<SQL
UPDATE flyway_schema_history SET checksum = $EXPECTED_1 WHERE version = '1' AND checksum = $BEFORE_SPLIT;
SQL
    echo "  $BEFORE_SPLIT -> $EXPECTED_1 로 바꿨습니다."
else
    echo "  예상하지 못한 값입니다: $current" >&2
    echo "  분리 전($BEFORE_SPLIT) 도 코드값($EXPECTED_1) 도 아닙니다. 마이그레이션 파일이 또 바뀌었는지 확인하세요." >&2
    exit 1
fi

echo
echo "[4/4] 네 건 대조"
fail=0
for v in 1 2 3 4; do
    eval "want=\$EXPECTED_$v"
    got=$(sql <<SQL
SELECT checksum FROM flyway_schema_history WHERE version = '$v';
SQL
)
    got=$(echo "$got" | tr -d '[:space:]')
    if [ -z "$got" ]; then
        printf '  V%-3s 행 없음 (기대 %s)  다름!\n' "$v" "$want"
        fail=1
    elif [ "$got" = "$want" ]; then
        printf '  V%-3s %-14s OK\n' "$v" "$got"
    else
        printf '  V%-3s %-14s 기대 %-14s 다름!\n' "$v" "$got" "$want"
        fail=1
    fi
done

if [ "$fail" -ne 0 ]; then
    echo
    echo "아직 맞지 않습니다. 되돌리려면:" >&2
    echo "  gunzip -c $backup | docker exec -i $C sh -c 'MYSQL_PWD=\$ANALYTICS_MYSQL_ROOT_PASSWORD mysql -uroot $DB'" >&2
    exit 1
fi

cat <<EOS

이력이 코드와 맞습니다. 다음 할 일:
  젠킨스에서 d205-backend/develop 잡을 다시 실행 (기동 검증 단계를 지나야 배포가 진행됩니다)
  배포 뒤 확인: 로컬에서 scp backend/deploy/verify.sh d205:/tmp/verify.sh 뒤 ssh d205 'bash /tmp/verify.sh'
백업: $backup
EOS
