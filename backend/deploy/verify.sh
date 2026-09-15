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
printf '내부 8080 (계정): '
curl -sS --max-time 5 http://localhost:8080/actuator/health || echo '응답 없음'
echo
printf '내부 8081 (수집): '
curl -sS --max-time 5 http://localhost:8081/actuator/health || echo '응답 없음'
echo
docker images d205-analytics --format '수집 이미지: {{.Repository}}:{{.Tag}}  {{.CreatedSince}}  {{.Size}}' | head -1
echo
printf 'HTTPS    : '
curl -sS --max-time 5 https://j15d205.p.ssafy.io/actuator/health || echo '응답 없음'
echo

echo
echo "=== 대시보드 (Metabase) ==="
printf '내부 3000: '
curl -sS --max-time 5 http://localhost:3000/api/health || echo '응답 없음 (첫 기동은 1분 넘게 걸립니다)'
echo
# 8443 은 Basic Auth 뒤라 401 이 정상입니다. 000 이면 nginx 나 방화벽, 502 면 Metabase 가 없는 것입니다.
printf 'HTTPS 8443 (401 이 정상): '
curl -sS --max-time 5 -o /dev/null -w '%{http_code}\n' https://j15d205.p.ssafy.io:8443/api/health || echo '응답 없음'
printf '분석 DB 준비 로그: '
docker logs d205-app --tail 500 2>&1 | grep -E '분석 DB' | tail -1 || echo '없음'

echo
echo "=== 상시 연결 (알림 WebSocket) ==="
# 클라이언트마다 연결 하나를 상시 붙들고 있습니다(883·890). 받을 수 있는 수는 nginx 의
# worker_connections 와 앱의 server.tomcat.max-connections(application.yml, 8192) 중
# 작은 쪽입니다. nginx 전역 설정은 저장소에 없어 여기서 읽습니다. 기본값 768 이면
# 그쪽이 먼저 막히고, 프록시라 접속자 하나가 연결 둘(클라이언트↔nginx, nginx↔앱)을 씁니다.
printf 'nginx worker_connections: '
sudo nginx -T 2>/dev/null | grep -m1 -oE 'worker_connections\s+[0-9]+' | awk '{print $2}' || echo '읽기 실패 (sudo 필요)'
printf '지금 443 에 맺힌 연결: '
ss -Htan state established '( sport = :443 )' 2>/dev/null | wc -l
printf '앱 8080 에 맺힌 연결: '
ss -Htan state established '( sport = :8080 )' 2>/dev/null | wc -l

if [ ! -r "$ENV_FILE" ]; then
    echo
    echo "(DB 확인 생략: $ENV_FILE 을 읽을 수 없습니다)"
    exit 0
fi

PW=$(grep '^MYSQL_ROOT_PASSWORD=' "$ENV_FILE" | cut -d= -f2)
DB=$(grep '^DB_NAME=' "$ENV_FILE" | cut -d= -f2)
# 분석 DB 는 다른 컨테이너, 다른 root 비밀번호입니다(S15P21D205-980).
APW=$(grep '^ANALYTICS_MYSQL_ROOT_PASSWORD=' "$ENV_FILE" | cut -d= -f2)

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

# 조회 하나를 돌리고, 실패하면 서버가 준 말을 그대로 보여줍니다.
#
# 처음에는 stderr 를 버리고 "테이블이 없습니다" 같은 문구를 대신 찍었습니다. 그것이
# 거짓말을 했습니다 - game_event 가 멀쩡히 있는데도 "아직 없습니다"가 나왔고, 진짜
# 이유(아래 주석)는 버려져 있었습니다. 실패 사유를 지어내지 말고 받은 것을 보여줍니다.
#
# SQL 안에는 한글을 쓰지 않습니다. docker exec 로 넘긴 UTF-8 식별자를 컨테이너 안
# 클라이언트가 다른 문자셋으로 읽어 구문 오류가 납니다. 설명은 바깥 echo 로 답니다.
# stderr 만 파일로 받고 stdout 은 그대로 흘립니다. /dev/tty 로 되돌리면 안 됩니다 -
# ssh 가 tty 없이 실행하므로 그 자리에서 죽습니다.
#
# --default-character-set=utf8mb4 가 없으면 한글 값이 물음표로 나옵니다. 컨테이너 안
# 클라이언트의 기본 문자셋이 한글을 못 담아서 커넥션 단계에서 버리는 것이고, 터미널
# 문제가 아닙니다(깨진 바이트가 아니라 깔끔한 ? 가 나오는 것이 그 증거). 대시보드
# 이름이 한글이라 이것이 없으면 어느 대시보드인지 알 수 없습니다.
ask() {
    local schema=$1 sql=$2
    docker exec d205-mysql mysql -uroot -p"$PW" --default-character-set=utf8mb4 \
        -t "$schema" -e "$sql" 2>/tmp/verify-sql.err && return 0
    echo "  조회 실패:"
    grep -v 'Using a password' /tmp/verify-sql.err | sed 's/^/    /'
}

