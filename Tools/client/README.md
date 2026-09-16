# Windows 클라이언트·Linux 게임 서버 배포

`release` 변경을 Jenkins `d205-unity-release`가 5분 간격으로 감지한다.
`Tools/client/Jenkinsfile`은 Windows x64 Mono 클라이언트와 Linux 전용 서버를 같은 Git SHA로 빌드한다.
기존 `d205-unity-webgl` 작업은 비활성 상태를 유지한다. 실행 노드의 `unity-webgl`은 기존 슬롯 이름일 뿐 WebGL을 빌드하지 않는다.

## 팀 사용 흐름

1. 기능 MR을 develop에 병합하고 검증한 내용을 release로 병합한다.
2. Jenkins의 계약 테스트 → Windows 빌드 → Linux 서버 빌드 → ZIP 생성 → 서버 준비 확인을 기다린다.
3. `https://j15d205.p.ssafy.io/play/`에서 ZIP을 받고 모두 압축 해제한 뒤 `Game.exe`를 실행한다.
4. 기존 파일은 자동 패치되지 않는다. 새 버전 공개 후에는 참가자 모두 최신 ZIP을 사용한다.

각 ZIP과 서버의 `version.txt`는 동일한 전체 커밋 SHA다. 이전 버전은 Photon 매칭 영역이 달라 최신 방에 입장하지 않는다.
`TEST_ONLY`는 네트워크 계약 테스트만 실행하며 빌드·배포하지 않는다.
release 이외의 브랜치를 검증용으로 연결하면 빌드·아티팩트 검증만 하고 공개 전환하지 않는다.

## 배포 전환과 한계

기존 `Tools/network/server-flow/release_host.py`를 그대로 사용한다.
`Builds/Download`의 HTML·ZIP·SHA256SUMS를 기존 release host의 `web` 디렉터리에 저장한다. 이는 다운로드용 정적 파일이며 WebGL 플레이어가 아니다.
빌드 중에는 기존 게임 서버를 유지한다. 새 서버가 준비된 뒤 `active.json`을 원자적으로 교체하므로 다운로드 버전도 함께 전환된다.
실패 시 기존 버전을 유지한다. 기존 경기 서버는 해당 방이 비고 종료될 때까지 유지된다.
호스트의 `rooms_per_release`만큼 독립 방을 준비한다(방당 최대 6인). 기존 설정은 1방, 예제 설정은 2방/총 4프로세스다. `max_processes`는 이전 버전과 후보 서버까지 포함한 전체 상한이다. 상한에 도달하면 기존 방을 강제 종료하지 않고 자리가 생기기를 기다린다. 한 장비 내 고정 슬롯 방식이며 여러 장비의 자동 증설은 포함하지 않는다. 상세 설정과 검증 범위는 [방 풀 운영 안내](../network/server-flow/release-host.md)를 따른다.

## 빌드 머신

- Unity 6000.3.22f1 GameCI Windows Mono / Linux 이미지, Docker, Python 3.11+, Git LFS.
- 기존 Jenkins Unity 실행 슬롯을 재사용하며 두 타깃을 순차 실행한다. Unity 컨테이너당 CPU 3개·메모리 8GB 제한. Jenkins 에이전트 서비스는 Git 체크아웃을 포함하여 MemoryMax=2G를 사용한다(512MB에서는 신규 클론 중 OOM 종료 확인).
- `CLIENT_CONFIG_DIR` 기본 `/var/lib/jenkins/.config/unity-webgl`의 `PhotonAppSettings.asset`를 주입한다.
- `CLIENT_UNITY_HOME` 기본 `/var/lib/jenkins/.config/unity3d/Unity`의 해당 EC2에서 정상 활성화한 Unity 라이선스를 사용한다. 다른 PC의 machine-id/라이선스를 복사하지 않는다.
- GMS 키는 공용 백엔드에만 둔다. ZIP에 소스·백업·환경변수 파일을 포함하지 않는다.
- 캐시는 `Library/ClientCiCache`, 로그는 `Logs/client-*`와 `Logs/server-build.log`다.
- Jenkins 빌드 로그 15개/아티팩트 3개를 보관한다. 서비스 릴리스는 현재·접속 중 버전을 자동 삭제하지 않는다. 디스크가 부족하면 사용하지 않는 과거 버전을 확인 후 정리한다.

## 호스트 이동과 복구

기존 [release host 설치 절차](../network/server-flow/release-host.md)를 따른다. 현재 호스트 루트는 `/var/www/d205-game/runtime`, HTTP는 loopback 4292, path_prefix는 `/download`다.
Nginx [다운로드 설정](nginx-download.conf)을 `/etc/nginx/snippets/d205-client-download.conf`로 설치하고 `nginx -t` 성공 후 reload한다. `backend/deploy/nginx/d205.conf`의 `d205-client*.conf` include가 이를 읽는다. `/download/`와 `/play/`를 해당 loopback으로 프록시한다. 기존 `/play/` 설정 교체는 아래 최초 공개 절차를 따른다. API와 Jenkins 경로는 기존 설정을 유지한다.
새 서버에서는 유효한 Unity 라이선스를 다시 준비하고 설정 경로·도메인·서비스 계정을 맞춘다.

```bash
python3 Tools/network/server-flow/releases.py /var/www/d205-game/runtime status
python3 Tools/network/server-flow/releases.py /var/www/d205-game/runtime activate <이전_전체_SHA>
```

롤백도 해당 버전 서버 준비 후 전환된다. status의 `outcome=activated`, `active`와 `/download/current.json`의 revision을 확인한다.
실행 중인 방을 보존하려면 배포 때 release host 자체를 재시작하지 않는다.

## 로컬 검증

```bash
python3 -m unittest discover -s Tools/client -p 'test_*.py'
python3 -m unittest discover -s Tools/network/server-flow -p 'test_*.py'
```

## 게임 소개·다운로드 페이지

`Tools/client/site/index.html`은 정적 소개·설치 페이지다. 별도 프런트엔드 빌드나 외부 로그인은 필요 없다.
패키징 시 ZIP 이름·크기, 압축 해제 크기, Git 버전, 한국 시간 기준 생성일을 넣고
저장소의 `docs/design/concept/main-screen-concept.png`를 `hero.png`로 복사한다.
검증된 서버와 같은 릴리스의 페이지·ZIP이 함께 공개된다. `/download/`도 호환 경로로 유지한다.

최초 `/play/` 공개 전에는 실제 서버 Ready와 다운로드 검증을 완료해야 한다.
기존 Nginx `d205-webgl*.conf`의 `/play` location과 유지보수 차단을 백업한 뒤
활성 include 밖으로 옮기고 `nginx-download.conf`를 설치한다. 중복 location을 두지 않는다.
`nginx -t` 성공 후 reload하고 `/play/`의 리다이렉트가 `/play/releases/<SHA>/web/`로
향하는지, 이미지와 ZIP이 같은 릴리스로 응답하는지 확인한다. WebGL 플레이어는 재개하지 않는다.
