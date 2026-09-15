# 배포 구성

운영 서버 `j15d205.p.ssafy.io`(EC2, Ubuntu 24.04)의 구성 파일입니다.
서버에서 직접 편집하지 말고 이 파일들을 고쳐 올리세요. 서버에서만 고친 설정은
EC2가 날아가면 같이 사라집니다.

## 파일

| 파일 | 배치 위치 | 역할 |
| --- | --- | --- |
| `nginx/d205.conf` | `/etc/nginx/sites-available/d205` | 443에서 받아 앱(`/`, 알림 WebSocket `/ws/`)과 Jenkins(`/jenkins/`)로 프록시. 8443은 Basic Auth 뒤에 Metabase |
| `install-jenkins.sh` | (서버에서 실행) | Jenkins 설치, docker 그룹 등록 |
| `jenkins/override.conf` | `/etc/systemd/system/jenkins.service.d/` | Jenkins 포트·바인딩·프리픽스 |
| `verify.sh` | (서버에서 실행) | 배포 상태 한 번에 확인 |
| `mysql/init/01-analytics-grant.sh` | (compose.local 이 마운트, 테스트가 복사) | 앱 계정에 분석 스키마 권한 |
| `mysql/init/02-analytics-accounts.sh` | (같음) | Metabase 용 읽기 계정 `d205_reader` 와 설정 저장용 `metabase` 계정 |
| `metabase/provision_dashboards.py` | (서버에서 실행) | `docs/analytics-dashboards.md` 의 쿼리로 Metabase 질문·대시보드 생성 |
| `reset-analytics.sh` | (서버에서 실행) | 쌓인 플레이 로그를 백업하고 비웁니다 |

파이프라인 정의는 이 디렉터리가 아니라 `../Jenkinsfile`에 있습니다.

## 구조

외부에서 닿을 수 있는 것은 22(SSH)와 nginx뿐입니다.

```
인터넷 ─┬─ :22  ────────────────▶ sshd
        ├─ :80  ──▶ nginx ──▶ 443 리다이렉트 + 인증서 갱신 챌린지
        ├─ :443 ──▶ nginx ─┬─ /jenkins/        ─▶ 127.0.0.1:9090  Jenkins
                            ├─ /api/v1/events   ─▶ 127.0.0.1:8081  d205-analytics (수집)
                            │                                        └▶ d205-mysql-analytics
                            │                                            ├─ d205_analytics  플레이 로그
                            │                                            └─ metabase        대시보드 설정
                            ├─ /ws/             ─▶ 127.0.0.1:8080  d205-app (WebSocket, 알림)
                            └─ /                ─▶ 127.0.0.1:8080  d205-app (계정·친구·신고·관리 화면)
                                                                     ├▶ d205-mysql (d205 게임 스키마)
                                                                     └▶ analytics:8080/internal/… (compose 안에서만)
        └─ :8443 ──▶ nginx (Basic Auth) ─▶ 127.0.0.1:3000  Metabase ─▶ d205-mysql-analytics (d205_reader, SELECT 만)
```

컨테이너 다섯입니다(`compose.prod.yml`). 대시보드가 `/analytics/` 가 아니라 8443 포트인 이유는
`nginx/d205.conf` 의 8443 블록 주석에 있습니다. 한 줄로 요약하면 Metabase 는 하위 경로 아래에서
동작하지 못합니다.

플레이 로그 수집은 2026-09-14 부터 **별도 서비스와 별도 DB 컨테이너**입니다(S15P21D205-980). 그 전에는
한 앱, 한 MySQL 에 스키마만 갈라 두었는데, 분석 쪽 메모리 폭주나 배포 재시작이 게임 API 를 같이
죽이는 구조였습니다. 지금은 `d205-analytics` 를 `docker kill` 해도 로비·친구·알림·신고가 그대로 돌고,
클라이언트는 실패한 배치를 스풀에 두었다가 다시 보냅니다. 서버가 한 대라 모든 컨테이너에 메모리·CPU
상한을 걸어 두었습니다. 상한이 없으면 한 컨테이너의 폭주가 나머지를 같이 죽여 나눈 의미가 없습니다.

두 서비스는 서로의 DB 에 붙지 않습니다. 계정 서비스가 분석에 시킬 일(탈퇴한 사람의 로그 익명화,
관리 화면 개요 탭의 경기 통계)은 compose 네트워크 안에서 `http://analytics:8080/internal/...` 을 공유
키(`INTERNAL_KEY`)와 함께 부릅니다. nginx 에 그 경로는 없습니다.

## 적용 방법

PowerShell에서 파일을 올리고, 서버 명령은 한 줄로 실행합니다.
여러 줄 붙여넣기는 PowerShell에서 깨집니다.

nginx 설정:

```
scp backend/deploy/nginx/d205.conf d205:/tmp/d205.conf
ssh d205 'sudo install -o root -g root -m 644 /tmp/d205.conf /etc/nginx/sites-available/d205 && sudo ln -sfn /etc/nginx/sites-available/d205 /etc/nginx/sites-enabled/d205 && sudo rm -f /etc/nginx/sites-enabled/default && sudo nginx -t && sudo systemctl reload nginx'
```

`nginx -t`가 실패하면 `&&`가 끊겨 reload까지 가지 않으므로 기존 설정이 유지됩니다.

배포 상태 확인 (`verify.sh` 를 올린 뒤 실행합니다. 아래 다른 절의 `bash /tmp/verify.sh` 는 모두 이 위치를 가리킵니다):

```
scp backend/deploy/verify.sh d205:/tmp/verify.sh
ssh d205 'bash /tmp/verify.sh'
```

### 서비스 분리 배포 순서 (2026-09-14 에 한 번 겪은 순서)

새 compose 가 요구하는 `.env` 키와 nginx 설정은 **Jenkins 가 만들어 주지 않습니다.** 순서를 지키지
않으면 아래처럼 됩니다. 실제로 겪은 일입니다.

1. **머지 전에** `.env` 두 곳(서버 `/home/ubuntu/d205/.env`, Jenkins 비밀 파일 `d205-backend-env`)에
   새 키를 넣습니다. `ANALYTICS_MYSQL_ROOT_PASSWORD`, `ANALYTICS_DB_NAME`(`d205_analytics`),
   `ANALYTICS_DB_USERNAME`, `ANALYTICS_DB_PASSWORD`, 그리고 `INTERNAL_KEY`. 빠지면 빌드가 `docker compose build`
   의 변수 치환 단계에서 `required variable ... is missing a value` 로 멈춥니다. 운영 컨테이너는 그대로라
   서비스는 살아 있지만 배포가 안 됩니다. Jenkins 쪽은 Secret file 의 Update 화면에서 **파일 선택 버튼으로
   새 파일을 고른 뒤** Save 해야 합니다. 파일을 고르지 않고 Save 만 누르면 옛 파일이 남습니다.
   서버에서 값을 만들어 붙이고 그 파일을 내려받아 올리는 순서가 안전합니다. 명령은 한 줄씩 따옴표 없이
   씁니다(PowerShell 이 안쪽 큰따옴표를 벗겨 버립니다).

   ```
   ssh d205 'cd /home/ubuntu/d205 && cp .env .env.bak && echo ANALYTICS_MYSQL_ROOT_PASSWORD=$(openssl rand -hex 24) >> .env && echo ANALYTICS_DB_NAME=d205_analytics >> .env && echo ANALYTICS_DB_USERNAME=d205_analytics >> .env && echo ANALYTICS_DB_PASSWORD=$(openssl rand -hex 24) >> .env && echo INTERNAL_KEY=$(openssl rand -hex 32) >> .env'
   scp d205:/home/ubuntu/d205/.env $env:USERPROFILE\Downloads\d205-backend-env
   ```

   올린 뒤 내려받은 사본은 지웁니다.

