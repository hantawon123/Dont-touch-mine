# 엔딩 맵 (Ending) — 경찰서 유치장

매치 종료 후 결과를 보여주는 엔딩 장면의 제작 문서. 컨셉은 [concept-guide.md](../concept/concept-guide.md)의 `[GAME END] — 탈출 성공 / 체포`.
컨셉 이미지: [game-ending-concept.png](../concept/game-ending-concept.png) — **한 장면**에 탈출 성공자는 철창 앞 복도에서 환호하고, 체포된 사람은 철창 안에 서 있다.

- 브랜치: `feature/client/ending-map`
- 이미지: `ending-stage-preview-v1.png` — 조립 씬에서 스폰 12자리에 미리보기 아바타를 세운 카메라 구도
- Jira: 724(에픽) · 725 컨셉 기획·에셋 · 726 임포트·URP · 727 모듈 조립 · 728 소품 · 729 조명 · 730 상호작용 소품 · 731 씬 통합·스폰 · 732 문서화 · 733 콜리전·플레이테스트

## 1. 컨셉 기획 (725, 2026-09-07 초안)

### 현재 결과 흐름 (이미 구현)
1. 매치 종료 → `Result.unity`가 인게임 위에 **화면 전체 오버레이**로 올라옴. 카메라·조명은 스코프가 꺼서 인게임 화면이 그대로 뒤에 보임.
2. `ResultPresenter`가 문구를 표시: 탈출 성공 / 체포 (`MatchTimerView.WinHeadline` 등). `ResultDisplaySeconds = 5`.
3. 5초 뒤 하이라이트 리플레이(클립 3개 × 10초) → 로비 복귀.

승패 데이터: `MatchResult.WinnerPlayerIndices`(권한자 발행) → `NetworkResultLobbyReturnController.IsLocalWinner`. 즉 **누가 탈출/체포인지 전원 명단을 클라이언트가 알고 있다.**

### 엔딩 장면 목표
결과 5초 구간을 텍스트 오버레이에서 **3D 연출 장면**으로 바꾼다.
- 배경: 경찰서 유치장(복도 + 철창 + 잠긴 문 + 열린 출구).
- 탈출 성공자: 철창 **앞 복도**에 서서 환호(컨셉의 팔 들기·점프 포즈).
- 체포된 사람: 철창 **안**에 서서 철창을 붙잡은 낙담 포즈.
- 카메라: 복도 끝에서 철창을 정면으로 보는 고정 카메라(에셋 제공값 위치 (0, 1.85, 7.6), 회전 (4.5, 180, 0), FOV 40.4).
- 문구는 기존 `ResultView`를 그대로 위에 얹는다(탈출 성공! / 체포되었습니다).

### 베이스 에셋 — 이미 확보됨
`Assets/PoliceHoldingStylized/` (2026-09-07 develop에 추가, `e70c86ec`, hantawon123). Unity 6 URP용, 단일 프리팹 `Police_Holding_Stylized.prefab`.
- 크기 7.5 × 5.2 × 13.9 m. 유치장(z -3~0, 내부 깊이 2.63 m) + 복도(z 0~10) + 출구(z 10, 흰빛 배경). 철창 폭 7.17 m.
- 렌더러 171개, URP Lit/Unlit 머티리얼 35개, BoxCollider 12개(바닥·벽·철창·출구 차단). 라이트 오브젝트 그룹 `Lighting` 포함(펜던트 전구 3개).
- 컨셉 이미지와의 차이: 컨셉은 형광등 3개 + 벽걸이 소화기 + 자물쇠 클로즈업. 에셋은 펜던트 전구 + 바닥 소화기 + 경찰 방패 장식. 분위기(회청색 벽돌·차가운 빛)는 조명으로 맞춘다.
- 추가 구매 불필요. 필요한 것은 **에셋이 아니라 애니메이션**.

