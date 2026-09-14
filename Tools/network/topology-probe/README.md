# 987: 네이티브 Server ↔ WebGL 최소 접속 실험

제품 변경 없이 설치된 Fusion 2.1.2.2279의 토폴로지를 검증하는 독립 Unity 실험이다. 이 폴더는 `Assets` 밖이므로 게임에 컴파일되지 않는다. 원본 SDK 파일은 수정하지 않으며 복사본의 프로젝트 설정만 바꾼다. 제품 전환 판단은 [결정 문서](../../../docs/planning/server-topology-987.md)에 있다.

## 검증 범위

- 네이티브 `GameMode.Server`에 로컬 플레이어가 없고 브라우저는 Client로 연결됨.
- 서버 권한의 tick·인원·요청 수·Rigidbody 높이가 WebGL로 복제됨.
- Client의 RPC 요청이 서버에서 처리됨.
- 클라이언트가 보낸 임의 nonce가 그 송신자에게만 Reliable Data로 돌아옴. 다른 클라이언트 nonce 수신은 실패.
- 두 브라우저 중 하나가 종료되어도 나머지 Client의 서버 tick이 계속 진행됨.
- 동일 AppVersion 접속, 다른 AppVersion/없는 방 접속 거절, 정상 Shutdown.

화면의 `PASS`는 한 클라이언트에서 tick 128회 이상 진행·RPC 처리·자기 nonce 회신·잘못된 회신 0·바닥에 정착한 물리 높이를 확인했다는 뜻이다. 2인 동시 접속과 이탈 후 유지, 실패 시나리오는 로그를 별도로 대조한다. `STOPPED` 자체는 테스트 통과를 의미하지 않는다.

게임의 전체 로비·캐릭터·물건·맵·하이라이트 구현은 이 실험에 포함하지 않는다. 성능 벤치마크도 아니며, 제품 테스트를 대체하지 않는다. 987에서 조사한 제품의 서버 전환 작업은 988 이후에 적용한다.

## 준비

Unity 6000.3.22f1과 Windows/WebGL 빌드 모듈, 원본의 Photon 설정, 정상 계정 API와 Photon 서비스가 필요하다. Photon App ID·토큰·기기 식별자는 보고서에 출력하지 않는다. 기존 계정 발급 API와 Photon Custom Auth를 그대로 거치며 익명 인증을 허용하는 설정 변경은 하지 않는다. 실험용 기기 ID는 제품과 별도 PlayerPrefs 키로 보관해 반복 실행 시 계정이 늘지 않게 한다. 서버도 이번 실험에서는 같은 계정 인증을 사용한다. 이는 운영용 서버 신원 설계의 완료를 뜻하지 않는다.

작업 디렉터리는 저장소 루트다. `<cache>`는 동일 Unity 프로젝트에서 복원한 `Library/PackageCache`, `<lab>`는 아직 존재하지 않는 격리 디렉터리다.

```powershell
python Tools/network/topology-probe/prepare.py . <cache> <lab>
```

원본 패키지 캐시에서 필요한 패키지와 모듈만 가져온다. 원본 PlayerSettings의 Fusion 컴파일 심볼도 복사하며, 실험 빌드는 제품의 Preloaded Assets를 제거한다. Unity 에셋은 복사 시 `.meta`를 유지하고, 새 실험 씬·스크립트의 `.meta`는 격리 프로젝트에서 생성한다.

## 빌드

다음 두 빌드는 **순서대로** 완료를 확인한 후 실행한다. 동일 프로젝트에서 Unity 빌드 두 개를 동시에 실행하지 않는다. 프로젝트·출력·로그 경로는 절대 경로를 사용한다.

```powershell
$env:PROBE_OUTPUT = '<native-output>'
& '<Unity.exe>' -batchmode -nographics -quit -projectPath '<lab>' -buildTarget Win64 -executeMethod ProbeBuild.Build -logFile '<native-build.log>'

$env:PROBE_OUTPUT = '<webgl-output>'
& '<Unity.exe>' -batchmode -nographics -quit -projectPath '<lab>' -buildTarget WebGL -executeMethod ProbeBuild.Build -logFile '<webgl-build.log>'
```

두 로그의 `[ProbeBuild] ... Succeeded errors=0`과 실제 실행을 모두 확인한다. `has not been weaved`, `AssertException`, 상태 미복제 결과는 연결 성공 로그가 있어도 폐기한다. 빌드가 중단됐다면 해당 빌드의 자식 프로세스까지 종료된 것을 확인한 뒤 재시도한다.