2. 머지하면 Jenkins 가 두 이미지를 빌드해 컨테이너 다섯을 교체합니다. 이때 **nginx 는 아직 옛 설정**이라
   `/api/v1/events` 가 8080(계정 서비스)으로 가는데, 새 계정 서비스에는 수집 컨트롤러가 없어 404 가 납니다.
   반대로 nginx 를 먼저 올리면 분석 컨테이너가 뜨기 전까지 502 입니다. 어느 쪽이든 그 사이 수집이 끊기고
   클라이언트 스풀이 버텨 줍니다. 배포 완료 신호는 `ssh d205 'docker ps | grep d205-analytics'` 에
   컨테이너가 보이고 `curl http://localhost:8081/actuator/health` 가 UP 인 것입니다.

3. 배포가 끝나는 **즉시** "적용 방법" 절의 nginx 설정을 올립니다. 그러면 `/api/v1/events` 가 400
   INVALID_REQUEST(빈 본문에 대한 정상 응답)로 돌아옵니다.

4. 아래 이관을 한 번 실행합니다.

### 분석 DB 이관 (서비스 분리 배포 뒤 **한 번**)

분리 전의 플레이 로그와 Metabase 설정은 `d205-mysql` 의 `d205_analytics`·`metabase` 스키마에 있습니다.
새 compose 가 올라가면 `d205-mysql-analytics` 는 빈 채로 뜨고 수집 서비스가 거기에 Flyway 를 돌립니다.
그 뒤 옛 데이터를 옮깁니다. 서버의 `/home/ubuntu/d205` 에는 `.env` 만 있고 `deploy/` 폴더가 없으므로
먼저 만듭니다. compose 파일도 그 경로에 없으니(Jenkins 작업 디렉터리에서 돕니다) Metabase 재시작은
컨테이너 이름으로 합니다.

```
ssh d205 'mkdir -p /home/ubuntu/d205/deploy'
scp backend/deploy/migrate-analytics.sh d205:/home/ubuntu/d205/deploy/migrate-analytics.sh
ssh d205 'cd /home/ubuntu/d205 && bash deploy/migrate-analytics.sh'
ssh d205 'docker restart d205-metabase'
```

백업이 먼저이고 실패하면 거기서 멈춥니다. 스크립트가 끝에 옛·새 컨테이너의 행 수를 나란히 찍으니
`다름!` 이 없는지 보고, Metabase 대시보드가 옮기기 전과 같은 숫자를 보이는지 확인합니다. 옛 스키마는
지우지 않습니다. 며칠 문제가 없으면 `d205-mysql` 에서 `DROP DATABASE d205_analytics; DROP DATABASE metabase;`
를 손으로 실행합니다.

읽기 계정(`d205_reader`)과 Metabase 계정은 `d205-mysql-analytics` 볼륨이 처음 만들어질 때
`deploy/mysql-analytics/init/01-accounts.sh` 가 자동으로 만듭니다. 손으로 할 것이 없습니다. 볼륨이 이미
있는데 계정만 다시 만들어야 하면 그 스크립트를 `docker cp` 로 넣어 `docker exec -e ANALYTICS_READER_PASSWORD -e METABASE_DB_PASSWORD d205-mysql-analytics bash /tmp/01-accounts.sh` 로 실행합니다. 두 번 실행해도 무해합니다.

### 분석 DB Flyway 이력 복구 (2026-09-15, 한 번)

위 이관을 **이미 옛 방식으로 돌렸다면** 분석 DB 의 `flyway_schema_history` 에 분리 전 체크섬이 남아
있습니다. 그러면 수집 서비스가 기동할 때마다 이렇게 죽습니다.

```
Migration checksum mismatch for migration version 1
-> Applied to database : -1736482106
-> Resolved locally    : -1060353966
```

분리 커밋에서 `V1__create_game_event.sql` 의 주석 한 줄이 바뀌었는데 Flyway 는 주석까지 체크섬에
넣습니다. 옛 이력으로 덮으면 그 값이 남습니다. 스키마는 이미 올바른 모양이라 다시 돌릴 것은 없고
이력의 체크섬만 맞추면 됩니다. `flyway repair` 가 하는 일과 같습니다.

**이미 떠 있는 컨테이너는 재검증을 하지 않으므로 살아 있습니다.** 그래서 눈에 안 띄다가 다음 배포의
기동 검증에서 터집니다. 젠킨스의 그 단계는 운영 분석 DB 에 임시 컨테이너를 붙이기 때문에, 고치기
전까지 백엔드 파이프라인이 매번 같은 자리에서 실패합니다.

```
scp backend/deploy/repair-analytics-flyway.sh d205:/home/ubuntu/d205/deploy/repair-analytics-flyway.sh
ssh d205 'cd /home/ubuntu/d205 && bash deploy/repair-analytics-flyway.sh'
```

스크립트가 이력 테이블을 먼저 백업하고, **분리 전 값일 때만** 코드값으로 바꾸고, V1~V4 를 기대값과
대조합니다. 다른 값이 들어 있으면 손대지 않고 멈춥니다. 여러 번 돌려도 무해합니다. 끝나면 젠킨스에서
develop 잡을 다시 실행합니다.

마이그레이션 파일을 고치면 스크립트 안의 기대 체크섬 표도 같이 고쳐야 합니다.


### 계정 DB 일일 백업 (한 번 설치)

플레이 로그 백업은 `reset-analytics.sh` 가 지울 때 뜨지만, 계정 DB(users·friendships·user_reports)는
정기 백업이 없었습니다(S15P21D205-981). `deploy/backup-game-db.sh` 를 매일 새벽 4시에 돌립니다.

```
ssh d205 'cd /home/ubuntu/d205 && bash deploy/backup-game-db.sh'
ssh d205 "( crontab -l 2>/dev/null | grep -v backup-game-db; echo '0 4 * * * cd /home/ubuntu/d205 && bash deploy/backup-game-db.sh >> /home/ubuntu/d205-backups/backup.log 2>&1' ) | crontab -"
ssh d205 'crontab -l'
```

첫 줄은 지금 한 번 떠 보는 것이고, 둘째 줄이 cron 을 등록합니다. 파일은 `/home/ubuntu/d205-backups/`
에 날짜 이름으로 남고 7일 지난 것은 스크립트가 지웁니다. 덤프에 기기 식별자(그 계정의 비밀번호)가
평문으로 들어 있어 권한이 600 입니다. 서버 밖으로 복사하려면 그때 암호화를 따로 정합니다.

**복구**: 컨테이너를 그대로 두고 덤프를 root 로 넣습니다. 덤프에 `CREATE DATABASE`/`USE` 가 들어 있어
스키마 이름을 따로 줄 필요가 없고, 넣기 전에 현재 상태를 한 번 더 백업합니다.

```
ssh d205 'cd /home/ubuntu/d205 && bash deploy/backup-game-db.sh'
ssh d205 "set -a; . /home/ubuntu/d205/.env; set +a; gunzip -c /home/ubuntu/d205-backups/<파일>.sql.gz | docker exec -i d205-mysql sh -c 'MYSQL_PWD=\$MYSQL_ROOT_PASSWORD mysql -uroot'"
ssh d205 'docker restart d205-app'
```

복구 뒤 앱을 재시작하는 이유는 Flyway 가 `flyway_schema_history` 를 다시 읽어 덤프 시점 이후의
마이그레이션을 적용해야 하기 때문입니다. 2026-09-14 에 로컬(`compose.local.yml`)에서 덤프 → 삭제 →
복구 → 계정 조회까지 한 번 확인했습니다.

### `.env` 의 키

`compose.prod.yml` 이 `:?` 로 요구하는 키는 열입니다. 하나라도 비면 compose 파싱 단계에서 멈춥니다.

