#!/bin/bash
# 플레이 로그와 Metabase 설정을 게임 DB 컨테이너에서 분석 DB 컨테이너로 옮깁니다 (S15P21D205-980).
#
# 서비스를 나누기 전에는 d205-mysql 한 컨테이너에 스키마 셋(d205, d205_analytics, metabase)이 있었습니다.
# 나눈 뒤 d205_analytics 와 metabase 는 d205-mysql-analytics 의 것입니다. 이 스크립트는 그 둘을 덤프해
# 새 컨테이너에 넣습니다. 게임 스키마(d205)는 건드리지 않습니다.
#
# 서버에서, 배포 디렉터리(/home/ubuntu/d205)에서, 새 compose 가 올라온 뒤에 한 번 실행합니다.
#
#     bash deploy/migrate-analytics.sh
#
# 순서가 중요합니다.
#   1. 새 compose 로 배포되어 d205-mysql-analytics 가 떠 있어야 합니다(빈 스키마에 Flyway 가 돌아 있음).
#   2. 이 스크립트를 돌립니다. 백업 → 덤프 → 새 컨테이너에 넣기 → 행 수 비교.
#   3. Metabase 를 재시작합니다. 옮긴 설정으로 붙게 하려는 것입니다.
#   4. 옛 스키마는 지우지 않습니다. 며칠 지나 문제가 없으면 deploy/README.md 의 절차로 지웁니다.
#
# 백업이 먼저이고 실패하면 거기서 멈춥니다. 백업 없이 옮기는 경로는 없습니다.
#
# Flyway 이력(flyway_schema_history)은 옮기지 않습니다. 새 컨테이너에는 수집 서비스가 이미 V1~V4 를
# 돌려 놓았고, 그것이 지금 코드와 맞는 유일한 이력입니다.
#
# 처음에는 이 이력도 덮었고 그래서 사고가 났습니다(2026-09-15). 분리 커밋에서 V1 의 주석 한 줄이 바뀌었는데
# Flyway 는 주석까지 체크섬에 넣습니다. 옛 이력으로 덮으니 분리 전 체크섬이 남았고, 수집 서비스는 기동할
# 때마다 검증에 실패했습니다. 스키마가 같아도 체크섬은 파일을 따라가므로 "같은 파일들의 이력이니 내용도
# 같다"는 전제가 틀렸습니다. 이미 덮어 버렸다면 deploy/repair-analytics-flyway.sh 로 되돌립니다.
set -euo pipefail

cd "$(dirname "$0")/.."
if [ ! -f .env ]; then
    echo ".env 가 없습니다. 배포 디렉터리에서 실행하세요." >&2
    exit 1
fi
set -a; . ./.env; set +a

: "${MYSQL_ROOT_PASSWORD:?.env 에 MYSQL_ROOT_PASSWORD 가 없습니다}"
: "${ANALYTICS_MYSQL_ROOT_PASSWORD:?.env 에 ANALYTICS_MYSQL_ROOT_PASSWORD 가 없습니다}"

OLD=d205-mysql
NEW=d205-mysql-analytics
BACKUP_DIR=/home/ubuntu/d205-backups
STAMP=$(date -u +%Y%m%dT%H%M%SZ)

for c in "$OLD" "$NEW"; do
    if ! docker inspect -f '{{.State.Running}}' "$c" 2>/dev/null | grep -q true; then
        echo "$c 가 떠 있지 않습니다." >&2
        exit 1
    fi
done

mkdir -p "$BACKUP_DIR"
chmod 700 "$BACKUP_DIR"

echo "[1/4] 백업: 옛 컨테이너의 d205_analytics 와 metabase 스키마"
for schema in d205_analytics metabase; do
    file="$BACKUP_DIR/${STAMP}-${schema}-before-split.sql.gz"
    # --single-transaction: InnoDB 라 잠금 없이 일관된 스냅샷을 뜹니다. 게임 API 는 계속 돕니다.
    # --databases: 덤프에 CREATE DATABASE/USE 가 들어가 새 컨테이너에서 그대로 실행됩니다.
    # 이력 테이블은 뺍니다. 위 주석에 적은 이유입니다.
    ignore=""
    [ "$schema" = d205_analytics ] && ignore="--ignore-table=$schema.flyway_schema_history"
    docker exec "$OLD" sh -c "MYSQL_PWD='$MYSQL_ROOT_PASSWORD' mysqldump -uroot --single-transaction --routines --triggers $ignore --databases $schema" \
        | gzip > "$file"
    chmod 600 "$file"
    echo "  $file ($(du -h "$file" | cut -f1))"
done

echo "[2/4] 새 컨테이너에 넣기"
for schema in d205_analytics metabase; do
    file="$BACKUP_DIR/${STAMP}-${schema}-before-split.sql.gz"
    gunzip -c "$file" | docker exec -i "$NEW" sh -c "MYSQL_PWD='$ANALYTICS_MYSQL_ROOT_PASSWORD' mysql -uroot"
    echo "  $schema 완료"
done

echo "[3/4] 행 수 비교"
for table in "d205_analytics.game_event" "metabase.report_dashboard" "metabase.report_card"; do
    old_n=$(docker exec "$OLD" sh -c "MYSQL_PWD='$MYSQL_ROOT_PASSWORD' mysql -uroot -N -e 'SELECT COUNT(*) FROM $table'" 2>/dev/null || echo "?")
    new_n=$(docker exec "$NEW" sh -c "MYSQL_PWD='$ANALYTICS_MYSQL_ROOT_PASSWORD' mysql -uroot -N -e 'SELECT COUNT(*) FROM $table'" 2>/dev/null || echo "?")
    mark="OK"; [ "$old_n" != "$new_n" ] && mark="다름!"
    printf '  %-40s 옛 %-10s 새 %-10s %s\n' "$table" "$old_n" "$new_n" "$mark"
done

echo "[4/4] 읽기 계정 권한 확인 (새 컨테이너)"
docker exec "$NEW" sh -c "MYSQL_PWD='$ANALYTICS_MYSQL_ROOT_PASSWORD' mysql -uroot -N -e \"SHOW GRANTS FOR 'd205_reader'@'%'\"" || {
    echo "  d205_reader 가 없습니다. 볼륨이 초기화 스크립트를 건너뛴 것입니다. deploy/README.md 의 수동 절차를 따르세요." >&2
}

cat <<EOF

다음 할 일:
  docker compose -f compose.prod.yml restart metabase
  브라우저에서 Metabase 대시보드가 옮기기 전과 같은 숫자를 보이는지 확인
  며칠 뒤 문제가 없으면 옛 스키마 정리(README): DROP DATABASE d205_analytics; DROP DATABASE metabase; (d205-mysql 에서)
백업: $BACKUP_DIR/${STAMP}-*
EOF
