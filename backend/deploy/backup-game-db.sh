#!/bin/bash
# 계정 DB(users, friendships, user_reports, ...)를 통째로 덤프합니다 (S15P21D205-981).
#
# 플레이 로그 백업(reset-analytics.sh)은 있었지만 계정 DB 정기 백업은 없었습니다. 이 스크립트를 매일 새벽
# systemd timer 나 cron 으로 돌립니다. 설치 방법은 deploy/README.md 에 있습니다.
#
#     bash deploy/backup-game-db.sh            # 지금 한 번
#
# 파일은 /home/ubuntu/d205-backups/ 에 날짜 이름으로 남고 7일 지난 것은 지웁니다. 배포 디렉터리 바깥이라
# 재배포가 건드리지 않습니다. 덤프에는 기기 식별자(계정의 비밀번호에 해당) 가 평문으로 들어 있으므로
# 파일 권한은 600, 디렉터리는 700 입니다. 서버 밖으로 복사하려면 그때 암호화를 따로 정합니다.
#
# --single-transaction: InnoDB 라 잠금 없이 일관된 스냅샷을 뜹니다. 게임 API 는 백업 중에도 그대로 돕니다.
# 실패하면 0 이 아닌 코드로 끝나 timer 가 실패로 기록합니다. 조용히 빈 파일을 남기지 않습니다.
set -euo pipefail

cd "$(dirname "$0")/.."
if [ ! -f .env ]; then
    echo ".env 가 없습니다. 배포 디렉터리에서 실행하세요." >&2
    exit 1
fi
set -a; . ./.env; set +a
: "${MYSQL_ROOT_PASSWORD:?.env 에 MYSQL_ROOT_PASSWORD 가 없습니다}"
: "${DB_NAME:?.env 에 DB_NAME 이 없습니다}"

CONTAINER=d205-mysql
BACKUP_DIR=/home/ubuntu/d205-backups
KEEP_DAYS=7
STAMP=$(date -u +%Y%m%dT%H%M%SZ)
FILE="$BACKUP_DIR/${STAMP}-${DB_NAME}.sql.gz"

mkdir -p "$BACKUP_DIR"
chmod 700 "$BACKUP_DIR"

if ! docker inspect -f '{{.State.Running}}' "$CONTAINER" 2>/dev/null | grep -q true; then
    echo "$CONTAINER 가 떠 있지 않습니다." >&2
    exit 1
fi

tmp="$FILE.part"
docker exec "$CONTAINER" sh -c "MYSQL_PWD='$MYSQL_ROOT_PASSWORD' mysqldump -uroot --single-transaction --routines --triggers --databases $DB_NAME" \
    | gzip > "$tmp"
chmod 600 "$tmp"

# 빈 덤프를 성공으로 남기지 않습니다. mysqldump 가 중간에 죽으면 파이프 뒤의 gzip 은 성공하고 파일만 짧아집니다.
if [ "$(stat -c %s "$tmp")" -lt 1024 ] || ! gunzip -c "$tmp" | tail -c 200 | grep -q "Dump completed"; then
    echo "덤프가 완전하지 않습니다. 파일을 남기지 않습니다: $tmp" >&2
    rm -f "$tmp"
    exit 1
fi
mv "$tmp" "$FILE"

find "$BACKUP_DIR" -maxdepth 1 -name "*-${DB_NAME}.sql.gz" -mtime +"$KEEP_DAYS" -print -delete | sed 's/^/오래된 백업 삭제: /'
echo "백업 완료: $FILE ($(du -h "$FILE" | cut -f1))"
