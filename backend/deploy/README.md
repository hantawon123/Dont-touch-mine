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