# 같은 것을 분석 DB 컨테이너에 대고 합니다. d205_analytics 와 metabase 스키마는 그쪽에 있습니다.
ask_analytics() {
    local schema=$1 sql=$2
    docker exec d205-mysql-analytics mysql -uroot -p"$APW" --default-character-set=utf8mb4 \
        -t "$schema" -e "$sql" 2>/tmp/verify-sql.err && return 0
    echo "  조회 실패:"
    grep -v 'Using a password' /tmp/verify-sql.err | sed 's/^/    /'
}

echo
echo "=== 분석 스키마 (d205_analytics) ==="
# 별도 컨테이너(d205-mysql-analytics)입니다. 여기가 비어 있어도 게임은 멀쩡히
# 돌아가므로, 보러 오지 않으면 수집이 멎은 것을 아무도 모릅니다.
echo "[Flyway]"
ask_analytics d205_analytics \
  'SELECT version, description, success FROM flyway_schema_history ORDER BY installed_rank;'

# 뷰 셋은 V2·V3·V4 가 만듭니다. 대시보드 여덟 화면이 전부 이 뷰를 읽으므로, 없으면
# 화면은 떠도 값이 나오지 않습니다.
echo "[뷰]"
ask_analytics d205_analytics \
  "SELECT table_name FROM information_schema.views WHERE table_schema = 'd205_analytics';"

echo
echo "--- 수집된 이벤트 (rows=행, matches=경기) ---"
# schema_ver 로 나눠 셉니다. v2 가 없으면 새 Unity 빌드가 아직 배포되지 않은 것이고,
# 그때는 대시보드가 비어 있는 것이 정상입니다. 뷰가 schema_ver = 2 만 읽습니다.
ask_analytics d205_analytics \
  'SELECT schema_ver, COUNT(*) AS rows_in, COUNT(DISTINCT match_id) AS matches,
          MIN(received_at) AS first_at, MAX(received_at) AS last_at
     FROM game_event GROUP BY schema_ver ORDER BY schema_ver;'

# 분석에 실제로 쓸 수 있는 경기 수. 시작·종료가 다 있고 예정 건수와 맞고 중단이
# 아닌 것만 셉니다. 받은 경기가 있는데 complete 가 0 이면 전송이 중간에 끊기고 있습니다.
echo "--- 완전 수신된 경기 ---"
ask_analytics d205_analytics \
  'SELECT SUM(upload_complete) AS complete, COUNT(*) AS total
     FROM match_analysis_summary;'

echo
echo "=== 대시보드 질문 (Metabase) ==="
# Metabase 는 자기 설정을 분석 DB 컨테이너의 metabase 스키마에 둡니다. API 로 물으면 관리자
# 로그인이 필요하지만 여기서는 이미 root 로 붙어 있으므로 그냥 읽습니다.
#
# provision_dashboards.py 가 만드는 것은 대시보드 하나와 질문 여덟 개입니다. 카드 수가
# 여덟보다 적으면 스크립트가 도중에 멈춘 것입니다.
#
# 테이블 이름은 Metabase 버전에 딸린 것이라 우리가 정하지 않습니다. 못 찾으면 실패가
# 그대로 찍히므로, 그때는 SHOW TABLES 로 이름부터 확인하면 됩니다.
ask_analytics metabase \
  'SELECT d.name AS dashboard, COUNT(c.id) AS cards, d.updated_at AS updated
     FROM report_dashboard d
     LEFT JOIN report_dashboardcard c ON c.dashboard_id = d.id
    WHERE d.archived = 0 GROUP BY d.id, d.name, d.updated_at;'

echo
echo "=== Photon 커스텀 인증 (S15P21D205-925) ==="
# .env 에만 넣고 compose.prod.yml 의 environment 에 빠뜨리면 앱이 값을 못 봅니다.
# 그 상태는 조용합니다 - 인증이 꺼진 채로 뜨고 모든 접속을 통과시킵니다. 그래서
# 파일이 아니라 컨테이너 안에서 확인합니다.
printf '컨테이너 환경변수: '
if docker exec d205-app printenv PHOTON_AUTH_SECRET >/dev/null 2>&1; then
    echo '있음'
else
    echo '없음 (인증이 꺼져 있습니다. .env 와 compose.prod.yml 둘 다 확인하세요)'
fi
# 켜져 있으면 키가 틀린 요청에 ResultCode 3 이, 꺼져 있으면 1 이 나옵니다.
printf '인증 응답(3 이면 켜짐, 1 이면 꺼짐): '
curl -sS --max-time 5 'http://localhost:8080/api/v1/photon/auth?userId=verify&token=x&key=y'   || echo '응답 없음'
echo

echo
echo "=== 인증서 만료 ==="
sudo certbot certificates 2>/dev/null | grep -E 'Certificate Name|Expiry' || echo '확인 실패'