### 필요한 애니메이션 (없음 → 제작 필요)
현재 클립: Idle·Walk·Run·Jump·Fall·Land·Crouch·Crawl·Punch·Stunned. 엔딩용 없음.
- `Celebrate`(환호: 팔 들기 + 제자리 점프 반복) — 탈출자 최대 6명이 같은 클립을 쓰되 시작 시간을 어긋나게.
- `Dejected`(낙담: 철창 붙잡고 고개 숙임 또는 무릎 꿇기) — 체포자.
- 제작 경로: Blender 리그(`PlayerCapsule_*.blend`)에서 액션 2개 추가 → FBX → `Assets/_Game/Content/Animations/`. Mixamo 리타겟(Humanoid)도 가능(Cheering, Defeated 계열).

### 씬 구성안 (727·728·731에서 구현)
- 새 씬 `Assets/_Game/Content/Scenes/Ending.unity` 또는 기존 `Result.unity`에 3D 배경 추가. **Result.unity 확장** 추천: 진입·타이밍·문구·하이라이트 전환이 이미 이 씬에 묶여 있어 흐름 변경이 없다. 스코프가 카메라·조명을 끄는 `DisableAdditiveSceneOutputs`는 엔딩용 카메라·조명을 예외로 두도록 수정.
- 배치: 유치장 프리팹 원점 고정. 스폰 포인트 두 세트 — `EscapeSpawn_1~6`(복도 z 3~6, 카메라 쪽에 가깝게 부채꼴) · `ArrestSpawn_1~6`(유치장 안 z -2~-0.6, 철창 바로 뒤 한 줄).
- 아바타: 네트워크 아바타를 옮기지 않고 **로컬 복제 비주얼**을 세운다(하이라이트 리플레이가 `Animator`가 달린 대상에 액션을 재생하는 방식과 같은 계열). 각 플레이어의 탈 종류·색은 로비/매치 외형 데이터로.
- 소품: 탈출자 손에 자기 물건(컨셉의 자루)을 들려 주는 것은 선택. 유치장 안 벤치는 에셋에서 제거된 상태.
- 조명: 컨셉의 차가운 형광 톤. 에셋 펜던트 전구는 색을 냉색으로, 상단에 긴 형광등 영역광 2~3개 추가 → 베이크(로비의 `Game/Lobby/Lighting` 메뉴를 엔딩용으로 복제).
- 상호작용 소품(730): 엔딩은 관람 장면이라 **들 수 있는 물건 없음**으로 제안. 이슈는 "해당 없음" 사유로 닫기.
- 콜리전(733): 아바타를 코드로 세우므로 이동 없음. 콜라이더는 에셋 12개로 충분. 플레이테스트는 6인 결과 조합(전원 탈출·전원 체포·혼합) 확인.

### 결정
- **담당 (2026-09-07)**: 엔딩 파트는 지환이 넘겨받아 전담한다. 유치장 에셋은 hantawon123이 추가한 것을 그대로 사용.

### 열어 둘 결정
1. 노출 시간: 3D 연출은 5초가 짧다. 7~8초로 늘릴지(`ResultDisplaySeconds`), 하이라이트 총 길이와 함께 결정.
2. 애니메이션 제작 주체(블렌더 캐릭터 담당 vs Mixamo 리타겟).
3. 씬 구성: Result.unity 확장(추천) vs 별도 Ending.unity.

## 2. 에셋 임포트·URP 확인 (726, 2026-09-07)

- 변환 작업 **불필요**: 머티리얼 35개 전부 URP Lit/Unlit, 텍스처 없이 플랫 컬러(`MapData.json`의 색·roughness·metallic 값), 이미시브 없음. FBX 스케일 1, Read/Write 꺼짐(콜라이더는 별도 BoxCollider라 문제 없음).
- 프리팹 안 `Lighting` 그룹에 실시간 스팟 6개: 창문 필(0.55, 온색), 펜던트 3개(2.8~3.4, 온색), 복도 바운스(1.4, 냉색, 그림자 없음), 출구 백광(7.6, 흰색). 전부 Realtime이라 베이크 전 상태. 데모 씬 환경광은 Flat #6B6E70, 스카이박스 기본, 안개 없음.
- 데모 카메라: (0, 1.85, 7.6) / (4.5, 180, 0) / FOV 40.4, Post Processing 켜짐, 배경 단색. 컨셉 구도와 같은 정면 샷.
- 컨셉(차가운 형광 톤)과 데모(온색 펜던트)의 차이는 729 조명에서 색온도·영역광 추가로 맞춘다.

