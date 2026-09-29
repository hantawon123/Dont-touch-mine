# 988 실제 게임 서버 흐름 검증

987의 작은 접속 장면과 달리 실제 게임 프로젝트·계정 인증·씬·명령을 사용한다. 이 폴더는 Assets 밖이므로 제품에 들어가지 않는다. `prepare.py`는 저장소 밖의 독립 프로젝트에만 검증 드라이버를 설치한다. 사용자 편집 중인 `Supermarket_copy` 씬은 복사하지 않는다. 처음 가져오기에 시간이 걸릴 수 있다.

```powershell
python Tools/network/server-flow/prepare.py . '<isolated-project>'
$env:SERVER_FLOW_OUTPUT = '<output>'
# Unity 작업은 순차 실행한다. 실행 중인 원본 프로젝트를 지정하지 않는다.
& '<Unity.exe>' -batchmode -nographics -quit -projectPath '<isolated-project>' -buildTarget Win64 -executeMethod ServerFlowBuild.Build -logFile '<build.log>'
```

서버와 클라이언트는 같은 실행 파일을 사용하지만 서버만 화면 없이 실행한다. 서버를 시작하고 Ready를 확인한 후 peer 1을, 그 접속이 확인된 후 peer 2~6을 실행한다. 각 클라이언트는 별도로 생성해 로컬에 보관한 기기 ID를 사용한다. 기기 ID와 인증 응답은 커밋·보고하지 않는다.

```text
ServerFlow988.exe -batchmode -nographics -gameServer -roomCode 988ABC -region kr -logFile <server.log>
ServerFlow988.exe -validationPeer 1 -deviceId <test-device-1> -screen-width 640 -screen-height 360 -screen-fullscreen 0 -logFile <client1.log>
ServerFlow988.exe -validationPeer 2 -roomCode 988ABC -deviceId <test-device-2> -screen-width 640 -screen-height 360 -screen-fullscreen 0 -logFile <client2.log>
```

peer 3~6도 각자의 번호와 기기 ID로 실행한다. 로그의 `CONNECTED role=Client` 이후 6인 경기 시작, peer 6의 경기 중 이탈, 첫 경기 결과, 남은 5인의 재경기, 방장 종료에 따른 전체 세션 종료를 검사한다. 일반 참가자는 설정·강퇴 권한이 없어야 하며 시작 RPC에는 서버의 거절 응답이 와야 한다. 제한 시간은 10분이며 실패 또는 증거 부족은 `FAIL`로 기록한다. 테스트 동작 중 화면이 보이지 않아도 클라이언트는 렌더링을 유지해야 하므로 `-nographics`를 주지 않는다. 같은 PC의 부하를 줄이기 위해 검증 드라이버는 화면을 640×360, 렌더링 목표를 30 FPS로 설정한다. 서버 시뮬레이션 주기와 경기 규칙은 변경하지 않는다.

로그를 `server.log`, `client1.log`~`client6.log`로 저장한 후 검사한다. 검증기는 계정·전송 로그나 임의의 오류 메시지를 증거 파일에 복사하지 않는다.

```powershell
python Tools/network/server-flow/verify.py '<logs-directory>' --output '<evidence.txt>'
```

서로 다른 PC에서 직접 확인할 절차는 [6인 수동 검증](manual-six-pc.md)에 있다.

## 수동 확인용 WebGL

같은 독립 프로젝트에서 다음 빌드를 실행한다. 네이티브와 WebGL은 같은 `SERVER_FLOW_VERSION`을 사용한다(생략 시 `988-local-v1`). 빠른 빌드는 기능 확인용이며 성능 판정에 사용하지 않는다.

```powershell
$env:WEBGL_OUTPUT = '<webgl-output>'
$env:WEBGL_FAST_BUILD = '1'
& '<Unity.exe>' -batchmode -nographics -quit -projectPath '<isolated-project>' -buildTarget WebGL -executeMethod ServerFlowBuild.BuildWebGL -logFile '<webgl-build.log>'
python Tools/network/server-flow/serve.py '<webgl-output>'
```