| 키 | 쓰는 곳 |
| --- | --- |
| `MYSQL_ROOT_PASSWORD` | mysql 컨테이너, `verify.sh`, `backup-game-db.sh` |
| `DB_NAME` | mysql(`MYSQL_DATABASE`), app, `verify.sh` |
| `DB_USERNAME` | mysql(`MYSQL_USER`), app. 컨테이너의 `MYSQL_USER` 와 앱의 `DB_USERNAME` 은 같은 계정입니다 |
| `DB_PASSWORD` | mysql(`MYSQL_PASSWORD`), app |
| `ANALYTICS_MYSQL_ROOT_PASSWORD` | mysql-analytics 컨테이너, `reset-analytics.sh`, `migrate-analytics.sh` |
| `ANALYTICS_DB_NAME` | mysql-analytics(`MYSQL_DATABASE`), analytics. `d205_analytics` 로 두세요. 이관 스크립트와 Metabase 가 그 이름을 씁니다 |
| `ANALYTICS_DB_USERNAME` / `ANALYTICS_DB_PASSWORD` | mysql-analytics(`MYSQL_USER`/`MYSQL_PASSWORD`), analytics |
| `ANALYTICS_READER_PASSWORD` | mysql-analytics 초기화 스크립트가 만드는 읽기 계정. Metabase 가 데이터를 볼 때 이 계정 |
| `METABASE_DB_PASSWORD` | metabase(`MB_DB_PASS`), mysql-analytics 초기화 스크립트 |

`INTERNAL_KEY` 는 `:?` 가 아니지만 **비우면 두 기능이 조용히 멈춥니다**(S15P21D205-1002). 계정 서비스가
분석 서비스의 `/internal/...` 을 부를 때 쓰는 공유 키인데, 분석 서비스는 키가 없거나 틀리면 404 로
답합니다. 탈퇴한 사람의 로그 익명화와 관리 화면 개요 탭의 경기 통계가 그 경로입니다. 길고 무작위인
값을 하나 만들어 두 서비스가 같은 `.env` 에서 읽게 하면 됩니다. 두 서비스의 기동 로그에 키가 비었다는
경고가 남습니다.

`PHOTON_AUTH_SECRET` 과 `PHOTON_AUTH_KEY` 도 선택입니다(S15P21D205-925). 없으면 Photon 커스텀
인증이 꺼진 채로 뜨고 게임은 그대로 돌아갑니다. **`.env` 에 넣는 것만으로는 앱에 닿지 않습니다** -
compose 는 `.env` 를 치환에만 쓰고 컨테이너에 전달하는 것은 `environment:` 에 적힌 것뿐이라,
`compose.prod.yml` 의 목록에도 있어야 합니다. 둘 중 하나만 하면 앱이 값을 못 보고 인증이 조용히
꺼진 채로 뜹니다.

선택인 키는 `ADMIN_USERNAME`, `ADMIN_PASSWORD` 입니다. 없으면 관리 API 만 막히고 게임은 돌아가므로
`:?` 를 붙이지 않았습니다.

같은 내용이 서버의 `/home/ubuntu/d205/.env` 와 Jenkins 의 비밀 파일 `d205-backend-env` 두 곳에 있어야 합니다.

### 대시보드 (Metabase) 처음 올리기

순서가 중요합니다. `.env` 가 먼저고, 계정이 그다음이고, 배포는 마지막입니다.
`compose.prod.yml` 이 `METABASE_DB_PASSWORD` 를 `:?` 로 요구하므로 `.env` 에 없으면
Jenkins 의 compose 단계가 그 자리에서 멈춥니다.

**1. `.env` 에 두 줄 추가. 두 곳에.** 값은 길고 무작위로, 그리고 **두 곳이 같아야** 합니다.

```
ANALYTICS_READER_PASSWORD=...
METABASE_DB_PASSWORD=...
```

- 서버의 `/home/ubuntu/d205/.env`: `mysql-analytics` 컨테이너가 이 두 줄로 계정을 만듭니다. `verify.sh` 도
  이 파일을 읽지만 `MYSQL_ROOT_PASSWORD` 와 `DB_NAME` 만 씁니다.
