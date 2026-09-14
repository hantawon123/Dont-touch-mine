# 빌드 중 기존 버전을 유지하는 테스트 호스트

기존 EC2 또는 다른 Linux x86_64 호스트에서 Python 3.11 이상과 Linux Dedicated Server 빌드로 실행한다. 추가 Python 패키지나 AWS 전용 API는 사용하지 않는다. 지금은 현재 버전의 방 한 개(플레이어 최대 6명)를 유지하고, 교체할 때에만 새 버전의 방을 추가한다. 총 게임 프로세스 상한은 2개다. 100~200 CCU용 여러 방 배정·자동 확장은 이 도구의 범위가 아니다.

## 동작

1. 기존 서버와 WebGL 파일을 그대로 둔 채 별도 디렉터리에 새 빌드를 만든다.
2. `stage`가 서버·WebGL 버전 일치와 필수 파일을 검사하고 SHA-256 목록을 기록한다. 공개한 릴리스 ID는 덮어쓸 수 없다.
3. `activate`는 교체 요청만 남긴다. 호스트가 새 서버를 시작하고 실제 인증·Photon 방 준비를 마쳐 `Ready`를 기록한 뒤에만 접속 대상을 바꾼다.
4. `/`로 새로 들어온 브라우저는 새 WebGL로 이동한다. 기존 탭은 버전별 주소와 기존 서버를 계속 사용한다. 진행 중인 경기 상태를 새 프로세스로 옮기지 않는다.
5. 이전 방은 방장 퇴장 등 기존 게임 종료 규칙에 따라 종료된다. 종료된 이전 버전은 다시 띄우지 않는다. 현재 버전의 방이 종료되면 약 3초 뒤부터 다시 실행을 시도한다. 클라이언트의 방 만들기는 준비 중인 서버를 최대 30초 기다린다.

후보가 제한 시간 안에 준비되지 않으면 기존 접속 대상을 유지한다. 두 자리가 모두 사용 중이면 기존 경기를 강제 종료하지 않고 자리가 생기기를 기다린다. 90초 기본 제한을 넘으면 요청이 실패하므로 기존 경기가 끝난 뒤 다시 요청한다. 활성화 후 발생한 게임 자체의 오류는 자동 롤백 대상이 아니며 아래 명령으로 이전 버전을 선택한다.

버전별 URL을 새로고침하면 같은 버전을 유지한다. **최신 버전으로 다시 테스트할 때는 주소창에 호스트의 `/` 주소를 새로 입력한다.** 예전 페이지에서 새 방을 만드는 동작은 이전 서버가 종료된 후 실패할 수 있다. 이전 WebGL 파일은 자동 삭제하지 않으며, 사용 중인 릴리스·상태·로그 폴더를 지우지 않는다.

## Unity 에디터에서 테스트

원본 프로젝트의 제품 버전과 검증 빌드 버전은 다를 수 있다. 예를 들어 에디터 `0.1.0`은 서버 `988-local-v1`의 방 목록을 볼 수 없다. 서버를 활성화한 후 다음 명령으로 **현재 준비된 릴리스**를 선택하고 Play 모드를 다시 시작한다. 지역은 서버와 같은 한국(`kr`)으로 선택한다.

```powershell
python Tools/network/server-flow/sync_editor.py . --origin http://localhost:4291
```

이 명령은 Git에서 제외된 `UserSettings/ServerFlowVersion.txt`에 버전만 기록한다. 에디터 Play 모드에서만 이 값을 사용하며 제품 버전·WebGL·네이티브 빌드는 바꾸지 않는다. 에디터 콘솔의 `[Network] Editor matchmaking`에 사용 버전과 지역이 나온다. 교체·롤백 후 다시 동기화하며, 제품 버전으로 복귀하려면 이 로컬 파일을 삭제한다. 실제로 호환되는 소스와 서버를 대상으로 사용한다. 임의의 구버전 번호를 지정해 호환성을 우회하지 않는다.