각 PC에서 빌드 폴더와 `serve.py`를 받아 Python 3으로 실행하고 `http://localhost:4291`을 연다. 서버 프로세스는 한 PC 또는 기존 EC2 한 곳에서만 실행한다. 플레이어끼리는 Photon을 통해 연결되므로 다른 PC에 로컬 HTTP 포트를 개방할 필요가 없다.

독립 프로젝트에만 적용되는 WebGL 설정은 HTTP API를 로컬 프리뷰를 거쳐 기존 HTTPS 백엔드로 전달한다. 프리뷰는 `127.0.0.1`에만 바인딩하고 대상 백엔드를 고정하며 계정·토큰·요청 본문을 로그에 저장하지 않는다. WebSocket은 기존 백엔드에 직접 연결한다. 제품 소스의 백엔드 주소와 운영 CORS 설정은 바뀌지 않는다. 확인 후 프리뷰 Python 프로세스를 종료한다.

실행 결과는 [작업 기록](../../../docs/planning/server-session-988.md)에 갱신한다. 이 도구의 존재나 컴파일 성공 자체는 게임 흐름 검증 통과를 뜻하지 않는다. 서로 다른 PC의 WebGL 6인 검증과 운영 서버 수용량 측정은 별도로 필요하다.

## 버전 교체 테스트 호스트

빌드 중 기존 서버 유지, 준비 확인 후 교체, 종료된 방 자동 재생성은 [테스트 호스트 실행 절차](release-host.md)를 사용한다. 아래 단발 EC2 실행 절차는 이전 수동 검증용이며, 새 테스트 호스트와 중복 실행하지 않는다.

## Linux / 기존 EC2 시험 실행

동일 버전 에디터의 Linux Dedicated Server Build Support를 설치한 뒤, 위 독립 프로젝트의 `Assets/Editor/ServerFlowBuild.cs`에 현재 `ValidationBuild.cs`를 복사한다. `SERVER_FLOW_OUTPUT`을 별도 Linux 출력 폴더로 지정하고 다음 진입점을 사용한다.

`ProjectSettings`의 Server 타깃에도 현재 SDK의 Fusion 컴파일 기호가 필요하다. 특히 `FUSION_WEAVER`가 없으면 Photon Voice/Fusion 어셈블리가 제외되고 네트워크 타입이 컴파일되지 않는다. 현재 저장소는 Standalone/WebGL과 같은 SDK 기호를 Server에도 기록한다. SDK 업그레이드 때 세 타깃의 설정을 함께 확인한다.

```powershell
& '<Unity.exe>' -batchmode -nographics -quit -projectPath '<isolated-project>' -buildTarget Linux64 -standaloneBuildSubtarget Server -executeMethod ServerFlowBuild.BuildLinux -logFile '<linux-build.log>'
```

