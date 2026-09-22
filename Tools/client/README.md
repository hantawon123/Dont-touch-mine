# Windows 클라이언트·Linux 게임 서버 배포

`release` 변경을 Jenkins `d205-unity-release`가 5분 간격으로 감지한다.
`Tools/client/Jenkinsfile`은 Windows 에이전트의 Windows x64 Mono 클라이언트와 EC2의 Linux 전용 서버를 같은 Git SHA로 병렬 빌드한다.
Linux 서버 빌드·배포는 `d205-unity-linux` 노드의 `unity-linux` 라벨을 사용한다. WebGL 작업과 `unity-webgl` 라벨은 사용하지 않는다. 기존 캐시·라이선스·설정 경로는 유지한다.

원격 `release`에 이 Jenkinsfile과 빌드 도구가 포함된 변경사항을 push 또는 병합하면 최대 약 5분 이내 SCM polling으로 실행한다. 로컬 커밋만으로는 실행되지 않으며, 어느 PC에서 push했는지와 관계없이 Windows 빌드는 등록된 개발 PC에서 수행한다. 개발 PC가 꺼져 있거나 절전 상태이면 Windows 단계가 대기하므로 전원·네트워크와 에이전트 연결을 유지한다. 현재 release에 Jenkinsfile이 없다면 CI 변경사항부터 release에 포함해야 한다.

## 팀 사용 흐름

1. 기능 MR을 develop에 병합하고 검증한 내용을 release로 병합한다.
2. Jenkins의 계약 테스트 → Windows 빌드 → Linux 서버 빌드 → ZIP 생성 → 서버 준비 확인을 기다린다.
3. `https://j15d205.p.ssafy.io/play/`에서 ZIP을 받고 모두 압축 해제한 뒤 `Don't Touch Mine.exe`를 실행한다.
4. 기존 파일은 자동 패치되지 않는다. 새 버전 공개 후에는 참가자 모두 최신 ZIP을 사용한다.

각 ZIP과 서버의 `version.txt`는 동일한 전체 커밋 SHA다. 이전 버전은 Photon 매칭 영역이 달라 최신 방에 입장하지 않는다.
`TEST_ONLY`는 네트워크 계약 테스트만 실행하며 빌드·배포하지 않는다.
release 이외의 브랜치를 검증용으로 연결하면 빌드·아티팩트 검증만 하고 공개 전환하지 않는다.

## 배포 전환과 한계

기존 `Tools/network/server-flow/release_host.py`를 그대로 사용한다.
`Builds/Download`의 HTML·ZIP·SHA256SUMS를 기존 release host의 `web` 디렉터리에 저장한다. 이는 다운로드용 정적 파일이며 WebGL 플레이어가 아니다.
빌드 중에는 기존 게임 서버를 유지한다. 새 서버가 준비된 뒤 `active.json`을 원자적으로 교체하므로 다운로드 버전도 함께 전환된다.
실패 시 기존 버전을 유지한다. 기존 경기 서버는 해당 방이 비고 종료될 때까지 유지된다.
호스트는 `warm_rooms`개의 빈 방을 준비하고 선점될 때 `room_capacity`까지 확장한다(방당 최대 6인). 배포 예제는 대기 2방·동시 최대 20방·이전 릴리스 포함 총 40프로세스다. 실제 EC2 적용은 배포 시 자원 한도를 확인한 뒤 수행한다. `max_processes`는 이전 버전과 후보 서버까지 포함한 전체 상한이다. 상한에 도달하면 기존 방을 강제 종료하지 않고 자리가 생기기를 기다린다. 한 장비 내 고정 슬롯 방식이며 여러 장비의 자동 증설은 포함하지 않는다. 상세 설정과 검증 범위는 [방 풀 운영 안내](../network/server-flow/release-host.md)를 따른다.

## 빌드 머신

- Windows: Unity 6000.3.22f1, Java 21, .NET SDK, Python 3.11+, Git LFS. `unity-windows` 라벨의 에이전트에서 네이티브 빌드한다.
- EC2: `unity-linux` 라벨, GameCI Linux 이미지, Docker, Python 3.11+, Git LFS.
- Linux 서버는 기존 Jenkins Unity 실행 슬롯을 사용한다. Unity 컨테이너당 CPU 3개·메모리 8GB 제한. Jenkins 에이전트 서비스는 Git 체크아웃을 포함하여 MemoryMax=2G를 사용한다(512MB에서는 신규 클론 중 OOM 종료 확인).
- Linux 서버 빌드는 `/tmp/d205-ec2-build.lock`을 사용한다. 같은 EC2에서 실행되는 백엔드 Docker·Gradle 단계와 겹치지 않아 Jenkins와 게임 서버의 메모리를 보호한다. Windows 빌드는 별도 PC에서 계속 병렬 실행된다.
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

