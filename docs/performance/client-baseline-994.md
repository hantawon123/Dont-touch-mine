# 클라이언트 공통 성능 기준 — S15P21D205-994

## 범위와 현재 상태

Unity 에디터 Play, Windows PC Player, WebGL을 별도로 측정한다. 공통 병목을 먼저 개선하고 플랫폼별 잔여 병목을 뒤에 처리한다. 기준 소스는 develop 1d264039에서 시작한 feature/client/performance-baseline이다. 현재는 측정 경로와 기준을 준비하는 단계이며 실제 6인 기준선이나 120FPS 달성을 검증한 상태가 아니다.

확인된 PC: Intel Core Ultra 9 185H, NVIDIA RTX 4070 Laptop 및 Intel Arc GPU. Windows 조회상 연결 화면은 2880×1800, 60Hz다. 실제 Unity 사용 GPU는 각 결과의 graphicsDevice로 확인한다. 화면 설정은 자동으로 바꾸지 않는다. 60Hz 화면의 결과를 120Hz 출력 검증으로 해석하지 않는다.

## 고정 조건과 초기 성능 예산

- Unity 6000.3.22f1, 동일 커밋·맵·입력 경로·품질·1920×1080 Game/Player 출력. 브라우저는 일반 Chrome의 정확한 버전을 별도 기록한다.
- 기준 PC에서 120Hz 이상 화면, 일반적인 6인 경기의 평균 120FPS 이상을 목표로 한다. 초기 판정 기준은 평균 120FPS 이상, p95 8.33ms 이하이며 p99·최대값·8.33ms 초과율도 반드시 함께 보고한다. 만족하지 못한 구간은 숨기지 않는다.
- CPU와 GPU의 병렬 처리 시간을 단순 합산하지 않는다. 측정 가능한 주 경로의 8.33ms 예산 초과를 찾고, 995·996의 세부 예산은 실제 상위 병목 확인 뒤 분배한다. 서버 tick 목표는 별도다.
- 에디터는 Game 창을 포커스하고 Scene 창 노출, Stats·Profiler·Console 상태, 다른 Unity/서버 프로세스를 기록한다. 같은 PC의 서버/여러 클라이언트 부하는 별도 조건으로 분리한다.
- 고정 렌더 해상도 비교와 자동 해상도 기능을 사용한 비교는 구분한다. 기존 WebGL 자동 해상도 판단은 최대 60FPS 기준이며 이 단계에서 동작을 바꾸지는 않았다. 렌더 스케일을 고정하지 않은 측정을 고정 해상도 개선으로 보고하지 않는다.
- 진단용 Development Build와 일반 Player 결과를 구분한다. Unity Profiler 연결·Deep Profiling은 비용이 있으므로 최종 프레임 비교와 별도 실행한다. Web에서 제공되지 않는 GPU 등 지표는 0으로 기록하지 말고 미수집으로 표시한다.

## 시나리오

각 안정 구간은 준비 후 10초 이상 워밍업하고 30초씩 3회 측정한다. 최초 로딩과 씬 전환은 별도 실행에서 시간·정지 구간을 기록한다.

1. 로비 대기 및 이동.
2. 마트의 동일 동선 탐색, 진열대 밀집 구역과 파쇄기 주변.
3. 6인과 물건이 함께 보이는 상태에서 줍기·운반·던지기·파괴·기절.
4. 결과와 하이라이트 정상 재생/종료.
5. 한 명만 스킵한 뒤 로비 이동, 다른 사람은 계속 시청. 스킵 직후와 원래 하이라이트 종료 시점을 모두 포함.
6. 경기 중 퇴장 후 방 생성·재입장·재경기.

장시간 검증(997)은 30분 이상 및 5회 경기 이상으로 고정하고 경기별 복귀 직후 메모리를 비교한다. 6인 실기는 서로 다른 PC에서 사용자와 조율한다. 단일/합성/동일 PC 다중 실행을 실제 6인 결과로 대체하지 않는다.