## 실행

겹치지 않는 임시 방 이름을 사용한다. 실험은 Photon의 `kr` 리전, `topology-987-v1` AppVersion, 비공개 방으로 고정된다. 정상 게임의 방 목록에 게시되지 않는다. 테스트 서버와 브라우저는 같은 PC에서 실행해도 Photon Cloud를 실제 경유한다.

```powershell
Start-Process '<native-output>/Probe.exe' -WindowStyle Hidden -ArgumentList '-batchmode -nographics --mode Server --peer 0 --room T987EXAMPLE --seconds 300 -logFile "<server.log>"'
python Tools/network/topology-probe/serve.py '<webgl-output>' --port 4290 --log '<browser-events.log>'
```

서버 `CONNECTED` 확인 후 브라우저 두 개에서 접속한다. `serve.py`는 127.0.0.1에만 바인딩하며 공개 배포를 하지 않는다. WebGL 상태는 화면 상단과 로컬 서버 stdout 및 지정 로그에 기록한다. WebGL은 동일 출처 `/api/v1/accounts`를 사용하고, 로컬 서버가 고정된 기존 HTTPS 계정 API에 실험용 기기 ID를 전달하여 응답을 반환한다. 인증·TLS·운영 CORS 설정은 변경하지 않는다. 이 경로는 인증 정보를 취급하므로 승인된 로컬 실험에서만 실행한다.

```text
http://127.0.0.1:4290/?room=T987EXAMPLE&peer=1&seconds=35
http://127.0.0.1:4290/?room=T987EXAMPLE&peer=2&seconds=60
```

두 번째 브라우저가 로드될 때 첫 번째의 35초가 지나지 않도록 로컬 파일 다운로드 완료 후 실행한다. 필요하면 유효 시간을 늘린다. `players=2`, 두 브라우저의 `PASS`, 첫 번째 종료 후 두 번째의 `survivedPeerExit=True`와 증가하는 tick을 대조한다. 종료 후 같은 빌드로 재입장도 확인한다.

다른 버전은 `peer=4&version=topology-987-incompatible`을 사용하고, 없는 방은 `peer=5`와 새 임시 방 이름으로 접속한다. 두 경우 모두 Client가 서버를 새로 만들지 않고 `CONNECT_FAILED`로 끝나야 한다. 모든 실험 후 `python Tools/network/topology-probe/verify.py '<browser-events.log>'`로 판정한다.

완료 후 실험 서버가 Shutdown한 로그를 확인하고 이 작업에서 시작한 프로세스·브라우저만 종료한다. 인증 응답과 전체 SDK 디버그 로그를 커밋하지 않고 `[Probe]`의 검증 결과만 보관한다.

## 실행 기록

2026-09-14 중간 결과:

- Win64 v4, WebGL v2 및 동일 출처 인증 경로를 반영한 WebGL v3 빌드: `Succeeded errors=0`, Unity 종료 코드 0.
- 네이티브 Server/Client 실제 Photon 연결: Client의 tick 1752→2652, `requests=1 players=1 height=0.500 echo=1 wrongEcho=0`, 정상 Shutdown 확인.
- 네이티브 다른 AppVersion: `CONNECT_FAILED GameNotFound`. 서버를 새로 만들지 않음.
- WebGL 두 탭 실행: 인증 요청에서 `UnityWebRequestException: Unknown Error`. 기존 API OPTIONS 응답에 localhost 허용 CORS 헤더가 없어 접속 전 단계에서 중단. **WebGL 접속 통과 아님.**
- 동일 출처 계정 전달 경로를 구현했으나 실행은 자동 승인 검토에서 차단됨. 실험용 기기 ID의 기존 API 전달과 인증 응답 반환에 대한 사용자 승인 후 재검증 필요.
- 최종 브라우저 판정 도구는 아직 통과하지 않음. 두 Client 동시 접속·개별 회신·퇴장 후 유지·정상 종료·재접속은 후속 실행 대상.

초기 실험에서 Fusion weaving 누락/씬 bake 오류가 발생한 결과는 폐기했다. `prepare.py`의 PlayerSettings 복사와 `ProbeBuild.cs`의 씬 저장·재열기 절차에 원인 수정을 반영했다. 위 네이티브 결과는 수정 후 빌드에서만 수집했다.
