# 배포 구성

운영 서버 `j15d205.p.ssafy.io`(EC2, Ubuntu 24.04)의 구성 파일입니다.
서버에서 직접 편집하지 말고 이 파일들을 고쳐 올리세요. 서버에서만 고친 설정은
EC2가 날아가면 같이 사라집니다.

## 파일

| 파일 | 배치 위치 | 역할 |
| --- | --- | --- |
| `nginx/d205.conf` | `/etc/nginx/sites-available/d205` | 443에서 받아 앱과 Jenkins로 프록시 |
| `install-jenkins.sh` | (서버에서 실행) | Jenkins 설치, docker 그룹 등록 |
| `jenkins/override.conf` | `/etc/systemd/system/jenkins.service.d/` | Jenkins 포트·바인딩·프리픽스 |
| `verify.sh` | (서버에서 실행) | 배포 상태 한 번에 확인 |

파이프라인 정의는 이 디렉터리가 아니라 `../Jenkinsfile`에 있습니다.

## 구조

외부에서 닿을 수 있는 것은 22(SSH)와 nginx뿐입니다.

```
인터넷 ─┬─ :22  ────────────────▶ sshd
        ├─ :80  ──▶ nginx ──▶ 443 리다이렉트 + 인증서 갱신 챌린지
        └─ :443 ──▶ nginx ─┬─ /jenkins/ ─▶ 127.0.0.1:9090  Jenkins
                            └─ /         ─▶ 127.0.0.1:8080  앱 컨테이너
                                                              └▶ d205-mysql (포트 미공개)
```

## 적용 방법

PowerShell에서 파일을 올리고, 서버 명령은 한 줄로 실행합니다.
여러 줄 붙여넣기는 PowerShell에서 깨집니다.

nginx 설정:

```
scp backend/deploy/nginx/d205.conf d205:/tmp/d205.conf
ssh d205 'sudo install -o root -g root -m 644 /tmp/d205.conf /etc/nginx/sites-available/d205 && sudo ln -sfn /etc/nginx/sites-available/d205 /etc/nginx/sites-enabled/d205 && sudo rm -f /etc/nginx/sites-enabled/default && sudo nginx -t && sudo systemctl reload nginx'
```

`nginx -t`가 실패하면 `&&`가 끊겨 reload까지 가지 않으므로 기존 설정이 유지됩니다.

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
| Scan Triggers | Periodically if not otherwise run | `1 day` (웹훅이 주 트리거) |
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
  대상 확인             O                    O
  빌드                  O (compose)          -
  빌드 (검증)            -                   O (docker build, verify 태그)
  테스트                O                    O
  기동 검증 (운영 DB)    O                    -
  기동 검증 (일회용 DB)   -                   O
  배포                  O                    -
  헬스체크              O                    -
```

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

### 다운타임

정상 배포에도 교체 순간 **수 초의 다운타임**이 있습니다. 이 파이프라인이 없애는
것은 실패로 인한 장시간 다운타임이지 교체 순간의 공백이 아닙니다. 그걸 없애려면
nginx upstream을 바꿔치는 블루-그린이 필요합니다.

## 배포가 깨졌을 때

### 1. 서비스가 살아 있는지 먼저 확인

파이프라인 실패가 곧 서비스 다운은 아닙니다.

```
ssh d205 'bash /var/lib/jenkins/workspace/d205-backend_develop/backend/deploy/verify.sh'
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