## 3. 환경 프리팹과 배치 (727 시작, 2026-09-07)

- `Assets/_Game/Content/Prefabs/EndingHoldingEnvironment.prefab` — 씬 결정(Result.unity 확장 vs 별도 씬)과 무관하게 먼저 만든 단위.
  - `Police_Holding_Stylized`(유치장 에셋, 원점) · `EndingCameraAnchor`(0, 1.85, 7.6 / 4.5, 180, 0 / FOV 40.4) · `EscapeSpawnPoints` 6 · `ArrestSpawnPoints` 6
  - **스폰 규칙 (2026-09-08 확정)**: 진 사람(체포)은 **철창 안**에서 **카메라 쪽(+Z)**을 보고 서서 갇힌다. 이긴 사람(탈출)은 **철창 밖 복도**에서 **철창 쪽(-Z)**을 본다(등이 카메라 쪽).
  - 체포자 스폰(유치장 안, 철창 뒤 0.8 m, rotY 0): (-2.5, -0.8) (-1.3, -0.9) (0, -0.8) (1.3, -0.9) (2.5, -0.8), 예비 (0, -1.8). 유치장 내부 z -3.45~-0.12(사용자 확장 후), 폭 ±3.42.
  - 탈출자 스폰(복도, 철창 앞 1.6/2.8 m, rotY 180): 앞줄 (-1.5, 1.6) (0, 1.4) (1.5, 1.6), 뒷줄 (-2.2, 2.8) (0, 2.9) (2.2, 2.8).
  - 12개 모두 콜라이더와 겹치지 않음. 유치장 정면 콜라이더(`COLLIDER_Locked_Cell_Front`, z ±0.12)가 철창 통과를 막는다. 유치장 확장 뒤 뒷벽 콜라이더(z -2.75)는 옛 위치라 733에서 갱신 필요.
  - 구도 검증(카메라 투영 계산): 12명 모두 화면 안. 철창은 화면 세로 0.28~0.83 구간, 체포자 머리 0.58·발 0.30, 탈출자 앞줄 발 0.08~0.15. 뒷줄을 z 3.4에 두면 발이 화면 아래 가장자리에 걸려 3.0으로 당김.
- URP는 에디터 모드 `Camera.Render()`로 라이트가 반영되지 않아 미리보기 렌더는 불가. 스크린샷은 씬 조립 후 씬 뷰로 찍는다.

## 4. 조명 1차 — 전체 밝기 (729 시작, 2026-09-07)