에디터와 WebGL은 같은 시험 방 자원을 사용한다. 이미 방이 하나 만들어져 있으면 새 방 대신 ‘게임 찾기’로 참가하거나 방장이 퇴장한 후 다시 만든다. 검사 도구의 `ready=true`는 초기화 완료를 뜻하며, 방에 사용자가 있는지까지 표시하는 값은 아니다.

## 빌드

저장소의 README와 CONTRIBUTING을 따르고 [격리 프로젝트 준비 절차](README.md)를 먼저 실행한다. 원본 Unity 프로젝트나 서비스 중인 출력 디렉터리에 빌드하지 않는다. 매 교체마다 새 `SERVER_FLOW_VERSION`과 새 출력 경로를 사용한다. Photon의 `AppVersion`에 이 값이 들어가므로 구버전 클라이언트가 신버전 서버에 섞이지 않는다.

```powershell
$env:SERVER_FLOW_VERSION = '988-rollout-v2'
$env:SERVER_FLOW_OUTPUT = '<new-linux-output>'
& '<Unity.exe>' -batchmode -nographics -quit -projectPath '<isolated-project>' -buildTarget Linux64 -standaloneBuildSubtarget Server -executeMethod ServerFlowBuild.BuildLinux -logFile '<linux-build.log>'
# Linux 빌드 성공과 프로세스 종료를 확인한 다음 순차 실행한다.
$env:WEBGL_OUTPUT = '<new-webgl-output>'
$env:WEBGL_FAST_BUILD = '1'
& '<Unity.exe>' -batchmode -nographics -quit -projectPath '<isolated-project>' -buildTarget WebGL -executeMethod ServerFlowBuild.BuildWebGL -logFile '<webgl-build.log>'
```

각 로그의 빌드 성공과 두 출력의 `version.txt` 일치를 확인한다. GUI Unity 실행 명령의 즉시 반환은 빌드 완료가 아니다. 빠른 WebGL 빌드는 기능 검증용이다. 출력 전체를 서버의 새 `incoming/<id>/web`, `incoming/<id>/server`에 복사한다. 네이티브 `.debug`, `*_BurstDebugInformation_DoNotShip` 폴더는 전달하지 않아도 된다. 계정 데이터나 전체 실행 로그를 빌드에 넣지 않는다.

## 호스트 준비와 실행

다음 구조로 복사한다. 릴리스와 슬롯 상태를 실행 중인 경로와 별도로 관리할 필요 없이 호스트 전체 루트 하나로 옮길 수 있다.

```text
/srv/d205-test/
  host.json
  tools/serve.py
  tools/releases.py
  tools/release_host.py
  incoming/<id>/web/...
  incoming/<id>/server/...
  runtime/releases/<id>/{web,server,release.json}
  runtime/state/slot-{0,1}/...
  runtime/{active,request,status}.json
  runtime/logs/...
```

`host.example.json`을 `host.json`으로 복사하고 `root`를 절대 경로로 바꾼다. `origin`은 브라우저가 여는 주소, `api_origin`은 기존 HTTPS 백엔드, `region`은 클라이언트와 같은 Photon 지역이다. 서버는 `-backendUrl` 인자로 백엔드를 받는다. 검증용 WebGL의 HTTP API는 현재 페이지의 origin을 사용하므로 게임 호스트 주소를 바꿔도 다시 빌드할 필요가 없다. 알림 WebSocket은 기존 백엔드에 직접 연결한다.

```bash
umask 077
python3 tools/releases.py /srv/d205-test/runtime stage release-001 \
  --web incoming/release-001/web --server incoming/release-001/server
python3 tools/release_host.py host.json
```

호스트를 실행한 상태에서 다른 터미널로 요청한다.

```bash
python3 tools/releases.py /srv/d205-test/runtime activate release-001
python3 tools/releases.py /srv/d205-test/runtime status
```