출력 전체를 Linux x86_64 서버에 복사한다. `ServerFlow988.x86_64`만 복사하면 데이터와 네이티브 라이브러리가 없어 실행되지 않는다. Mono Linux의 Dedicated Server 타깃과 `dedicatedServerOptimizations`를 사용해 그래픽 자산을 줄이며 `-batchmode -nographics -gameServer`로 시작한다. 물리 데이터 등 보존 범위는 [Unity 공식 최적화 설명](https://docs.unity3d.com/6000.0/Documentation/Manual/dedicated-server-optimizations.html)을 참고한다. 실제 게임의 경기·씬·물리 구성과 함께 실행 검증해야 한다.

기존 EC2의 검증 전용 경로는 `/home/ubuntu/d205-game-server-988/`이다. 다음 명령은 해당 경로에 Linux 빌드를 배치한 후 실행한다. 기존 백엔드·DB 서비스와 분리된 임시 systemd 서비스이며 부팅 시 자동 실행하지 않는다.

```bash
sudo systemd-run --unit=d205-game-988-trial --collect \
  -p User=ubuntu -p Group=ubuntu -p UMask=0077 \
  -p WorkingDirectory=/home/ubuntu/d205-game-server-988/linux-v2 \
  -p Environment=HOME=/home/ubuntu/d205-game-server-988/state \
  -p CPUQuota=150% -p MemoryMax=3G -p Nice=10 -p RuntimeMaxSec=1200 \
  /home/ubuntu/d205-game-server-988/linux-v2/ServerFlow988.x86_64 \
  -batchmode -nographics -gameServer -roomCode 988EC2 -region kr \
  -job-worker-count 1 -logFile /home/ubuntu/d205-game-server-988/logs/manual.log
```

CPU 상한은 1.5 vCPU이며 메모리 상한은 3 GiB이다. 상한은 안정적으로 운영 가능한 방 수의 측정 결과가 아니다. 서비스 최대 수명은 20분이며, 게임 자체의 빈 서버 120초 종료와 방장 퇴장 종료가 우선 적용된다. 재시작은 위 명령을 다시 실행한다. 명시적으로 종료할 때는 `sudo systemctl stop d205-game-988-trial.service`를 사용한다.

같은 `988-local-v1` 클라이언트로 접속하고 서버 로그의 `Server / IsServer=True`, 각 클라이언트의 `Client / IsServer=False`, 실제 경기 흐름과 종료를 확인한다. Photon을 통한 연결을 사용하며 이 시험을 위해 기존 웹 배포·Jenkins나 방화벽을 변경하지 않는다. 서버 HOME 아래의 실험 기기 ID와 전체 인증 로그는 외부 보고서에 넣지 않는다.


서버 빌드의 `DedicatedServerScenePreparation`은 임시 씬 복사본에서 UI 생성 컴포넌트와 UI 전용 Scope를 제거하고 Canvas를 비활성화한다. 게임용 Scope·물리 구성과 원본 씬은 유지한다. Canvas 안에 게임용 Scope나 물리 컴포넌트가 남아 있으면 빌드를 실패시킨다.

2026-09-14 검증된 배치는 `linux-v2`이며 이전 `linux-v1`은 사용하지 않는다. 기존 서비스가 종료된 상태에서 실행하고 로그 파일은 실행마다 다른 이름을 사용한다. EC2 로그를 수집한 뒤에는 다음 명령으로 서버 위치를 증거에 표시한다.

```powershell
python Tools/network/server-flow/verify.py '<logs-directory>' --server-location ec2 --output '<evidence.txt>'
```

[EC2 실제 검증 결과](../../../docs/planning/server-ec2-trial-988.md)에 6인 흐름, WebGL 접속, 자원 관측과 남은 문제를 기록했다. 별도 PC의 WebGL 6인 수동 검증은 아직 남아 있다.

## WebGL 포인터 잠금 회귀 확인

[오류 원인·실행 결과·수동 절차](../../../docs/planning/webgl-pointer-lock-988.md)를 따른다. `pointer-lock-check.js`는 검증 빌드의 index.html에서 Unity loader보다 먼저 삽입하는 진단 도구다. 제품 패키지에는 포함하지 않는다. 일반 창에서는 첫 Escape의 설정 열기와 클릭 후 조작 복귀를 확인한다. 게임 하단 버튼으로 진입한 전체화면에서는 키보드/마우스 권한을 허용한 뒤 Escape로 설정을 닫았을 때 추가 클릭 없이 조작이 복구되는지 확인한다. F11은 이 실험의 전체화면 진입 방법이 아니다. Escape를 약 2초 이상 누르면 전체화면에서 나올 수 있다.

프리뷰 페이지 HTTP 200만으로 접속 준비를 판단하지 않는다. `serve.py` 실행 환경에서 기존 HTTPS 백엔드 통신이 허용돼야 한다. 계정 발급 502 이후 Photon의 `Authentication type None not supported`가 나타나면 인증 중계 실패부터 확인한다. 승인된 실험용 계정 발급 성공을 확인하고 페이지를 새로고침한다. 인증을 끄거나 토큰을 로그에 남기지 않는다. 이미 방 하나에 할당된 시험 서버 한 개로는 새 방을 더 만들 수 없으므로 기존 방 찾기로 참가한다.

공통 브라우저 연동 계약은 다음 명령으로 확인한다. 격리된 DOM 모형 검사이므로 실제 Chrome 권한·시점·공격 검증을 대신하지 않는다.

```powershell
node Tools/network/server-flow/pointer-bridge.test.cjs Assets/_Game/Client/Common/WebTextInput.jslib
```