`Tools/client/site/play.html`은 정적 소개·설치 페이지의 배포 템플릿이다. 별도 프런트엔드 빌드나 외부 로그인은 필요 없다.
패키징 시 ZIP 이름·크기, 압축 해제 크기, Git 버전, 한국 시간 기준 생성일을 넣는다.
완성된 페이지는 `Builds/Download/index.html`로 출력하며, 파비콘과 `site/`의 페이지 에셋
(`banner.jpg`, `custom.jpg`, `win.jpg`, `highlight.jpg`, 스텝별 `step-N.mp4`·`step-N-poster.jpg`)을
함께 복사한다. 에셋이 하나라도 없으면 패키징이 실패한다. 페이지에 새 이미지나 영상을 추가하면
`package_release.py`의 `assets` 목록에도 넣어야 한다.
게임 다운로드 버튼은 같은 릴리스의 ZIP을 상대 경로로 연결한다. `@@ARCHIVE@@` 등의 값은
`package_release.py`가 채우므로 원본 `play.html`을 그대로 서버에 업로드하지 않는다.
검증된 서버와 같은 릴리스의 페이지·ZIP이 함께 공개된다. `/download/`도 호환 경로로 유지한다.

최초 `/play/` 공개 전에는 실제 서버 Ready와 다운로드 검증을 완료해야 한다.
기존 Nginx `d205-webgl*.conf`의 `/play` location과 유지보수 차단을 백업한 뒤
활성 include 밖으로 옮기고 `nginx-download.conf`를 설치한다. 중복 location을 두지 않는다.
`nginx -t` 성공 후 reload하고 `/play/`의 리다이렉트가 `/play/releases/<SHA>/web/`로
향하는지, 이미지와 ZIP이 같은 릴리스로 응답하는지 확인한다. WebGL 플레이어는 재개하지 않는다.

## 빌드 캐시와 중단 처리

Windows는 기존 작업 디렉터리의 `Library`와 `Library/ClientCiCache/bee`를 재사용한다.
Linux 서버는 작업 디렉터리 옆 `<workspace>@server-library`를 별도 마운트하고
`Library/ClientCiCache/bee-server`를 사용한다. 두 타깃의 Library 전환으로 발생하는 재임포트를 피한다.
Linux의 첫 실행은 새 캐시를 만들기 때문에 빨라지지 않으며 디스크 공간이 추가로 필요하다.
캐시는 빌드 중단 시에도 삭제하지 않는다. 첫 Windows 셰이더 컴파일 시간은 별도 병목이다.

`container.sh`는 컨테이너에 작업 디렉터리별 소유 라벨을 붙인다.
정상 종료·TERM/INT에는 해당 컨테이너를 정리하고, Jenkins post에서도 같은 작업의 잔여 컨테이너만 정리한다.
게임 서버·백엔드 컨테이너를 전체 종료하거나 prune하지 않는다.
구성 검증: `bash Tools/client/test_build.sh`.

## Windows 에이전트

`d205-unity-windows`는 현재 개발 PC의 빌드 전용 에이전트이며 동시 실행은 1개다.
루트는 `%USERPROFILE%\.d205-unity-ci\agent`, Photon 설정은 `..\config\PhotonAppSettings.asset`다.
`CLIENT_UNITY_EXE`와 `CLIENT_CONFIG_DIR`로 설치 경로를 바꿀 수 있다.
라이선스는 Windows PC에 정상 활성화된 Unity 라이선스를 사용하며 EC2 라이선스를 복사하지 않는다.
현재 PC에서 `powershell -NoProfile -ExecutionPolicy Bypass -File "$env:USERPROFILE/.d205-unity-ci/start-agent.ps1"`로 연결한다.
현재 PC에는 `D205 Unity Jenkins Agent` 예약 작업을 등록해 사용자 로그인 시 자동 연결한다. 작업은 현재 사용자 권한으로 실행하며 서비스는 설치하지 않는다. 이 PC가 꺼지거나 절전/로그아웃되면 Windows 빌드를 수행할 수 없다.
기존 에이전트가 연결 중이면 중복 실행하지 않는다. 연결 인증 파일은 Git에 넣거나 팀에 공유하지 않는다.

Linux 체크아웃에서 확정한 전체 SHA를 Windows도 체크아웃한다. Windows 테스트 실패 시 서버 빌드도 중단한다.
성공한 Windows 압축 파일만 stash로 전달하고 SHA·체크섬을 재검사한다. 대형 압축 파일 전달은
Jenkins 컨트롤러 I/O 비용이 있으므로 전송 시간이 병목이 되면 외부 아티팩트 저장소로 전환한다.
TEST_ONLY는 Windows 계약 테스트만 수행하고 Linux 서버 빌드/배포는 하지 않는다.
첫 Windows 작업 폴더는 전체 에셋을 임포트해야 한다. 이후 이 폴더의 Library를 재사용한다.
Windows 종료 정리는 PID와 시작 시간·실행 경로가 일치하는 해당 빌드 프로세스 트리만 대상으로 한다.
PowerShell 검증: `powershell -NoProfile -ExecutionPolicy Bypass -File Tools/client/test-windows-build.ps1`.