## 동일 프레임 기록기 사용

기존 WebFrameCapture를 확장했다. 평소에는 Update가 꺼져 있고 수동 측정 중에만 고정 배열에 프레임 간격을 저장한다. 배열 준비·환경 조회는 타이머 시작 전, 정렬·파일 저장은 타이머 종료 후다. 샘플 상한은 32768이며 상한 도달 시 실제 seconds를 보고한다. CPU/GPU 프로파일러나 전체 프로세스 메모리 측정기를 대체하지 않는다.

- 에디터: Play 후 `Game > Performance > Capture 30 Seconds`. Game 창을 유지한다.
- Windows Player: `Game.exe -perf`로 실행하고 F8을 누른다. 측정 중 다시 눌러도 현재 구간을 재시작하지 않는다.
- WebGL: URL에 `?perf=1`을 붙이고 기존 성능 측정 패널의 버튼을 누른다.
- 에디터/PC 결과: `Application.persistentDataPath/Performance/frames-*.json`, Console/Player 로그에도 경로를 남긴다. WebGL은 기존 패널에서 JSON을 복사한다.
- 매 결과와 함께 정확한 git 커밋, 시나리오·인원·서버 위치·브라우저·렌더 스케일·백그라운드 부하를 기록한다. 에디터의 revision은 PlayerSettings 버전이며 git 커밋을 자동 식별하지 않는다.

평균 FPS는 프레임 수/실제 경과 시간, p50/p95/p99는 nearest-rank다. 120FPS 초과 예산과 50ms 초과 끊김을 각각 센다. managedHeapBytes는 관리 힙의 시작/끝 값이며 native/GPU/전체 브라우저 메모리나 메모리 피크가 아니다. interrupted는 포커스·활성 씬·품질·화면 크기·프레임 제한 변화가 있었음을 뜻한다. 안정 구간 비교에서는 제외하고, 의도적인 씬 전환 측정은 전환 구간으로 따로 분류한다.

CPU/렌더/물리/GC 할당의 원인은 Unity Profiler로, 실제 Web 실행은 Development Build의 Web Profiler 및 브라우저 도구로 별도 진단한다. 사용자 플레이 장면을 임의의 빈 씬 벤치마크로 대체하지 않는다.

## 빌드 경로

원본과 분리된 프로젝트에서 빌드하며 기존 서버를 중단하거나 배포하지 않는다.

- PC: `CLIENT_REVISION`을 일치시킬 버전으로 지정하고 `Game.Editor.ClientBuild.Build` 실행. `CLIENT_OUTPUT`은 exe 경로, 기본값은 Builds/Client/Game.exe. `CLIENT_PROFILE_BUILD=1`이면 Development Build다.
- WebGL: 기존 `Game.Editor.WebBuild.Build`와 `WEBGL_REVISION`, `WEBGL_OUTPUT` 사용. `WEBGL_PROFILE_BUILD=1`일 때만 Development Build. 일반 빌드 기본값은 그대로 유지한다.
- 빠른 빌드 옵션의 결과를 실행 속도 최적화 빌드와 섞지 않는다. 서버/클라이언트의 버전과 개발 테스트 코드를 맞춘다.

## 후속 보고와 작업 경계

기준선 표에는 환경·커밋·시나리오·인원·표본 수·seconds·평균 FPS·p95/p99·8.33ms 초과율·최대 프레임·GC·관리 힙·별도 프로파일 근거를 넣는다. 현재 실기 수치는 미수집이다. 994를 완료로 처리하지 않는다.

기준선 확보 뒤 비용 상위 1~3개를 보고하고 995·996의 수정 범위를 정한다. 사용자 로컬 폰트·QualitySettings·Supermarket_copy 변경은 이 작업에 포함하지 않는다. MR 생성은 사용자에게 결과 보고 후 진행한다.