`outcome=activated`, `active=release-001`, 해당 프로세스의 `ready=true`를 확인한다. 준비 표시는 서버 초기화까지의 증거이며 전체 경기 성공을 뜻하지 않는다. 원시 Unity 로그에는 인증 정보가 포함될 수 있으므로 공유하지 않는다. `status`는 프로세스·방·준비 여부만 출력한다.

지속 실행에는 `release-host.service.example`의 사용자·경로를 맞춰 systemd로 실행한다. CPU 2 vCPU 상당·메모리 5 GiB는 호스트와 두 게임 프로세스 전체의 상한이며 수용량 측정값이 아니다. 호스트 전체의 20분 강제 종료 제한은 두지 않는다. 빈 방의 120초 종료는 게임에 유지되며 호스트가 현재 버전을 다시 준비한다. 서비스 자체를 재시작하거나 호스트를 종료하면 그 안의 게임도 종료되므로 **릴리스 교체에는 서비스 재시작 대신 `activate`를 사용한다.**

관리 API는 HTTP로 열지 않는다. 테스트 웹은 `127.0.0.1`에만 바인딩한다. 로컬에서 SSH 터널을 실행한 뒤 `http://localhost:4291/`로 접속한다. 같은 로컬 포트를 사용하는 이전 프리뷰는 먼저 종료한다.

```bash
ssh -N -L 127.0.0.1:4291:127.0.0.1:4291 -o ExitOnForwardFailure=yes \
  -o ServerAliveInterval=30 -o ServerAliveCountMax=3 -i <key> <user>@<host>
```

## 교체와 롤백

```bash
python3 tools/releases.py /srv/d205-test/runtime stage release-002 \
  --web incoming/release-002/web --server incoming/release-002/server
python3 tools/releases.py /srv/d205-test/runtime activate release-002
python3 tools/releases.py /srv/d205-test/runtime status
# 이상이 있으면 이전 버전을 다시 선택한다. 기존 프로세스가 살아 있으면 재사용한다.
python3 tools/releases.py /srv/d205-test/runtime activate release-001
```

롤백 역시 준비 확인 이후에 적용하며, 이미 새 버전에서 플레이 중인 연결을 강제로 끊지 않는다. 새 버전과 이전 버전의 백엔드 API·DB가 함께 호환돼야 한다. 이 절차는 DB 스키마를 변경하지 않는다.

## 다른 서버로 옮길 때

동일한 Linux x86_64 실행 환경에 도구·릴리스·슬롯 상태를 옮기고 `host.json`의 경로와 브라우저 origin, systemd 사용자·경로를 변경한다. 슬롯 상태는 실험 계정 정보를 포함하므로 비공개 권한으로 복사한다. 백엔드와 Photon 설정을 유지하면 게임 서버를 특정 EC2 주소에 묶는 코드 변경은 필요 없다. 현재 호스트의 경기를 다른 물리 서버로 실시간 이전하는 기능은 없으므로 기존 경기를 끝낸 뒤 이전 호스트를 종료한다.

공개 배포 시에는 HTTPS reverse proxy에서 Host를 보존하고 `/api/v1/`과 `/releases/`를 전달하도록 구성해야 한다. 이 테스트 HTTP 서버를 그대로 인터넷에 노출하지 않는다. 백엔드·DB 자체의 이전, 공개 도메인·TLS 구성, 여러 방 수용량 확장은 별도 작업이다.

## 검사

```powershell
python -m unittest discover -s Tools/network/server-flow -p 'test_release_host.py' -v
```

준비 전 교체 차단, 후보 실패·버전 충돌 시 기존 경로 보존, 기존 프로세스 유지와 종료 후 정책, 두 프로세스 상한, 롤백, 실제 HTTP의 버전별 파일 보존·비공개 경로 차단, 릴리스 무결성을 검사한다. 모형 프로세스 검사는 실제 EC2의 WebGL 접속·교체 검증과 구분한다.

2026-09-14 실제 실행 결과와 범위는 [교체 검증 기록](../../../docs/planning/server-release-host-988.md)에 있다.
