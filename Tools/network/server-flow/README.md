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

같은 독립 프로젝트에서 다음 빌드를 실행한다. 네이티브와 WebGL의 버전은 모두 `988-local-v1`이다. 빠른 빌드는 기능 확인용이며 성능 판정에 사용하지 않는다.

```powershell
$env:WEBGL_OUTPUT = '<webgl-output>'
$env:WEBGL_FAST_BUILD = '1'
& '<Unity.exe>' -batchmode -nographics -quit -projectPath '<isolated-project>' -buildTarget WebGL -executeMethod ServerFlowBuild.BuildWebGL -logFile '<webgl-build.log>'
python Tools/network/server-flow/serve.py '<webgl-output>'
```

각 PC에서 빌드 폴더와 `serve.py`를 받아 Python 3으로 실행하고 `http://localhost:4291`을 연다. 서버 프로세스는 한 PC에서만 실행한다. 플레이어끼리는 Photon을 통해 연결되므로 다른 PC에 로컬 HTTP 포트를 개방할 필요가 없다.

독립 프로젝트에만 적용되는 WebGL 설정은 HTTP API를 로컬 프리뷰를 거쳐 기존 HTTPS 백엔드로 전달한다. 프리뷰는 `127.0.0.1`에만 바인딩하고 대상 백엔드를 고정하며 계정·토큰·요청 본문을 로그에 저장하지 않는다. WebSocket은 기존 백엔드에 직접 연결한다. 제품 소스의 백엔드 주소와 운영 CORS 설정은 바뀌지 않는다. 확인 후 프리뷰 Python 프로세스를 종료한다.

실행 결과는 [작업 기록](../../../docs/planning/server-session-988.md)에 갱신한다. 이 도구의 존재나 컴파일 성공 자체는 게임 흐름 검증 통과를 뜻하지 않는다. 서로 다른 PC의 WebGL 6인 검증과 운영 서버 수용량 측정은 별도로 필요하다.