- **Jenkins 의 비밀 파일 `d205-backend-env`**: 배포의 compose 가 읽습니다. `Jenkins 관리 → Credentials
  → d205-backend-env → Update` 에서 두 줄을 더한 파일을 올립니다. 이걸 빠뜨리면 develop 빌드가
  `required variable METABASE_DB_PASSWORD is missing a value` 로 멈춥니다(2026-09-07 #79). 서비스는
  교체 전이라 멀쩡하고, 파일을 올린 뒤 "지금 빌드" 를 누르면 됩니다.
- 두 곳의 값이 다르면 Metabase 가 자기 DB 에 못 붙어 재시작을 반복합니다. 해시로 비교하려면
  `ssh d205 'set -a; . /home/ubuntu/d205/.env; set +a; printf %s $METABASE_DB_PASSWORD | md5sum; printf %s $(docker exec d205-metabase printenv MB_DB_PASS) | md5sum'`
  두 줄이 같아야 합니다.

**2. MySQL 계정.** 손으로 할 것이 없습니다. `d205-mysql-analytics` 볼륨이 처음 만들어질 때
`deploy/mysql-analytics/init/01-accounts.sh` 가 `.env` 의 두 값으로 읽기 계정과 Metabase 계정을 만듭니다.
`docker logs d205-mysql-analytics 2>&1 | grep analytics-init` 에 두 줄이 찍혀 있으면 된 것입니다.
볼륨이 이미 있는 상태에서 비밀번호를 바꿨다면 위 "분석 DB 이관" 절 끝의 `docker exec` 한 줄로 다시 만듭니다.

**3. nginx 8443 과 Basic Auth**

```
ssh -t d205 "sudo apt-get install -y apache2-utils && sudo htpasswd -c /etc/nginx/.htpasswd-analytics d205"
ssh d205 "sudo ufw allow 8443/tcp"
scp backend/deploy/nginx/d205.conf d205:/tmp/d205.conf
ssh d205 "sudo install -o root -g root -m 644 /tmp/d205.conf /etc/nginx/sites-available/d205 && sudo ln -sfn /etc/nginx/sites-available/d205 /etc/nginx/sites-enabled/d205 && sudo rm -f /etc/nginx/sites-enabled/default && sudo nginx -t && sudo systemctl reload nginx"
```

넷째 줄은 위 "nginx 설정" 과 같은 명령입니다. `sites-enabled/default` 를 내리는 부분을 빼면
같은 `server_name` 의 443 블록이 둘이 되어 nginx 가 먼저 나온 쪽만 씁니다(`d205.conf` 머리 주석).

첫 줄은 비밀번호를 물어봅니다. 프롬프트가 떠야 하므로 PowerShell 에서는 `ssh -t` 로 실행하고,
명령은 한 번만 붙여 넣습니다(두 번 붙으면 htpasswd 가 인자를 잘못 받아 사용법만 출력합니다).
그 계정과 비밀번호를 팀에 공유합니다. 저장소에는 두지 않습니다.
EC2 보안 그룹의 8443 은 2026-09-07 에 열었습니다. 다른 계정으로 EC2 를 새로 받으면 다시 열어야 합니다.

8443 에서 **어떤 계정을 넣어도 403** 이면 비밀번호 파일이 없는 것입니다. nginx 는 `auth_basic_user_file`
이 없으면 401 대신 403 을 냅니다. `ls -l /etc/nginx/.htpasswd-analytics` 로 확인하고 위 첫 줄을 다시
실행하세요. 파일은 요청마다 읽으므로 nginx 재시작은 필요 없습니다.

Basic Auth 를 통과했는데 **502** 면 Metabase 컨테이너가 3000 에서 응답하지 않는 것입니다.
`docker logs d205-metabase 2>&1 | grep -E 'Initialization (FAILED|COMPLETE)'` 로 봅니다. FAILED 가
반복되면 자기 DB 에 못 붙는 것이고, 원인은 셋 중 하나입니다. 계정 스크립트(2번) 미실행, 두 `.env`
값 불일치(1번), 또는 compose 의 `MB_DB_CONNECTION_URI` 에서 `allowPublicKeyRetrieval=true` 가 빠짐.
Metabase 는 원인 예외를 로그에 남기지 않아 이 셋을 순서대로 확인해야 합니다.

**4. 배포.** develop 에 머지하면 Jenkins 가 `compose up` 으로 Metabase 컨테이너까지 올립니다.
첫 기동은 자기 스키마에 마이그레이션을 돌려 1분 넘게 걸리고 메모리를 1GB 가까이 씁니다.
`bash /tmp/verify.sh` 의 "대시보드" 절에서 내부 3000 이 응답하고 8443 이 401 이면 정상입니다.

**5. Metabase 첫 설정** (브라우저, `https://j15d205.p.ssafy.io:8443`, Basic Auth 뒤에 Metabase
자체 관리자 계정을 만드는 화면이 뜹니다):

- 관리자 계정을 만들고 그 정보를 팀에 공유합니다.
- "데이터베이스 추가" 에서 MySQL, 호스트 `mysql-analytics`, 포트 `3306`, 데이터베이스 `d205_analytics`,
  사용자 `d205_reader`, 비밀번호는 `.env` 의 `ANALYTICS_READER_PASSWORD`. 이름은 "플레이 로그".
- 게임 DB 는 등록하지 않습니다. 분석 DB 컨테이너에는 게임 스키마가 없고, 읽기 계정도 그쪽에 권한이 없습니다.
  플레이 로그에는 닉네임이 없어 `userId` 문자열만 보이는데, 그것이 의도입니다(개인정보). 이름이 필요한
  조회는 관리 화면(사용자 탭)에서 합니다.
- 앱 계정(`ANALYTICS_DB_USERNAME`)을 넣지 마세요. 그 계정은 쓸 수 있는 계정이라 Metabase 의 SQL 창이
  로그를 지우는 창이 됩니다.

**6. 기본 대시보드.** 데이터베이스를 등록했으면 화면은 스크립트가 만듭니다. 문서의 쿼리를 읽어
질문 다섯 개와 대시보드 하나를 만들고, 여러 번 돌려도 이름으로 찾아 갱신합니다.

```
scp backend/docs/analytics-dashboards.md backend/deploy/metabase/provision_dashboards.py d205:/tmp/
ssh -t d205 "MB_USER=<Metabase 관리자 이메일> python3 /tmp/provision_dashboards.py --doc /tmp/analytics-dashboards.md"
```

비밀번호는 물어봅니다. 8443 이 아니라 컨테이너 옆 `127.0.0.1:3000` 으로 붙으므로 Basic Auth 는
지나지 않습니다. 무엇을 만들지 먼저 보려면 `--dry-run`, 화면을 읽는 법은
`docs/analytics-dashboards.md` 입니다.

### 플레이 로그 지우기

`game_event` 에는 보존 기간도 자동 삭제도 없습니다. 디스크가 찰 때까지 쌓입니다. 컨테이너와 볼륨은
게임 DB 와 다르지만 **디스크는 같은 EC2 것**이라, 가득 차면 게임 DB 도 쓰기를 못 합니다. 플레이테스트
사이에 비우려면(스크립트는 `d205-mysql-analytics` 를 봅니다):

```
scp backend/deploy/reset-analytics.sh d205:/tmp/
ssh -t d205 'bash /tmp/reset-analytics.sh'
```

`-t` 가 필요합니다. 지우기 전에 `yes` 를 직접 입력받는데 tty 가 없으면 진행하지 않습니다.
자동화에서 부를 때만 `--yes` 를 주세요. 얼마나 쌓였는지만 보려면 `--dry-run` 입니다.

백업이 먼저이고 실패하면 거기서 멈춥니다. 백업 없이 지우는 경로는 없습니다. 파일은 배포
디렉터리 바깥인 `/home/ubuntu/d205-backups/` 에 남습니다 - 재배포가 그 안을 건드리지 않게
하려는 것이고, 같은 디스크이므로 디스크 장애까지 막아주지는 않습니다.

지우는 것은 `game_event` 한 테이블뿐입니다. `flyway_schema_history` 를 같이 지우면 다음
배포가 V1~V4 를 처음부터 다시 실행하려다 실패합니다. 뷰는 실체화가 아니라 원본을 그때그때
읽으므로 따로 비울 것이 없고, Metabase 의 질문·대시보드는 `metabase` 스키마에 따로 있어
그대로 남습니다.

Jenkins 설치:

```
scp backend/deploy/install-jenkins.sh d205:/tmp/install-jenkins.sh
ssh d205 'bash /tmp/install-jenkins.sh'
scp backend/deploy/jenkins/override.conf d205:/tmp/override.conf
ssh d205 'sudo install -d -m 755 /etc/systemd/system/jenkins.service.d && sudo install -o root -g root -m 644 /tmp/override.conf /etc/systemd/system/jenkins.service.d/override.conf && sudo systemctl daemon-reload && sudo systemctl reset-failed jenkins && sudo systemctl start jenkins'
```

## 알아둘 것

**Jenkins는 docker 소켓 권한을 갖습니다.** 호스트 파일시스템을 마운트한
컨테이너를 띄울 수 있으므로 사실상 root입니다. Jenkins 관리자 계정이 뚫리면
EC2 전체가 넘어갑니다. 그래서 9090을 루프백에만 바인딩하고 nginx 뒤에 둡니다.

**Docker는 ufw를 우회합니다.** 컨테이너 포트를 publish하면 Docker가 ufw보다
먼저 평가되는 규칙을 넣습니다. `compose.prod.yml`에서 MySQL 포트를 열지 않는
이유가 이것입니다 — ufw로 3306을 막아뒀어도 publish하면 인터넷에 열립니다.

**설치 직후 Jenkins는 기동 실패 상태입니다.** 기본 포트 8080을 앱 컨테이너가
쓰고 있기 때문입니다. 드롭인을 넣기 전에 실패가 5회 반복되면 systemd가 시작을
거부하므로(`Start request repeated too quickly`) `reset-failed`가 필요합니다.

**인증서는 certbot이 관리합니다.** `certbot.timer`가 자동 갱신하고 갱신에는
80번이 열려 있어야 합니다. ufw에서 80을 닫으면 90일 뒤에 만료됩니다.

### 알림 WebSocket 은 nginx 에 별도 블록이 필요합니다

`d205.conf` 의 `/ws/` 블록이 `/ws/notifications` 를 앱(8080)으로 넘깁니다. `/` 블록과 목적지가 같은데
따로 둔 이유가 둘입니다.

- `proxy_http_version 1.1` 에 `Upgrade` 와 `Connection "upgrade"` 헤더를 넘겨야 합니다. 없으면 nginx 가
  일반 HTTP 로 취급해 핸드셰이크가 400(`Can "Upgrade" only to "WebSocket".`)으로 끝납니다.
- `proxy_read_timeout`/`proxy_send_timeout` 이 90초입니다. 서버가 `notifications.ping-interval-ms`
  (`application.yml`, 30000) 마다 ping 을 보내므로 그보다 길어야 합니다. `/` 블록의 60초를 그대로 쓰면
  조용한 연결을 nginx 가 먼저 끊어 클라이언트가 이유 없이 재연결을 반복합니다.

**Jenkins 는 compose 의 컨테이너만 배포합니다.** nginx 설정은 파이프라인이 건드리지 않으므로 `d205.conf` 를
고치면 위 "적용 방법" 의 nginx 설정 명령(scp 후 install, `nginx -t`, reload)을 손으로 실행해야 합니다.
`/ws/` 블록이 없는 서버에 앱만 배포하면 알림 핸드셰이크는 400 으로 끝납니다.

**동시 접속자 상한은 nginx 와 Tomcat 중 작은 쪽입니다.** 클라이언트마다 알림 연결 하나를 상시 붙들고
있어서, 받을 수 있는 수는 nginx 의 `worker_connections` 와 앱의 `server.tomcat.max-connections`
(`application.yml`, 8192) 중 작은 값입니다. 프록시라 접속자 하나가 연결 둘(클라이언트↔nginx, nginx↔앱)을
쓰고, nginx 의 기본값은 768 입니다. nginx 전역 설정(`/etc/nginx/nginx.conf`)은 저장소에 없어
`verify.sh` 의 "상시 연결 (알림 WebSocket)" 절이 서버의 `worker_connections` 와 지금 443, 8080 에 맺힌
연결 수를 찍습니다. 거기 768 이 보이면 nginx 가 먼저 막히는 것이고, `/etc/nginx/nginx.conf` 의
`events { worker_connections ... }` 를 올려야 합니다. 이것도 Jenkins 가 아니라 손으로 고치는 설정입니다.

### Jenkins 체크아웃이 10분 타임아웃으로 죽으면

증상: 콘솔이 `git checkout -f <sha>` 에서 멈춰 `ERROR: Timeout after 10 minutes`,
`fatal: the remote end hung up unexpectedly` 로 끝나고, 코드와 무관하게 모든 MR 이 빨간불입니다.
2026-09-07 에 클라이언트가 LFS 클립을 대량으로 올린 뒤 그렇게 됐습니다.

원인: `git checkout` 이 LFS 스머지 필터로 Unity 에셋 3천 개를 GitLab 에서 내려받는데, 백엔드
파이프라인은 그 파일을 쓰지 않습니다. 내려받기가 느려지면 체크아웃 자체가 타임아웃입니다.

해결은 스머지를 끄는 것입니다. 지금은 **파이프라인이 스스로 합니다.** `Jenkinsfile` 의 `체크아웃`
단계가 `git lfs install --skip-smudge --skip-repo` 로 jenkins 사용자의 전역 gitconfig 에 스머지 생략을
쓰고, `GIT_LFS_SKIP_SMUDGE=1` 을 건 채 `checkout scm` 을 실행합니다. 매 빌드가 같은 설정을 다시 쓰므로
Jenkins 를 다시 설치해도 따로 손볼 것이 없습니다.

아래 두 서버 쪽 조치는 그 전에 쓰던 것으로, 지금은 **예비**입니다. 그 `체크아웃` 단계가 없는 머신이나
`git lfs` 가 설치되지 않아 첫 줄이 건너뛰어지는 머신에서만 필요합니다. 첫째는 지금 당장 듣고, 둘째는
재시작 뒤에도 남습니다.

```
ssh d205 "sudo -u jenkins -H git lfs install --skip-smudge && sudo -u jenkins -H git config --global --get filter.lfs.smudge"
```

출력이 `git-lfs smudge --skip -- %f` 면 적용된 것입니다. 실행 중인 빌드가 없을 때 아래로 드롭인도 갱신합니다.

```
scp backend/deploy/jenkins/override.conf d205:/tmp/override.conf
ssh d205 "sudo install -o root -g root -m 644 /tmp/override.conf /etc/systemd/system/jenkins.service.d/override.conf && sudo systemctl daemon-reload && sudo systemctl restart jenkins"
```

그 뒤 실패한 MR 의 빌드를 다시 돌립니다. 브랜치에 커밋을 하나 푸시하면 웹훅이 다시 돌리고,
아니면 Jenkins 의 해당 `MR-*` 잡에서 "지금 빌드" 를 누릅니다. 체크아웃은 수 초로 끝나야 합니다.

## Jenkins Job 설정

`Jenkinsfile`에 담을 수 없는 설정입니다. Job을 다시 만들면 여기 보고 복원하세요.

Job 이름 `d205-backend`, 종류 **Multibranch Pipeline**.

| 위치 | 항목 | 값 |
| --- | --- | --- |
| Branch Sources | 종류 | `GitLab Project` |
| Branch Sources | Server | `ssafy-lab` |
| Branch Sources | Checkout Credentials | `gitlab-deploy-token` |
| Branch Sources | Owner | `s15-metaverse-game-sub1` |
| Branch Sources | Projects | `S15P21D205` |
| Behaviours | Discover merge requests from origin | **`Merging the merge request with the current target branch revision`** |
| Behaviours | Filter by name (with wildcards) → Include | **`develop MR-*`** |
| Build Configuration | Mode | `by Jenkinsfile` |
| Build Configuration | Script Path | `backend/Jenkinsfile` |
| Scan Triggers | Periodically if not otherwise run | `1 day` (웹훅이 주 트리거. 웹훅이 죽었을 때의 안전망은 `Jenkinsfile` 의 `pollSCM('H/10 * * * *')`) |
| Orphaned Item Strategy | Discard old items | 7일 / 20개 |

두 값이 특히 중요합니다.

**MR 발견 전략**을 `Merging the merge request with the current target branch revision`
으로 둬야 합니다. 이게 "MR을 develop에 합친 결과"를 빌드하는 설정이고, 각각은
깨끗하게 머지되는데 합치면 깨지는 의미 충돌을 잡는 유일한 장치입니다.

**이름 필터 `develop MR-*`**가 없으면 낡은 브랜치가 배포합니다. Multibranch는
각 브랜치의 Jenkinsfile을 읽으므로, 분기 시점이 오래된 브랜치는 DEPLOY 가드가
없던 시절의 파이프라인을 실행하고 곧바로 배포로 갑니다. 실제로 문서 브랜치가
운영에 배포한 일이 있었습니다. MR은 `MR-<번호>` 형태라 `MR-*`로 잡힙니다.

### GitLab 서버 연결

Jenkins 관리 → System → GitLab 섹션.

| 항목 | 값 |
| --- | --- |
| Name | `ssafy-lab` |
| Server URL | `https://lab.ssafy.com` |
| Credentials | `gitlab-api-token` |
| Manage Web Hooks | 체크하지 않음 (웹훅은 GitLab에서 직접 등록) |

### 크리덴셜

| ID | 종류 | 내용 |
| --- | --- | --- |
| `gitlab-deploy-token` | Username with password | Deploy Token (`read_repository`). 클론용 |
| `gitlab-api-token` | GitLab Personal Access Token | Project Access Token, 역할 **`Developer`**, 스코프 `api` |
| `gitlab-api-token-text` | Secret text | **`gitlab-api-token`과 같은 값.** 파이프라인 스크립트용 |
| `d205-backend-env` | Secret file | prod `.env`. 원본은 서버의 `/home/ubuntu/d205/.env` |

`gitlab-api-token`의 역할이 `Reporter`면 **커밋 상태를 게시할 수 없습니다**(403).
그러면 MR에 초록·빨간불이 뜨지 않아 머지 차단이 성립하지 않습니다. 역할은
발급 후 변경이 불가하므로 잘못 만들었으면 폐기하고 다시 발급해야 합니다.

`d205-backend-env`는 `Jenkinsfile`이 코드에서 직접 참조하므로 ID를 바꾸면 빌드가 깨집니다.

**토큰이 두 곳에 있는 이유가 있습니다.** GitLab 플러그인의 토큰 크리덴셜은
`StringCredentials`가 아니어서 파이프라인의 `string()` 바인딩으로 읽을 수 없습니다
(`is of type 'GitLab Personal Access Token' where 'StringCredentials' was expected`).
플러그인은 `gitlab-api-token`을, 파이프라인 스크립트는 `gitlab-api-token-text`를
씁니다. **토큰을 갱신할 때 두 개를 모두 고쳐야 합니다.** 하나만 고치면 한쪽이
조용히 실패합니다.

### 웹훅

GitLab → Settings → Webhooks.

| 항목 | 값 |
| --- | --- |
| URL | `https://j15d205.p.ssafy.io/jenkins/gitlab-webhook/post` |
| Trigger | `Push events`, `Merge request events` |
| Secret token | 비움 (Jenkins 쪽도 `none`이라 양쪽이 맞아야 함) |
| SSL verification | 활성화 |

URL은 Job과 무관한 고정 주소입니다. GitLab Branch Source 플러그인이 제공합니다.

### 머지 차단

GitLab → Settings → Merge requests → Merge checks → `Pipelines must succeed`.

**`main`을 대상으로 하는 MR에 주의하세요.** `main`과 `release`에는 `backend/`가
없어서, 머지 결과에 `backend/Jenkinsfile`이 없으면 Job이 생기지 않고 파이프라인도
없습니다. 그 상태로는 머지가 막힙니다. `develop → release → main` 순서를 지키면
`release`에 `backend/`가 들어온 뒤이므로 문제가 없습니다.

## 배포 파이프라인

`../Jenkinsfile`이 정의합니다. 하나의 파일이 두 경로를 처리합니다.

```
                      develop            MR / 그 외
  체크아웃              O                    O
  대상 확인             O                    O
  빌드                  O (compose)          -
  빌드 (검증)            -                   O (docker build, verify 태그)
  테스트                O                    O
  기동 검증 (운영 DB)    O                    -
  기동 검증 (일회용 DB)   -                   O
  배포                  O                    -
  헬스체크              O                    -
```

**체크아웃**이 명시적 단계인 이유는 `skipDefaultCheckout(true)` 로 암묵적 체크아웃을 끄고
LFS 스머지를 생략한 채 직접 받기 때문입니다. 자세한 것은 "Jenkins 체크아웃이 10분 타임아웃으로
죽으면" 에 있습니다.

`DEPLOY` 판정은 `BRANCH_NAME` 기준입니다. `when { branch 'develop' }` 는
Multibranch에서만 동작하고 단독 Job에는 `BRANCH_NAME`이 없어 조건이 false가 되므로,
그대로 쓰면 빌드는 초록불인데 배포가 멈추는 상태가 됩니다.

**대상 확인**이 `backend/` 변경 여부를 판별해 없으면 성공으로 보고하고 나머지를
건너뜁니다. 실패로 끝내면 머지 차단을 켰을 때 백엔드와 무관한 MR이 전부 막힙니다.
비교 기준은 경로별로 다릅니다. MR은 `origin/$CHANGE_TARGET...HEAD`, develop은
`GIT_PREVIOUS_SUCCESSFUL_COMMIT..HEAD`입니다. develop을 하드코딩하면 `release`
대상 MR에서 엉뚱한 비교를 합니다.

**기동 검증**이 핵심입니다. `docker compose up`은 컨테이너를 먼저 교체하고 앱은
그 뒤에 뜹니다. 곧바로 배포하면 마이그레이션 오류나 설정 오류가 그대로 서비스
다운이 됩니다. 임시 컨테이너를 먼저 띄워 `/actuator/health`가 UP인지 확인하면
그런 실패가 살아 있는 서비스를 건드리기 전에 드러납니다.

검증 DB가 경로별로 다른 이유가 있습니다. **MR은 일회용 MySQL**을 쓰고 운영
크리덴셜을 받지 않습니다. 남의 브랜치가 운영 DB나 비밀번호를 만질 이유가 없습니다.
**develop은 운영 DB**를 씁니다. 빈 DB에서는 통과하지만 기존 데이터가 있으면
실패하는 마이그레이션이 있어서, 실제 스키마 상태에 대고 확인해야 합니다.

임시 컨테이너의 호스트 포트는 `0`으로 두어 도커가 빈 포트를 고르게 합니다.
MR Job들은 서로 병렬로 돌기 때문에 고정 포트는 충돌합니다.

컨테이너·네트워크·이미지 이름에는 **`BUILD_TAG`**를 넣습니다. Job 이름까지 포함해
전역적으로 유일합니다. `BUILD_NUMBER`는 Job 단위라서, 서로 다른 MR Job이 모두
빌드 #1이면 이름이 겹칩니다. 실제로 MR 두 개가 동시에 돌면서 한쪽은 네트워크
생성에 실패하고 다른 쪽은 정리 단계에 컨테이너를 빼앗겼습니다.

**배포할 때 이미지에 이름표를 남깁니다.** `d205-app:<배포시각UTC>-<커밋SHA12>`
형태로 최근 5개를 유지합니다. compose는 `d205-app:latest` 하나만 쓰기 때문에,
이름표가 없으면 다음 빌드가 그 이름을 가져가는 순간 이전 이미지가 사라져 되돌릴
대상이 없어집니다. 이름 앞에 시각을 넣는 것은 정리 순서를 정하기 위해서입니다.
도커의 `CreatedAt`은 캐시에서 상속되므로 빌드 시각과 다릅니다 — 전부 CACHED로
끝난 빌드는 몇 시간 전 시각을 그대로 물고 있어서, 그걸로 정렬하면 엉뚱한 것을
지웁니다. 정리에 쓰는 `docker rmi`에는 `-f`를 붙이지 않습니다. 돌고 있는
컨테이너가 쓰는 이미지는 도커가 거부하므로 실수로 지울 수 없습니다.

**develop 빌드는 대상 확인의 판별 직후에 열려 있는 MR의 재빌드를 요청합니다.**
GitLab Free에는 merged results pipeline과 merge train이 없어 대상 브랜치가 움직여도
MR이 자동으로 재검증되지 않습니다. 그러면 MR은 "이전 develop에 합친 결과"로 받은
초록불을 그대로 들고 있게 되고, 그 상태로 머지하면 검증되지 않은 조합이 들어갑니다.

**빌드 마지막이 아니라 맨 앞에서 하는 이유는 그 낡은 초록불이 보이는 시간을 줄이는
것입니다.** 처음에는 `post success`에 뒀는데, 그러면 develop 빌드가 끝날 때까지 MR은
낡은 초록불을 그대로 보여주고 머지 버튼도 열려 있습니다. 같은 MR로 재보니 머지 후
**70초**였고, 판별 직후로 옮겨 **10초**가 됐습니다.

빌드 시작 시점까지 더 당길 수는 없습니다. 초록불이 낡는 순간은 develop ref가
움직이는 그 순간이고, 그건 Jenkins가 개입하기 전입니다. 남는 틈의 대응은
"배포가 깨졌을 때 5번"에 적어뒀습니다.

재검증 요청이 실패하면 빌드가 `UNSTABLE`로 끝나지만 빌드와 배포는 계속 진행합니다.
재검증은 이 빌드의 본 임무가 아니어서 그것 때문에 배포를 막을 이유가 없습니다.
다만 조용히 넘기면 무효화가 죽은 것을 아무도 모르므로 눈에 띄게 남깁니다.
**GitLab은 `UNSTABLE`을 `failed`로 표시합니다.** develop 커밋에 빨간불이 뜨지만
배포는 정상이라는 뜻이니, 그때는 빌드 로그의 재검증 부분을 확인하세요.

커밋 상태를 직접 게시하지 않고 재빌드를 요청하는 이유가 있습니다. GitLab은 상태를
(SHA, 컨텍스트) 쌍으로 관리하므로, 플러그인과 다른 컨텍스트로 `pending`을 올리면
그 상태가 영구히 남아 **머지가 영원히 잠깁니다.** 재빌드를 요청하면 플러그인이
스스로 `pending`을 올려 즉시 잠그고, 완료 후 최종 상태로 갱신합니다.

### 파괴적 마이그레이션은 두 배포로 나누세요

기동 검증의 임시 컨테이너가 운영 DB에 마이그레이션을 적용하는 20초 동안,
**이전 버전이 아직 살아 있습니다.** 컬럼 추가처럼 덧붙이는 변경은 안전하지만
`DROP COLUMN` 같은 파괴적 변경은 그 사이 이전 버전이 에러를 낼 수 있습니다.
먼저 코드에서 그 컬럼 사용을 없애 배포하고, 그다음 배포에서 컬럼을 지웁니다.

### 적용된 마이그레이션은 주석도 고치지 마세요

Flyway 체크섬은 SQL 본문이 아니라 **파일 전체**로 냅니다. 주석 한 줄만 바꿔도 값이 달라지고, 그 파일이
이미 어느 DB 에 적용돼 있었다면 그 DB 에 붙는 서비스가 기동 검증에서 죽습니다. 2026-09-15 에
이것으로 develop 파이프라인이 멈췄습니다(S15P21D205-1009).

설명을 고치고 싶으면 파일이 아니라 `docs/` 에 씁니다. 파일을 꼭 고쳐야 하면 새 버전을 추가하거나,
바꾼 뒤 위의 복구 절차로 이력을 맞춥니다.


### 다운타임

정상 배포에도 교체 순간 **수 초의 다운타임**이 있습니다. 이 파이프라인이 없애는
것은 실패로 인한 장시간 다운타임이지 교체 순간의 공백이 아닙니다. 그걸 없애려면
nginx upstream을 바꿔치는 블루-그린이 필요합니다.

## 배포가 깨졌을 때

### 1. 서비스가 살아 있는지 먼저 확인

파이프라인 실패가 곧 서비스 다운은 아닙니다.

```
scp backend/deploy/verify.sh d205:/tmp/verify.sh
ssh d205 'bash /tmp/verify.sh'
```

| 실패 단계 | 서비스 | 긴급도 |
| --- | --- | --- |
| 대상 확인 / 빌드 / 테스트 | 정상 | 낮음. 고쳐서 다시 푸시 |
| 기동 검증 | 정상 — 교체 전에 멈춤 | 낮음 |
| 배포 / 헬스체크 | 내려갔을 수 있음 | 높음 |

### 2. 서비스가 내려갔으면 되돌리기

정석은 revert입니다. GitLab의 머지된 MR 페이지에 `Revert` 버튼이 있고, 누르면
되돌리는 MR이 생성됩니다. 머지하면 웹훅이 즉시 이전 상태로 재배포합니다.

명령으로 하면 이렇습니다. `-m 1`은 "머지 커밋의 첫 번째 부모 쪽으로 되돌린다"는
뜻이고 머지 커밋을 revert할 때 필수입니다.

```
git fetch origin
git checkout -b revert/broken-deploy origin/develop
git revert -m 1 <머지커밋SHA>
```

**머지 차단이 켜져 있으므로 revert MR도 파이프라인을 통과해야 합니다.** revert가
깨진 코드를 되돌리는 것이라면 통과합니다. 통과하지 못하면 revert 자체에 문제가
있다는 신호이니 로그를 보세요.

승인을 기다릴 수 없는 급한 상황에서는 서비스를 먼저 살립니다. 배포할 때마다
이미지에 이름표를 남기므로 이전 버전으로 즉시 되돌릴 수 있습니다.

```
ssh d205 'docker images d205-app --format "{{.Tag}}  {{.ID}}" | sort -r'
```

이름표는 `<배포시각UTC>-<커밋SHA12>` 형태라 정렬하면 최근 순입니다. 되돌릴 것을
골라 이렇게 합니다.

```
W=/var/lib/jenkins/workspace/d205-backend_develop/backend
sudo cp /home/ubuntu/d205/.env $W/.env
cd $W
docker tag d205-app:<고른-이름표> d205-app:latest
docker compose -f compose.prod.yml up -d --force-recreate app
```

`.env`를 복사하는 단계가 필요한 이유는 **파이프라인이 매 빌드 끝에 그 파일을
지우기** 때문입니다. compose가 `${VAR:?}`로 읽으므로 없으면 기동조차 못 합니다.

**마이그레이션이 있었던 배포에는 이 방법이 듣지 않습니다.** DB는 되돌아가지 않으므로
되돌린 이미지 안에는 이미 적용된 마이그레이션 스크립트가 없습니다. Flyway는 기본
설정에서 그것을 검증 실패로 처리해 기동을 거부합니다. 그때는 3번으로 가세요.

Jenkins의 Replay로 이전 성공 빌드를 다시 돌리는 방법은 기대하지 마세요. 대상 확인이
`GIT_PREVIOUS_SUCCESSFUL_COMMIT..HEAD`를 보므로 **변경 0개로 판정해 전부 건너뛸
가능성이 높습니다.** 시험해보지 않았고, 급할 때 시험할 일도 아닙니다.

단 `develop`은 여전히 깨진 상태이므로 **반드시 revert를 이어서 해야 합니다.**
안 하면 다음 배포가 깨진 커밋을 다시 올립니다.

### 3. 마이그레이션이 실패한 경우

가장 손이 많이 갑니다. Flyway가 실패하면 `flyway_schema_history`에 `success=0` 행이
남고 **그 뒤로 모든 기동이 막힙니다**. 이전 버전으로 되돌려도 마찬가지입니다.
게다가 MySQL의 DDL은 트랜잭션이 아니라서 `ALTER TABLE` 여러 개 중 중간에서 깨지면
앞의 것은 적용된 채 남습니다.

1. `flyway_schema_history`에서 실패 행 확인
2. 부분 적용된 DDL을 SQL로 직접 되돌림
3. `flyway repair`로 실패 기록 정리
4. 마이그레이션 파일을 고쳐 다시 배포

**마이그레이션은 반드시 로컬에서 먼저 적용해보고 커밋하세요.** `compose.local.yml`로
DB를 띄우고 `bootRun`으로 확인하면 됩니다. 이 습관이 파이프라인의 어떤 장치보다
강력합니다. 기동 검증이 운영 DB에 대고 확인하지만, 그건 마지막 방어선입니다.

### 4. 낡은 브랜치가 배포한 경우

Multibranch는 **각 브랜치의 Jenkinsfile을 읽습니다.** 이름 필터(`develop MR-*`)가
풀리면 분기 시점이 오래된 브랜치가 DEPLOY 가드 없는 옛 파이프라인을 실행해
운영에 배포할 수 있습니다. 그 브랜치의 backend 소스가 낡았으면 운영이 롤백됩니다.

빌드 로그의 단계 목록이 지금 파이프라인과 다르면(예: `배포`와 `헬스체크`만 있으면)
이 경우입니다. Job 설정의 이름 필터를 먼저 복원하고, 그다음 `develop`을 수동
빌드해 정상 버전으로 되돌리세요.

### 5. 짧은 틈에 두 MR을 연달아 머지한 경우

develop이 갱신되면 열린 MR에 재빌드가 걸리지만 그 사이 10초 남짓은 낡은 초록불이
그대로 보이고 머지 버튼도 열려 있습니다. 그 틈에 두 번째를 머지하면 develop에
검증되지 않은 조합이 들어갑니다.

**그래도 검증 없이 배포되지는 않습니다.** develop 빌드가 그 조합을 빌드하고
테스트하고 기동까지 확인한 뒤에 교체합니다. 잃는 것은 안전이 아니라 **원인
귀속**입니다. 조합이 깨졌을 때 어느 쪽 탓인지 로그만으로는 모릅니다.

1번으로 서비스가 살아 있는지 먼저 봅니다. 살아 있으면 급하지 않고 **되돌릴 필요도
없습니다.** develop만 빨간불이니 원인을 찾아 고쳐서 새 MR을 올리면 됩니다. 머지된
것을 되돌리는 것보다 이게 깔끔합니다.

원인을 가릴 때는 두 머지가 각각 빌드를 트리거했는지 봅니다. 보통 순서대로 돌아서
첫 번째 단독 결과가 남습니다. 체크아웃 타이밍에 따라 첫 빌드가 이미 둘 다
가져갔으면 단독 결과가 없고, 그때는 로컬에서 각각 확인하는 편이 빠릅니다.

서비스가 내려갔으면 2번으로 갑니다. 되돌린 뒤 **다시 넣을 때 함정**이 있습니다.
revert한 브랜치를 그대로 다시 올리면 원래 변경이 들어가지 않습니다. Git이 이미
머지된 커밋으로 보기 때문입니다. 되돌리기를 되돌려야 합니다.

```
git checkout -b feature/backend/xxx-retry origin/develop
git revert <revert-커밋SHA>
```

애초에 밟지 않는 법은 간단합니다. 앞 MR을 머지한 뒤 **다음 MR 화면에서 머지 버튼이
잠긴 것을 눈으로 보고** 누르면 됩니다. 새로고침 한 번입니다. 마이그레이션이 든
MR일 때만 이 습관을 의식적으로 지키면 충분합니다.

## 검증된 것과 가정인 것

실제로 확인한 것과 문서만 보고 믿는 것을 구분해 둡니다. 사고가 났을 때
어디를 의심할지가 달라집니다.

확인된 동작입니다.

- **비백엔드 MR이 머지 차단에 걸리지 않습니다.** `backend/` 변경이 없는 MR은
  대상 확인에서 성공으로 보고하고 즉시 끝납니다. 머지 차단을 켠 뒤 Unity MR이
  실제로 통과해 머지됐습니다.
- **MR 빌드는 운영을 건드리지 않습니다.** 배포 관련 네 단계가 모두 건너뛰어지고,
  일회용 MySQL과 검증 태그 이미지가 빌드 후 전부 정리됩니다. 로그에 비밀번호가
  남지 않는 것도 확인했습니다.
- **기동 검증이 실패하면 배포 단계가 실행되지 않습니다.** 임시 컨테이너의
  DB_HOST를 잘못된 값으로 두고 돌려서 확인했습니다. 운영 컨테이너와 Flyway
  이력이 그대로였습니다.
- **낡은 브랜치는 배포할 수 있습니다.** 이름 필터가 없던 동안 문서 브랜치가
  옛 파이프라인으로 운영에 배포했습니다. 소스가 같아 이미지가 동일해서 교체는
  일어나지 않았지만, 달랐다면 운영이 롤백됐을 것입니다.
- **대상 브랜치가 움직여도 MR은 자동 재빌드되지 않습니다.** MR 빌드 이후
  develop이 갱신됐는데 그 MR은 빌드가 하나뿐이었습니다. 그래서 develop 빌드가
  직접 재빌드를 요청합니다.
- **그 재검증 요청이 실제로 동작합니다.** develop 빌드가 열린 MR을 찾아 재빌드를
  걸고, 해당 MR Job에 빌드가 하나 더 생기는 것을 확인했습니다. 지연은 develop
  빌드 시작에서 MR 재빌드 시작까지 **10초**입니다. 재검증을 `post success`에
  뒀을 때 같은 MR로 재보니 **70초**였습니다.
- **동시에 도는 MR 빌드가 서로를 건드리지 않습니다.** 자원 이름을 `BUILD_TAG`로
  나눈 뒤 MR 두 개를 동시에 빌드해 둘 다 통과했습니다. `BUILD_NUMBER`를 쓰던
  때는 실제로 충돌했습니다 — 한쪽은 네트워크 생성에 실패하고 다른 쪽은 정리
  단계에 컨테이너를 빼앗겼습니다.
- **워크스페이스에 남은 파일이 다음 빌드의 배포를 삼켰습니다.** 판별 결과를 파일로
  주고받던 때, 변경 0개였던 빌드가 만든 표시 파일이 지워지지 않아 다음 빌드가
  **초록불로 끝나면서 배포를 건너뛰었습니다.** 워크스페이스는 빌드 사이에
  재사용되므로 남은 파일이 그대로 다음 빌드에 영향을 줍니다. 지금은 판별에
  파일을 쓰지 않고 표준 출력으로 같은 스테이지 안에서 처리합니다.

아직 가정인 것입니다.

- **머지 차단이 파이프라인 없는 MR을 막는지.** `main` 대상 MR은 머지 결과에
  `backend/Jenkinsfile`이 없어 Job이 생기지 않습니다. 그 상태를 실제로 시험하지
  않았습니다. `develop → release → main` 순서를 지키면 `release`에 `backend/`가
  들어온 뒤이므로 문제가 없다고 보고 넘어갔습니다.
- **이미지 이름표로 되돌리는 절차를 실제로 해본 적은 없습니다.** 태그는 배포마다
  남고 있지만, 그것으로 서비스를 이전 버전으로 되돌리는 것을 시험하지 않았습니다.
  운영이 살아 있는 상태에서 일부러 되돌려볼 만한 가치가 있습니다.
- **Jenkins Replay로 이전 커밋을 재배포할 수 있는지.** 대상 확인이
  `GIT_PREVIOUS_SUCCESSFUL_COMMIT..HEAD`를 보므로 변경 0개로 판정해 전부 건너뛸
  것으로 봅니다. 확인하지 않았습니다.

## 아직 안 한 것

### 자동 롤백

**자동**으로는 넣지 않았습니다. 이미지를 이전 태그로 되돌리는 방식은
**마이그레이션 실패를 구제하지 못합니다** — DB가 되돌아가지 않기 때문입니다. 가장
위험한 실패 유형을 막지 못하면서 "롤백이 있으니 괜찮다"는 착각을 주는 쪽이 더
위험하다고 판단했습니다. 대신 기동 검증 단계로 **애초에 교체하지 않는** 방향을
택했습니다.

손으로 쓸 수단은 마련해 뒀습니다. 배포마다 이미지에 이름표를 남기므로 마이그레이션이
없던 실패는 몇 초에 되돌릴 수 있습니다(2번 참고). 실패의 대부분이 여기 속합니다.
자동으로 걸지 않는 것은 **마이그레이션이 섞였는지를 파이프라인이 판단할 수 없기**
때문입니다. 그 판단은 사람이 해야 합니다.

### 아티팩트 승격 (레지스트리)

상용 관행은 CI가 이미지를 한 번 빌드해 레지스트리에 커밋 SHA로 push하고, 배포는
같은 digest를 pull하는 것입니다. 재빌드가 사라지고 롤백이 수 초가 됩니다.
**SSAFY GitLab에 Container Registry가 없어서** 막혔습니다. `registry.lab.ssafy.com`이
DNS에 없고 `/jwt/auth`가 404입니다. 지금은 Docker 레이어 캐시로 재빌드 비용을
흡수하고 있습니다.

### 무중단 배포 (블루-그린)

교체 순간의 수 초 공백을 없애려면 새 컨테이너를 다른 이름으로 띄우고 nginx
upstream을 바꿔치는 구조가 필요합니다. 트래픽이 없는 지금은 값이 작아 미뤘습니다.

### 머지 큐

GitLab의 Merge trains는 Premium 기능이라 쓸 수 없습니다. 대신 두 장치로 근사합니다.
MR을 **머지 결과로 빌드**하고, **develop이 갱신되면 열린 MR에 재빌드를 요청**합니다.
남는 틈은 develop 갱신과 재빌드 **시작** 사이입니다. 재빌드가 시작되면 `pending`이
게시되어 머지가 잠깁니다. **실측 10초입니다** — develop 빌드 시작에서 MR 재빌드
시작까지. 재검증을 `post success`에 뒀을 때는 70초였습니다. 그 틈에 머지했을 때의
대응은 "배포가 깨졌을 때 5번"에 있습니다.

### 강제 빌드 스위치

대상 확인이 `backend/` 변경 0개로 판정하면 배포까지 건너뜁니다. 그래서 **같은 커밋을
다시 배포하는 수단이 없습니다.** Jenkins Replay도 같은 판정에 막힙니다. `FORCE`
파라미터로 판별을 무시하는 스위치를 넣으면 해결되지만, 이미지 이름표로 되돌릴 수
있게 되어 급함이 줄었다고 보고 미뤘습니다.

### 빌드 인프라 분리

빌드와 테스트가 운영 서버와 같은 EC2에서 돕니다. 4 vCPU / 15GB이고 앱 CPU가
0.2% 수준이라 지금은 경합이 없습니다. Jenkins 실행기가 2개로 제한돼 있어 동시
빌드도 두 개까지입니다. 트래픽이 생기면 빌드용 인스턴스를 분리해야 합니다.
