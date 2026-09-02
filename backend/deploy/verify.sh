#!/usr/bin/env bash
# 배포 상태 확인. EC2에서 실행합니다.
#
#   ssh d205 'bash /tmp/verify.sh'
#
# SQL을 명령줄에 인라인하지 않는 이유: PowerShell에서 ssh로 넘길 때
# 홑따옴표 안의 겹따옴표가 벗겨져 bash가 SHOW TABLES를 두 단어로 해석합니다.
# 스크립트로 두면 인용부호 문제가 사라집니다.
set -uo pipefail

ENV_FILE=/home/ubuntu/d205/.env
COMPOSE_DIR=$(docker inspect d205-app \
  --format '{{index .Config.Labels "com.docker.compose.project.working_dir"}}' 2>/dev/null)

echo "=== 컨테이너 ==="
docker ps --filter label=com.docker.compose.project=d205 \
  --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}'

echo
echo "=== 배포 주체 ==="
echo "compose 실행 위치: ${COMPOSE_DIR:-(알 수 없음)}"
docker images d205-app --format '이미지: {{.Repository}}:{{.Tag}}  {{.CreatedSince}}  {{.Size}}'

echo
echo "=== 애플리케이션 ==="
printf '내부 8080: '
curl -sS --max-time 5 http://localhost:8080/actuator/health || echo '응답 없음'
echo
printf 'HTTPS    : '
curl -sS --max-time 5 https://j15d205.p.ssafy.io/actuator/health || echo '응답 없음'
echo

if [ ! -r "$ENV_FILE" ]; then
    echo
    echo "(DB 확인 생략: $ENV_FILE 을 읽을 수 없습니다)"
    exit 0
fi

PW=$(grep '^MYSQL_ROOT_PASSWORD=' "$ENV_FILE" | cut -d= -f2)
DB=$(grep '^DB_NAME=' "$ENV_FILE" | cut -d= -f2)

echo
echo "=== 테이블 ($DB) ==="
docker exec d205-mysql mysql -uroot -p"$PW" -N -B -e 'SHOW TABLES;' "$DB" 2>/dev/null \
  || echo '조회 실패'

echo
echo "=== Flyway 적용 이력 ==="
docker exec d205-mysql mysql -uroot -p"$PW" -t "$DB" 2>/dev/null -e \
  'SELECT installed_rank AS n, version, description, success, installed_on
     FROM flyway_schema_history ORDER BY installed_rank;' \
  || echo '조회 실패 (마이그레이션이 아직 적용되지 않았을 수 있습니다)'

echo
echo "=== 인증서 만료 ==="
sudo certbot certificates 2>/dev/null | grep -E 'Certificate Name|Expiry' || echo '확인 실패'
