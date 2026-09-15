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

기준선 표에는 환경·커밋·시나리오·인원·표본 수·seconds·평균 FPS·p95/p99·8.33ms 초과율·최대 프레임·GC·관리 힙·별도 프로파일 근거를 넣는다. 초기 에디터 단독 플레이 수치는 아래에 기록했다. PC Player·WebGL·6인 기준선은 아직 미수집이다. 994를 완료로 처리하지 않는다.

기준선 확보 뒤 비용 상위 1~3개를 보고하고 995·996의 수정 범위를 정한다. 사용자 로컬 폰트·QualitySettings·Supermarket_copy 변경은 이 작업에 포함하지 않는다. MR 생성은 사용자에게 결과 보고 후 진행한다.

## 2026-09-15 AC 전원 자동 측정 — 첫 공통 개선

- RTX 4070 Laptop, D3D12, 에디터 Game 2880×1418, 품질 프리셋 이름 WebGL, 화면 약 60Hz. 이 결과는 브라우저 실행이 아니다. 해상도와 품질을 낮추지 않았다.
- 동일 PC의 별도 서버 에디터(`-batchmode -nographics`)와 렌더링하는 클라이언트 에디터 1개. 마트 탐색 단계에서 서버가 출발 위치 (-20, 0.2, -13)를 준비한 뒤 실제 Fusion/KCC 입력으로 반복 이동했다. 준비 이동은 측정 전에 한 번만 수행했다.
- 첫 탐색 프로파일에서 `NetworkHighlightPlaybackController.Tick`이 평균 약 8.73ms였다. 일반 경기 중에도 매 프레임 모든 물건과 로비를 재검색하는 경로였다.
- 씬 조립 때 로비 물건을 제외한 물건 참조를 보관하고, 이후 실제 리플레이에 포함되는 ID로만 조회하도록 변경했다. 물건 렌더링·품질·충돌을 끄지 않는다. 씬 합쳐짐/운반으로 씬이 바뀌어도 참조를 유지하며, 원본이 파괴된 뒤에도 기존 리플레이 복사본을 보존한다.
- 동일 경기에서 기존 전체 검색 분기와 새 참조 조회 분기를 교대로 30초씩 3회 측정했다. 검증 실행기가 private `sceneBound` 값을 바꿔 기존 fallback을 비교 경로로 사용했다. 루프 동선의 시작 위상까지 동일한 프레임 비교는 아니므로 범위를 함께 보고한다. Profiler를 켠 마지막 별도 구간은 아래 FPS에서 제외했다.

| 방식 | 평균 FPS 3회 | p95 ms 3회 | p99 ms 3회 |
| --- | --- | --- | --- |
| 기존 전체 검색 | 44.87 / 42.59 / 44.15 | 28.08 / 30.11 / 28.88 | 32.53 / 34.90 / 33.75 |
| 씬 참조 재사용 | 65.14 / 65.90 / 64.73 | 19.91 / 19.87 / 19.50 | 25.25 / 23.49 / 22.18 |

평균 FPS 중앙값은 약 47.5% 개선됐다. **120FPS 달성 결과는 아니다.** 후속 프로파일에서 VContainerUpdate 약 3.59ms, 카메라 렌더링 약 4.73ms, 물리 업데이트 약 1.59ms와 에디터 표시 비용이 남았다. 중첩된 프로파일 마커를 단순 합산하지 않는다. `Highlight.CapturePlayers/Ids/Items` 마커로 잔여 준비 비용을 구분한다.

검증: NetworkMatchHudPresenterTests와 HighlightMapReadinessTests 총 42개 통과. 초기 새 테스트의 에디터 씬 생성 방식은 수정 후 재실행했다. 검증 자료는 작업 공간 `.build/performance-995-ab`의 trial-0~6.csv, environment.json, cpu.raw, markers.csv, game.png 및 `performance-995-highlight-tests-v3.xml`이다. 원시 프로파일은 저장소에 포함하지 않는다.

앞선 수동 30초 기록은 평균 약 30.33FPS였지만 interrupted=true였고 해상도도 1528×819여서 이 비교의 기준선으로 사용하지 않았다. 전원 연결 전후의 순수 개선율이나 실제 GPU 교체를 입증하는 자료로도 사용하지 않는다. 서로 다른 PC의 6인, PC Player, WebGL, 하이라이트/재경기 장시간 시나리오는 별도 검증이 필요하다.
## 캐릭터 준비 비용 추가 개선

추가 프로파일에서 `Highlight.CapturePlayers`가 약 2.46ms였다. 방 참가자별 리플레이 복사본이 모두 준비되면 캐릭터 전역 검색을 멈춘다. 참가자 명단보다 아바타가 늦게 도착하거나 같은 인원수에서 참가자가 교체되면 다시 검색하며, 퇴장한 참가자의 기존 복사본은 보존한다.

이번 실행 환경은 RTX 4070 Laptop / D3D12 / 2880×1418 / 약 120Hz였다. 앞선 60Hz 실행과 절대 수치를 직접 비교하지 않는다. 같은 경기 안에서 검색 유지·검색 생략·GPU Resident Drawer 시험을 각 30초씩 세 차례 교차했다.

| 방식 | 평균 FPS 3회 | p95 ms 3회 |
| --- | --- | --- |
| 물건 개선만 적용, 캐릭터 검색 유지 | 75.94 / 71.26 / 67.91 | 16.22 / 17.44 / 19.16 |
| 캐릭터 준비 완료 후 검색 생략 | 83.49 / 78.43 / 79.69 | 15.67 / 16.89 / 16.37 |
| 검색 생략 + Forward+ / GPU Resident Drawer 시험 | 83.58 / 81.64 / 79.91 | 15.24 / 15.50 / 15.76 |

검색 생략의 평균 FPS 중앙값 개선은 약 11.8%다. GPU 시험은 추가 이득이 작고 그리기 작업 수가 늘어 원본 설정에 적용하지 않았다. 앞선 BRG Keep All 미설정 시험은 실제 GPU Resident Drawer가 비활성 상태여서 해당 기능의 결과에서 제외했다. 수정한 시험에서는 GPUResidentDrawer 프로파일 마커로 실행을 확인했다.

마지막 별도 Profiler 구간에서 VContainerUpdate는 약 0.75ms, 물건 참조 조회는 약 0.008ms였다. 이 구간은 FPS 비교에서 제외했다. HUD LateUpdate의 약 16KB/frame 할당과 에디터 ItemCollectionBuilder의 플레이 중 요청 파일 조회가 후속 분석 대상이다. **평균·p95 모두 120FPS 목표 미달이며 995는 진행 중이다.**

검증: 최종 소스의 NetworkMatchHudPresenterTests + Game.Tests.EditMode.HighlightMapReadinessTests 43개 통과. 자료: 작업 공간 `.build/performance-995-players-gpu` 및 `performance-995-players-final-tests.xml`. 동일 PC 서버/클라이언트 한 명의 자동 이동 결과로, 6인·모든 구역·하이라이트 전환의 성능 보증이 아니다.