- 요청: 실내가 어둡고 전등 아래만 밝음 → 전체적으로 밝게. 실내라 태양광은 출구·창으로만 들어오므로 **환경광 + 천장 냉백색 조명**으로 해결.
- 조립 씬 `Assets/_Game/Content/Scenes/EndingBuild.unity` 생성(데모 씬은 에셋 팩 파일이라 건드리지 않음): 환경광 Flat #9EA8B8(0.62/0.66/0.72, 데모의 약 1.5배), 안개 없음, 카메라는 앵커 위치·FOV 40.4·Post Processing 켬.
- 프리팹 `EndingLights` 그룹 추가(Mixed): 컨셉의 형광등 자리에 천장 스팟 3개(복도 앞 z 6.5 · 복도 중간 z 3.0 · 유치장 안 z -1.5, 150°, 냉백색 #DBEBFF, 3.0~3.5) + 출구로 들어오는 태양광 Directional(1.2, 약한 온색, 소프트 그림자).
- 에셋 자체 조명(펜던트 온색 스팟 등 6개)은 유지. 다음: 베이크 대상 결정과 라이트맵 베이크, 톤 조정(너무 평평하면 환경광 0.5 수준으로 내리고 스팟 대비 확보).

## 5. 콜라이더 갱신 (733, 2026-09-08)

- 사용자가 조립 씬에서 바꾼 유치장(뒷벽 z -3.45로 확장, 측벽·천장·바닥·파이프 연장, 표지판·방패 5개 제거, 자물쇠·창문 이동, 조명 값)은 씬 인스턴스 오버라이드였음 → `PrefabUtility.ApplyPrefabInstance`로 **프리팹에 반영**. 이제 `EndingHoldingEnvironment.prefab`이 기준이고 Result 씬에서도 같은 지오메트리가 나온다.
- 콜라이더를 렌더러 실측값에 맞춰 재설정(프리팹 안 중첩 오버라이드):
  - `COLLIDER_Cell_Rear` z -4.17~-3.45(안쪽 면 = 벽 페인트 면), 높이 4.92
  - `COLLIDER_Cell_Side_±1` z -4.17~0.15, 안쪽 면 x ±3.42
  - `COLLIDER_Cell_Floor` z -4.17~0
  - `COLLIDER_Locked_Cell_Front`(철창) 폭 ±3.95, **높이 0~4.84(천장까지)** → 철창을 뛰어넘을 틈 없음
  - `COLLIDER_Ceiling` 신규: y 4.74~5.04, 유치장 뒷벽부터 복도 끝까지 전체 천장(점프 안전망)
- 검증(물리 레이캐스트, 유치장 중앙 (0,1,-1.5)에서): 뒷벽 -3.45, 좌우 벽 ±3.42, 철창 -0.12, 천장 4.74 모두 정확히 맞음. 철창 위 y 4.0에서도 철창 콜라이더에 막힘.
- 복도 측벽·출구 차단 콜라이더는 원본 그대로(변경 없음).

## 6. 아바타 세우기 — Result 씬 통합 (731, 2026-09-08)

- **방식 (2차 확정)**: 호스트(권한자)가 숨기기 스폰과 같은 경로 `NetworkRunnerService.TryTeleportPlayer`로 **실제 아바타를 무대 자리로 텔레포트**한다. 위치는 Fusion이 전원에 동기화. 철창·벽 콜라이더가 실제로 가두고, 걸어 다니는 모습이 고정 카메라에 보인다.
  - 1차는 리플레이의 `ReplayVisual` 복제본을 세우는 방식이었으나, 진짜 플레이어는 인게임 맵에 남아 "갇힘"을 확인할 수 없어 폐기(2026-09-08).
  - 이동은 허용(가둠을 체감), 물건 상호작용(`PlayerInteractor.IsInputLocked`)만 잠금. 클라이언트는 카메라 전환과 문구 배경 끄기만 담당.
  - 결과가 끝나면 하이라이트가 복제본으로 재생되고, 로비 로드 시 스폰 재배치가 아바타를 되돌린다.
- **Result 씬**: 에디터 메뉴 `Game/Ending/1. Place Ending Stage In Result Scene`이 `EndingStage` 루트(y -300, 인게임 맵과 겹치지 않게)에 환경 프리팹 + `EndingCamera`(depth 5, 인게임 카메라 0 위에 덮음, FOV 40.4, PP·SMAA) + `Visuals` 컨테이너를 놓고 `ResultLifetimeScope.endingStage`에 연결한다. 기존 결과 문구 캔버스(ScreenSpaceOverlay, sortingOrder 100)는 그 위에 얹힌다.
- **코드**
  - `EndingStageLayout`(Core): 참가자 + 승자 명단 → (탈출 여부, 자리 번호). 플레이어 번호 순, 자리 부족 시 마지막 자리 재사용. 단위 테스트 `EndingStageLayoutTests`.
  - `EndingStage`(Client): 카메라·앵커·스폰 루트 참조, `ShowCamera/HideCamera/Slot`.
  - `EndingStagePresenter`(Bootstrap): 호스트에서 결과(`NetworkResultLobbyReturnController.HasMatchResult`, `LastWinnerPlayerIndices`)와 참가자 명단으로 배치 후 텔레포트, 실패한 플레이어는 Tick에서 재시도. 전원 완료 시 `[Ending] Staged N of M players (escaped K)` 로그.
  - `ResultLifetimeScope`: 카메라·조명 끄기에서 무대 하위는 예외. 무대가 없으면 기존 텍스트 전용 흐름 그대로.
- **미리보기**: `Game/Ending/2. Preview Avatars In Active Scene`이 조립 씬 스폰 12자리에 `PlayerCharacter` 복제본을 세운다(저장하지 말고 3번으로 정리). `ending-stage-preview-v1.png`.
- **남은 것**: Celebrate/Dejected 애니메이션(현재 Idle·이동), 조명 톤·베이크.

## 7. 첫 E2E 테스트와 수정 (2026-09-08)

- 실제 매치 종료로 확인: 프레젠터 로그 `[Ending] Staged 2 of 2 players (escaped 0)` 정상. 그러나 **무대가 보이지 않음** → 결과 문구 캔버스가 원래 인게임 위에 문구만 띄우던 화면이라 전체 화면 불투명 검은 배경(`Result Background`)을 깔고 있었음.
  - 수정: `IResultView.SetBackdropVisible` 추가, `EndingStagePresenter`가 무대가 연결된 경우 배경을 끄고 종료 시 복원. 무대 없는 씬은 예전 그대로.
- 두 번째 테스트: 무대·아바타·문구 정상 표시. 피드백 두 가지 반영.
  - 노출 시간 5초 → **8초** (`NetworkResultLobbyReturnController.ResultDisplaySeconds`). 하이라이트 3클립은 그대로.
  - 결과 화면 동안 로컬 플레이어가 자기 캐릭터를 움직일 수 있었음(카메라는 무대 고정이라 복제본은 그대로지만 원본이 맵을 돌아다님). 로비 Esc 메뉴와 같은 잠금(`PlayerMovement.IsMovementLocked`, `PlayerInteractor.IsInputLocked`)을 무대 표시 동안 걸고 종료 시 해제.
- 세 번째 테스트(실제 텔레포트 방식): 호스트 로그 `[PlayerTeleport] … target=(-2.50, -300.00, -0.80) success=True`로 철창 안 자리 이동 확인. 그러나 **움직일 수 없음** → 권한자의 조작 정책(`NetworkMatchRuntimeCoordinator.SynchronizePlayers`)이 숨기기·탐색 단계에서만 조작을 켜고 결과·하이라이트 단계(`MatchPhase.Highlight`)에서는 끄기 때문.
  - 수정: 하이라이트 단계라도 **결과 씬이 떠 있는 동안**(`INetworkResultNavigation.IsResultSceneLoaded`)은 조작을 켠다. 결과 씬이 내려가 리플레이로 넘어가면 자동으로 다시 꺼짐. 테스트의 가짜 권한자는 해당 인터페이스가 없어 기존 동작 유지.
- 네 번째 테스트: 움직이긴 하나 **WASD 기준이 이상함**. 원인은 이동이 각자의 플레이어 카메라 리그(무대에서는 보이지 않음) 기준이고, 몸이 그 카메라 방향을 향하는 기본 규칙 때문. 요구: W = 화면 안쪽(카메라에서 멀어짐), S = 카메라 쪽, A/D = 화면 좌우, 처음 방향은 체포자 카메라 쪽·탈출자 등지기.
  - 수정: `PlayerMovement.SetStageControl(Transform)` — 지정되면 이동은 그 참조(무대 카메라)의 앞·오른쪽 기준으로 계산하고, 입력 의도를 "이동 방향으로 전진 + 그 방향의 yaw"로 바꿔 보낸다. 모터는 lookYaw로 몸을 돌리므로 **몸이 이동 방향을 향하고**, 멈추면 현재 방향을 유지(텔레포트로 준 초기 방향 그대로). `EndingStagePresenter`가 로컬 아바타에 지정·해제.
- 다섯 번째 테스트: 승자(로컬 플레이어)의 **몸이 안 보이고 물건만 보임**. 원인은 카메라 리그가 1인칭일 때 자기 몸 렌더러를 `ShadowsOnly`로 숨기는 처리 — 무대 카메라는 다른 카메라라 그 상태가 그대로 남음.
  - 수정: `PlayerCameraController.SetBodyVisibleOverride(bool)` 추가. 무대가 떠 있는 동안 1인칭이어도 몸을 그리고, 종료 시 원래 규칙으로 복귀. 프레젠터가 시작·Tick에서 리그를 찾아 켬.

## 8. 카메라 구도 C안과 뒷벽 (2026-09-08)

- 캐릭터가 작게 보인다는 피드백 → 방 크기를 줄이지 않고 **카메라를 당김**. 게임 카메라 렌더로 세 안을 비교:
  - A(기존): 앵커 (0, 1.85, 7.6), 화각 40.4 → 탈출자 약 40%, 체포자 약 29% 높이.
  - B: 5.9 m로 확 당기면 앞줄 탈출자의 등이 화면을 가림(폐기).
  - **C(채택)**: 앵커 **(0, 1.75, 6.6), 피치 4°, 화각 40**, 탈출자 줄을 철창 쪽으로 당김 → 앞줄 (-1.5, 1.2) (0, 1.0) (1.5, 1.2), 뒷줄 (-2.2, 2.0) (0, 2.1) (2.2, 2.0). 탈출자 약 45%, 체포자 약 32%, 철창이 화면에 꽉 참.
- **카메라 뒷벽**: 탈출자가 카메라 뒤로 걸어가 사라지지 않게 카메라 1 m 뒤(z 7.6~7.9)에 폭 8 m·높이 5 m 보이지 않는 BoxCollider `StageBounds/COLLIDER_Camera_Backstop` 추가. 레이캐스트 검증: 뒷줄에서 카메라 쪽 5.5 m 지점에서 막힘.
- Result 씬 무대 재배치(`Game/Ending/1`)로 반영, EndingBuild 카메라도 동일. 주의: 프리팹을 저장한 같은 프레임에 재배치하면 옛 프리팹이 들어가므로 `AssetDatabase.ImportAsset(ForceUpdate)` 후 실행해야 함(메뉴 재실행 시에도 참고).

## 9. 소지 물건 처리 (2026-09-08)

- 관찰: 마지막에 물건을 들고 있던 플레이어가 무대에도 그 물건을 든 채 텔레포트된다(홀드 포인트에 붙어 함께 이동).
- **결정: 승자만 유지.** 승자가 훔친 물건을 든 채 철창 앞에 선 모습은 컨셉 이미지(주머니를 든 탈출자)와 같은 승리의 증거. 패자가 들고 있던 물건은 무대에서 보이지 않게 한다.
  - 실제 내려놓기는 권한자의 매치 규칙(`MatchSessionCoordinator.TryDropHeldObject`)이 결과 단계에서 거절하므로, **각 클라이언트가 패자의 소지 물건 렌더러만 숨기고**(`forceRenderingOff`) 결과가 끝나면 되돌린다. 하이라이트·로비 전환에서 물건 상태는 초기화된다.
- 결과 화면 동안 물건 조작 잠금: F키·던지기는 `PlayerInteractor.IsInputLocked`, 배치 모드(우클릭 진입·실루엣)는 `ItemPlacementController.IsInputLocked`로 막는다(켜지면 진행 중인 배치 모드도 끝나고 물건은 머리 위로 돌아간다). 종료 시 해제.

## 참고 파일
- 결과 흐름: `Assets/_Game/Bootstrap/ResultLifetimeScope.cs`, `NetworkResultLobbyReturnController.cs`, `Assets/_Game/Content/Scenes/Result.unity`
- 리플레이 아바타 재생: `Assets/_Game/Bootstrap/HighlightReplayPlayer.cs`
- 와이어프레임: `docs/planning/wireframes/ending.md`, `highlight.md`
