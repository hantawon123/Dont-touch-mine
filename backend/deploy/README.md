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

Job 이름 `d205-backend-deploy`, 종류 Pipeline.

| 위치 | 항목 | 값 |
| --- | --- | --- |
| Pipeline | Definition | `Pipeline script from SCM` |
| Pipeline | SCM | `Git` |
| Pipeline | Repository URL | `https://lab.ssafy.com/s15-metaverse-game-sub1/S15P21D205.git` |
| Pipeline | Credentials | `gitlab-deploy-token` |
| Pipeline | Branch Specifier | `*/develop` |
| Pipeline | Script Path | `backend/Jenkinsfile` |
| Pipeline | Additional Behaviours | `Polling ignores commits in certain paths` |
| Pipeline | └ Included Regions | `backend/.*` |

`Included Regions`가 핵심입니다. 이 저장소는 Unity 작업이 대부분이라, 없으면
백엔드와 무관한 커밋마다 Gradle 빌드와 컨테이너 교체가 일어납니다.
폴링에만 적용되므로 "지금 빌드"는 경로와 무관하게 항상 동작합니다 —
필요할 때 강제 배포 수단이 됩니다.

### 크리덴셜

| ID | 종류 | 내용 |
| --- | --- | --- |
| `gitlab-deploy-token` | Username with password | GitLab Deploy Token (`read_repository`). 발급은 Maintainer 권한 필요 |
| `d205-backend-env` | Secret file | prod `.env`. 원본은 서버의 `/home/ubuntu/d205/.env` |

`d205-backend-env`는 `Jenkinsfile`이 코드에서 직접 참조하므로 ID를 바꾸면 빌드가 깨집니다.

### 트리거

폴링(2분 간격)입니다. 웹훅을 쓰지 않는 이유는 GitLab 웹훅 등록에 Maintainer
권한이 필요하고, Jenkins에 인바운드 엔드포인트를 여는 대가가 따르기 때문입니다.
Jenkins는 docker 소켓 권한을 가지므로 노출면을 늘리지 않는 편이 낫습니다.

## 배포 파이프라인

`../Jenkinsfile`이 정의합니다. 네 단계이고, **살아 있는 컨테이너를 교체하기 전에
새 이미지가 실제로 기동하는지 먼저 확인**합니다.

```
1. 빌드        이미지 빌드, mysql 확인
2. 기동 검증    새 이미지를 임시 컨테이너로 띄워 UP 확인 후 제거
3. 배포        docker compose up -d 로 실제 교체
4. 헬스체크     /actuator/health 폴링
```

2단계가 핵심입니다. `docker compose up`은 **컨테이너를 먼저 교체하고 앱은 그 뒤에**
뜹니다. 곧바로 배포하면 마이그레이션 오류나 설정 오류가 그대로 서비스 다운이 됩니다.
임시 컨테이너를 먼저 띄워보면 그런 실패가 살아 있는 서비스를 건드리기 전에 드러납니다.

임시 컨테이너는 `127.0.0.1:18080`에 붙고 실제 DB를 씁니다. **여기서 Flyway가 돌아
마이그레이션이 실제로 적용됩니다.** 같은 DB에 두 앱이 20초쯤 함께 붙지만 Flyway가
DB 락을 잡으므로 이중 적용은 없습니다.

주의할 점이 하나 있습니다. 컬럼 추가처럼 덧붙이는 마이그레이션은 안전하지만,
`DROP COLUMN` 같은 파괴적 변경은 그 20초 동안 **아직 살아 있는 이전 버전이 에러를
낼 수 있습니다.** 파괴적 변경은 두 배포로 나누세요. 먼저 코드에서 그 컬럼 사용을
없애 배포하고, 그다음 배포에서 컬럼을 지웁니다.

정상 배포에도 3단계에서 **수 초의 다운타임**이 있습니다. 이 파이프라인이 없애는 것은
실패로 인한 장시간 다운타임이지, 교체 순간의 공백이 아닙니다. 그걸 없애려면 nginx
upstream을 바꿔치는 블루-그린이 필요합니다.

## 배포가 깨졌을 때

### 1. 서비스가 살아 있는지 먼저 확인

파이프라인 실패가 곧 서비스 다운은 아닙니다.

```
ssh d205 'bash /var/lib/jenkins/workspace/d205-backend-deploy/backend/deploy/verify.sh'
```

| 실패 단계 | 서비스 | 긴급도 |
| --- | --- | --- |
| 빌드 | 정상 | 낮음. 고쳐서 다시 푸시 |
| 기동 검증 | 정상 — 교체 전에 멈춤 | 낮음 |
| 배포 / 헬스체크 | 내려갔을 수 있음 | 높음 |

### 2. 서비스가 내려갔으면 되돌리기

정석은 revert입니다. GitLab의 머지된 MR 페이지에 `Revert` 버튼이 있고, 누르면
되돌리는 MR이 생성됩니다. 머지하면 폴링이 2분 안에 이전 상태로 재배포합니다.

명령으로 하면 이렇습니다. `-m 1`은 "머지 커밋의 첫 번째 부모 쪽으로 되돌린다"는
뜻이고 머지 커밋을 revert할 때 필수입니다.

```
git fetch origin
git checkout -b revert/broken-deploy origin/develop
git revert -m 1 <머지커밋SHA>
```

승인을 기다릴 수 없으면 Job의 Branch Specifier를 **마지막 성공 커밋 SHA**로 바꿔
빌드해 서비스를 먼저 살립니다. 단 `develop`은 여전히 깨진 상태이므로 반드시 revert를
이어서 해야 합니다. 안 하면 다음 폴링이 깨진 커밋을 다시 배포합니다.

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
강력합니다.

## 아직 안 한 것

### MR 단계 빌드 (pre-merge CI)

지금은 **머지된 뒤에만** 빌드합니다. 그래서 두 가지 한계가 있습니다.

- **의미 충돌을 못 잡습니다.** 각각은 깨끗하게 머지되는데 합치면 깨지는 경우
  (한쪽이 메서드 이름을 바꾸고 다른 쪽이 그 메서드를 호출하는 코드를 추가)를
  GitLab도 이 파이프라인도 잡지 못합니다.
- **원인 귀속이 약합니다.** 빌드 중에 머지가 여러 개 들어오면 대기열에서 하나로
  합쳐져 다음 빌드가 최신 HEAD를 빌드합니다. 실패하면 후보가 여럿입니다.
  빌드 페이지의 Changes로 후보는 알 수 있고, Branch Specifier를 중간 커밋으로
  놓고 이등분하면 몇 분 안에 좁혀집니다.

해결책은 MR마다 빌드하는 것인데 **웹훅 등록과 머지 차단 설정에 Maintainer 권한이
필요하고**, 배포 파이프라인과 검증 파이프라인을 분리해야 합니다. 지금은 백엔드
작업자가 사실상 한 명이라 두 한계가 실제 비용이 되는 빈도가 낮아 미뤘습니다.
**백엔드에 두 명 이상 붙으면 그때가 도입 시점입니다.**

### 자동 롤백

넣지 않았습니다. 이미지를 이전 태그로 되돌리는 방식은 **마이그레이션 실패를 구제하지
못합니다** — DB가 되돌아가지 않기 때문입니다. 가장 위험한 실패 유형을 막지 못하면서
"롤백이 있으니 괜찮다"는 착각을 주는 쪽이 더 위험하다고 판단했습니다.
대신 기동 검증 단계로 **애초에 교체하지 않는** 방향을 택했습니다.
