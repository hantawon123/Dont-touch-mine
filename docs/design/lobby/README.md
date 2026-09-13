# 로비 맵 (Lobby) — 지하실 아지트

대기실(로비) 맵의 제작 과정과 결정 사항, 중간 결과 이미지를 둡니다.
컨셉은 [concept-guide.md](../concept/concept-guide.md)의 `[LOBBY] — 작전실`: 도둑들이 작전을 준비하는 지하 아지트.

- 파일명은 영문 소문자와 하이픈. 예: `basement-overview-topdown.png`
- Unity 조립 씬: `Assets/PolyWorkshop_BasementWorkshop/Scenes/LobbyBuild.unity` → 프리팹 `Assets/_Game/Content/Prefabs/LobbyBasementEnvironment.prefab` → 실제 로비 씬 `Assets/_Game/Content/Scenes/Lobby.unity`에 배치

## 목록

- `basement-overview-topdown.png` — 지하실 내부 전경(위에서). 작업대 존·잡동사니 존·바닥 전단 배치 확인용
- `basement-wanted-posters.png` — 서벽의 탈 종류별 WANTED 포스터 4장(Rabbit/Bear/Cat/Dog)
- `basement-ceiling-lights.png` — 천장 장선과 조명 배치, 동벽 계단·초록 문
- `basement-boundary-colliders.png` — 벽에 붙인 보이지 않는 경계 콜라이더 6면(연두색 와이어)
- `basement-lighting-bake-v1.png` — 1차 라이트맵 베이크 결과(전등 25개, 간접광·AO 적용). 이후 조정 시 v2로 추가
- `basement-plan-board-v1.png` — 서벽 공구판을 작전 계획판으로 쓰는 방 설정 상호작용 확인 화면
- `basement-lighting-bake-v2.png` — 조명 2차(포인트 라이트 베이크 전환·태양광 냉색·포스트프로세스) 재베이크 결과. 동북 스폰 구역에서 작업대 방향
- `basement-plan-board-v2-outline.png` — 공구판+책상을 하나로 묶은 작전 계획판에 흰색 5 px 실루엣을 켠 모습(플레이 모드)
- `basement-plan-board-v3-label.png` — 실루엣 위에 공중 라벨 "ROOM SETTING ▼"을 더한 최종 안. 실제 방 생성→로비 입장 흐름의 스폰 지점에서 본 화면
- `posters/` — WANTED 포스터 원본 이미지 4종(1024×1536). 데칼 아틀라스 합성에 사용

## 제작 경과

### 1. 컨셉과 레이아웃 (2026-09-04)

- 로비를 "도둑 아지트 = 지하실 작업실"로 정의. Blender로 14×12×5 m 블록아웃을 먼저 만들어 레이아웃 설계도로 사용
  (서벽 작업대 존, 북벽 선반 존, 동벽 금고·계획판·닫힌 문, 남벽 높은 창 3개, 천장 목재 장선, 잡동사니 존).
- 에셋: Unity Asset Store **Basement Workshop Environment**(PolyWorkshop) 구매. 모듈러 벽·천장·기둥과 소품 프리팹 제공.
  Built-in 렌더 파이프라인 에셋이라 URP 머티리얼 변환 후 사용.

### 2. Unity 조립

- `LobbyBasementEnvironment` 루트 아래에 역할별로 분리:
  `BasementModules`(BigRoom·SmallRoom 벽/천장/기둥) · `StaticProps`(고정 소품 79) · `Items`(공구 15) · `Decals`(종이류 20) · `Lights`(20) · `Boundary`(경계 6)
- 설계 원칙: 상자·콘·타이어·가스통처럼 들고 옮길 수 있는 소품은 벽과 합치지 않고 **개별 오브젝트**로 둔다.
  이후 물건 들기/놓기 시스템(Carryable)에 연결할 대상이기 때문.

### 3. WANTED 포스터 데칼 (2026-09-07)

- 팩의 데칼 10종은 아틀라스 텍스처 1장을 공유한다. 원본을 건드리지 않고 복제본 텍스처의 포스터 칸(Decal04 세로 포스터, Decal08 바닥 전단)에만 우리 그림을 합성.
- 탈 종류(Rabbit/Bear/Cat/Dog)마다 아틀라스 1장 + 머티리얼 1개, 총 4세트:
  `Assets/_Game/Content/Textures/TEX_LobbyDecals_{탈}.png`, `Assets/_Game/Content/Materials/MAT_LobbyDecals_{탈}.mat`
- 벽의 세로 포스터 4장에 세트를 하나씩, 바닥 전단은 세트를 섞어서 배치.
- **결정**: 바닥 신문·전단은 고정 배치(들 수 없음). 그림자에 사각 프레임이 생기는 문제는 머티리얼을 Transparent → **Opaque + Alpha Clipping**으로 바꿔 해결.

### 4. 경계 콜라이더 (2026-09-07)

- 벽 모듈 콜라이더가 있어도 물리 버그로 맵 밖에 나가는 것을 막는 안전망. 에디터 메뉴 `Game/Lobby/Build Boundary Colliders`(`LobbyBoundsBuilderMenu.cs`)로 자동 생성.
- 측정 기준은 **벽·문 모듈(Basement_Wall/Door)의 최외곽 면**, 높이는 벽 상단/천장 모듈. 바닥 판(Ground)과 천장 위 램프 메시를 포함하면 경계가 벽에서 수 m 떨어지므로 제외.
- 결과: 내부 13.3 × 6.4 × 12.5 m (x -3.15~10.10, z -10.35~2.15, y -0.09~6.32), 판 두께 2 m, 벽 면에서 5 cm 여유.

### 5. 로비 씬 통합 (2026-09-07)

- `LobbyBasementEnvironment` 루트를 프리팹으로 저장하고 `Lobby.unity`의 창고 환경(LobbyWarehouseEnvironment)과 교체. 조립 씬(LobbyBuild)은 프리팹 인스턴스로 연결돼 있어 이후 수정은 프리팹에서 한다.
- 프리팹에 **함께 들어간 것**: 벽·천장·기둥 모듈, 소품 79, 공구 15, 데칼 20, 전등 25(URP 라이트 데이터 포함), 콜라이더 전부, 경계 6면, Reflection Probe 2개(`Probes` 아래로 이동), Static 플래그.
- 프리팹에 **안 들어가서 씬에 직접 옮긴 것**(Lighting 창의 Environment 설정): 스카이박스 `Skybox1_Material`, 환경광 Skybox 1.5, 안개 ExpSquared 0.03(회청색), 태양 = 프리팹의 Directional Light.
- Lobby 씬에서 **제거한 것**: 창고 프리팹 인스턴스, 40 m 바닥 콜라이더(LobbyGroundCollider, 지하실 바닥·경계와 중복), 씬 자체 Directional Light(실내에 외부 태양광이 새어 들어옴). 조립 씬의 Post-process Volume은 Built-in 전용 스크립트가 깨진 상태여서 삭제.
- 스폰 포인트 6개(x 6.4, z -6~-1)는 지하실 내부 바닥 위이고 소품과 겹치지 않음. 동벽을 보고 있던 방향을 방 안쪽(-X)으로 회전.
- 창고 프리팹 `LobbyWarehouseEnvironment.prefab` 삭제. 원본 에셋 팩 `Assets/IGBlocks/IG_Warehouse`(129 MB)는 다른 곳에서 참조하지 않아 삭제 가능(팀 확인 후).
- 조명 참고: 팩 라이트 25개 중 Baked 10·Mixed 6이지만 라이트맵은 베이크되지 않은 상태. 현재 보이는 결과는 Realtime/Mixed 직접광만이며, 베이크는 조명 연출(614)에서 Lobby 씬 기준으로 진행.

### 6. 플레이테스트와 계단 수정 (2026-09-07)

- Home → 방 만들기 → 로비 진입으로 테스트. 스폰·이동·벽 충돌·경계·Esc 메뉴 정상.
- **문제**: 동벽 계단을 중간까지만 오를 수 있음.
  원인은 계단 모듈 `Basement_WallStairs`에 준 비균등 스케일 `(1, 1, 0.7)`. 모듈 안의 **회전된 박스 콜라이더는 비균등 스케일을 제대로 받지 못해** 경사로 콜라이더의 위 끝이 층계참(2.64 m)보다 0.8 m 낮게 끊기고, 계단 위 천장 콜라이더가 내려와 층계참 머리 공간이 1.1 m로 줄었음. 시각 메시(0.26 m × 0.20 m 10단)는 정상.
- **수정**(프리팹 `LobbyBasementEnvironment`): 모듈 안의 `WallStairs_Collider_Stairs`(경사로)·`WallStairs_Collider_Ceiling`(천장) 두 박스 콜라이더 비활성화. 스케일 영향이 없는 형제 오브젝트 `Walls/WallStairs_RampFix`에 계단 코 선(z -6.8 → -8.8, y 0 → 2.6)을 따르는 경사로 BoxCollider 추가(폭 0.91, 경사 52.4°, KCC MaxGroundAngle 60 이내). 검증: 바닥 0 → 층계참 2.64까지 연속, 머리 공간 3 m 이상.
- **지붕 관통 수정**: 천장 콜라이더를 끄자 계단에서 점프하면 지붕 메시를 뚫고 올라감. 지붕은 계단 FBX 메시에 합쳐진 나무 판재라 그대로 두기로 결정하고, 지붕 밑면 선(계단 아래 y 2.54 → 문 쪽 y 5.12)에 맞춘 경사 BoxCollider `Walls/WallStairs_RoofFix`(폭 1.42, 두께 0.15) 추가. 계단 위 머리 공간 2.1~2.9 m, 층계참 2.26 m로 서서 이동은 자유롭고 점프만 막힘.
- **교훈**: 팩 모듈에 비균등 스케일을 주면 회전된 자식 콜라이더가 어긋난다. 크기를 바꿔야 하면 콜라이더를 따로 만들거나 스케일 1로 두고 배치를 조정한다.

### 7. 조명 베이크 1차 (2026-09-07, 614)

- 에디터 메뉴 `Game/Lobby/Lighting/`(`LobbyLightingSetupMenu.cs`)로 설정·프로브·베이크를 한 번에 처리.
  1. Setup: 조명 설정 에셋 `Assets/_Game/Content/Lighting/LobbyLighting.lighting` 생성·연결 + 방 내부 라이트 프로브 격자 `LobbyLightProbes`(수평 1.5 m, 높이 0.3/1.5/3.0/4.8 m, 벽·소품 속 제외 → 321개)
  2. Bake: 라이트맵 + 리플렉션 프로브 2개 + 라이트 프로브 동시 베이크
  3. Clear: 베이크 데이터 삭제
- 설정: Progressive GPU, 20 texels/m, 최대 2048, Shadowmask, Directional, AO(0.5 m), 샘플 32/512/256, 바운스 3, 프로브 샘플 ×4. 팩 기본(40 texels/m, 4096)의 절반.
- 결과: 라이트맵 2장(2048), 산출물 `Assets/_Game/Content/Scenes/Lobby/`(약 25 MB, exr는 LFS). URP는 레거시 라이트 프로브 방식(APV 아님)이라 캐릭터·들고 다니는 소품은 라이트 프로브로 조명받음.
- 조명 25개: Realtime 9 · Baked 10 · Mixed 6. 렌더러 651 중 581이 ContributeGI.
- **재베이크 규칙**: 소품 이동·추가·삭제, 벽·천장 모듈 변경, 전등 변경, Static 플래그 변경(Carryable 전환 포함) 뒤에는 메뉴 2번으로 반드시 재베이크. 결과가 어둡거나 얼룩이 보이면 `LobbyLighting.lighting`의 샘플/해상도를 올려 다시 굽는다.
- 포스트프로세스(URP Volume: 비네트·색보정·블룸)는 아직 미적용. 분위기 조정과 함께 2차에서 진행.

### 8. 방 설정 상호작용 — 작전 계획판 (2026-09-07, 619~621)

- **결정**: 새 에셋 대신 서벽 작업대 위 공구판(`Basement_ToolBoard`, -2.98/1.35/-3.02)을 작전 계획판으로 재사용. 조준 후 F키로 방 설정 화면을 연다. 방장은 편집, 비방장은 읽기 전용(기존 설정 화면 규칙 그대로). 프롬프트는 방장 "방 설정 열기", 비방장 "작전 계획 보기".
- **로비의 상호작용 조건**: 캐릭터 프리팹에 `PlayerInteractor`가 있어 조준·F키는 로비에서도 동작하지만, 네트워크 브리지는 매치 씬에만 있음. 계획판은 로컬 UI만 열므로 동기화가 필요 없어 지금 가능. 소품 들기(615)는 동기화가 필요해 별도 결정 대기.
- **구현**
  - `IPlaySettingsOpener` — 월드에서 설정 화면을 여는 계약. `LobbyPauseMenuPresenter`가 구현: Esc→방 설정과 같은 상태(커서 해제·이동 잠금)로 열고, 월드에서 열었을 때는 닫으면 Esc 메뉴 대신 바로 게임으로 복귀.
  - `LobbyPlanBoardInteractable`(씬 컴포넌트, `IInteractable`) + `LobbyPlanBoardPresenter`(씬에서 계획판을 찾아 방장 여부·열기 동작 바인딩, `LobbyLifetimeScope`에 등록).
  - `IPlaySettingsView.RequestOpen()` 추가(닫기 요청과 대칭).
  - 테스트: `LobbyPauseMenuPresenterTests`(월드/메뉴 경로별 복귀 동작), `LobbyPlanBoardPresenterTests`(바인딩·프롬프트·1회 열기·해제).
- **배치**: 에디터 메뉴 `Game/Lobby/Place Plan Board (ToolBoard)`(`LobbyPlanBoardSetupMenu.cs`)가 공구판 렌더러 바운드를 재서 씬 루트 `LobbyPlanBoard`에 BoxCollider + 컴포넌트를 놓는다. 깊이는 벽 콜라이더(x -3.10~-3.00) 안쪽 면에서 1 cm만 나오게 고정 → 조준 광선은 벽보다 먼저 맞고, 위 모서리가 발판이 되지 않음. 환경 프리팹은 그대로라 **라이트맵 재베이크 불필요**.
- **확장 (2026-09-08, 619)**: 상호작용 대상을 공구판만에서 **공구판 + 책상(`Basement_Desk`) 전체**로 넓히고, 유저가 알아볼 수 있게 **주황 실루엣을 늘 켜 둔다**(`basement-plan-board-v2-outline.png`).
  - 콜라이더: 메뉴 이름이 `Game/Lobby/Place Plan Board (ToolBoard + Desk)`로 바뀌고, 두 소품 렌더러 바운드를 합쳐 x[-3.10(벽 바깥면), -2.29(책상 앞면+3 cm)] · y[0.81(상판 표면), 1.89(선반 밑면)] · z[-3.85, -2.15]로 놓는다. 책상 아래(플라스틱통·종이상자)와 선반 위(Fragile 상자)에 집을 수 있는 소품이 있어 위아래는 덮지 않는다. 선반 윗면 -2 cm까지 덮었을 때는 눈높이에서 올려 보는 광선이 앞면에 먼저 걸려 선반 위 상자를 못 집었고, 선반 밑면으로 내리자 해결. 에디트 모드 레이캐스트로 판 중앙·상판·선반 띠 → `LobbyPlanBoardInteractable`, 책상 아래 통·선반 위 상자 → `CarryableItem` 확인.
  - 실루엣: `InteractableFocusOutline`에 `sourceRenderers`(외부 렌더러 지정) 추가. 비어 있으면 기존처럼 자식 렌더러를 쓰므로 Carryable 쪽 동작은 그대로. 메뉴가 공구판 판·프레임(LOD0만, LOD1은 겹쳐 보여 제외)·책상 렌더러 3개를 연결하고, `LobbyPlanBoardInteractable.outline`에 참조를 넣는다. 실루엣은 프레젠터가 `Bind`한 동안 켜지고 `Unbind`/비활성화 시 꺼진다(집는 소품은 조준 시에만 켜지지만, 이 판은 방에 하나뿐인 설정 입구라 상시 표시). 실루엣 복사본은 원본 소품의 자식으로 런타임에 생기며 DontSave·정적 플래그 없음 → 라이트맵·배칭과 무관.
  - **정적 배칭 함정**: 소품이 Static이라 플레이 모드에서 Static Batching이 메시를 씬 단위 "Combined Mesh"로 합친다. 런타임에 `MeshFilter.sharedMesh`를 그대로 복사하면 결합 메시(바운드 11.8×5.9×7.1 m)가 복사돼 실루엣이 엉뚱한 곳에 그려지거나 안 보였다(에디트 모드 미리보기에서는 배칭이 안 일어나 정상으로 보였음). 해결: 메뉴가 에디트 시점의 원본 메시를 `sourceMeshes`에 함께 저장하고, 런타임 복사본은 그 메시를 우선 사용. 자식 렌더러 모드(Carryable)는 소품이 정적이 아니어서 해당 없음.
  - 테스트: `LobbyPlanBoardInteractableTests`(외부 소품에 실루엣 생성·Bind/Unbind 토글, 실루엣 없는 판도 동작). OnDestroy 정리는 에디트 모드에서 호출되지 않아 테스트 제외.
  - 환경 프리팹은 그대로라 **라이트맵 재베이크 불필요**.
  - **색·두께 (2026-09-08)**: 주황 2 px는 플레이 화면에서 너무 가늘어 `InteractableFocusOutline`에 `outlineColor`/`pixelWidth` 필드를 추가(기본값은 기존 주황 2 px라 Carryable은 그대로). 계획판은 메뉴가 **흰색 5 px**로 지정 — 조준 시에만 켜지는 소품(주황)과 상시 표시 설치물(흰색)을 색으로 구분.
  - **표시 방식 비교 (2026-09-08)**: 플레이 모드에서 런타임 값만 바꿔 A 검정 테두리 5 px / B 반투명 흰 막(URP Unlit Transparent, α 0.28) / C 검정 4 px + 약한 막(α 0.18) / D 공중 라벨을 비교. **결정: D + 흰 5 px 실루엣 유지.** 검정 테두리는 만화풍 외곽선처럼 보여 다른 소품과 톤이 어긋나고, 막은 원목·공구판 질감을 죽였다.
  - **공중 라벨 (2026-09-08, 619)**: `LobbyPlanBoard/Label` 자식에 3D TextMeshPro 두 개("ROOM SETTING" 2.1, "▼" 2.2, 폰트 Paperlogy-7Bold). 작업대 윗면 +0.55 m(y 2.54, 선반 위 Fragile 상자 2.24보다 위), 책상 중심 x/z. 라벨은 작업대 정면(방 안쪽 +X)을 향해 **고정**(처음엔 카메라를 따라 도는 빌보드였으나 플레이어 기준으로 움직이는 게 어색해 고정으로 변경). `LobbyPlanBoardLabel`은 화살표만 ±4 cm/1.2 Hz로 흔든다. 켜고 끄기는 `LobbyPlanBoardInteractable.label`이 실루엣과 함께 Bind/Unbind에 맞춰 처리(씬에는 꺼진 채 저장). 재질은 전용 에셋 `MAT_PlanBoardLabel.mat`(폰트 재질 복제 + Face HDR 1.8·Dilate 0.12·검정 외곽선 0.22) — 순백 1.0은 포스트프로세스 톤매핑에 눌려 회색빛으로 보였고, HDR 밝기로 블룸이 살짝 얹히며 또렷해짐. 런타임 재질 인스턴스를 씬에 남기면 참조가 깨지므로 에셋으로 만든 것. 테스트: `Bind_ShowsLabel_UnbindHidesIt`.
  - 라벨 문구는 방장/비방장 공통 "ROOM SETTING"(비방장은 읽기 전용). 한글 전환·방장별 문구는 필요 시 메뉴 상수만 바꾸면 됨.
  - **실제 입장 흐름에서 안 보이던 문제 (2026-09-08)**: Lobby 씬만 단독 재생하면 정상이었지만, Home→방 생성→로비 입장에서는 `bound=true`인데 실루엣·라벨이 꺼져 있었다. 로비 씬은 `NetworkRunnerService`가 백그라운드 프리로드(activation gate) 후 Fusion에 넘기는데, 그 사이 씬 오브젝트가 한 번 비활성/재활성되어 `OnDisable`이 숨긴 뒤 다시 켜 주는 곳이 없었다. `LobbyPlanBoardInteractable.OnEnable`에서 바인딩 상태에 맞춰 다시 표시하도록 수정. 검증은 에디터 플레이 모드에서 `IRoomBrowser.CreateAsync` + `NetworkRunnerService.EnterLobbyScene()`을 코드로 호출해 실제 흐름을 재현(MCP execute_code). **교훈: 로비 기능은 씬 단독 재생이 아니라 방 생성 흐름으로 검증해야 한다.**
- **테스트 중 관찰**: 동료 클라이언트가 옛 로비(창고) 씬을 갖고 있으면 그 플레이어가 지하실 벽을 통과해 밖에 서 있는 것처럼 보인다. 충돌은 각자 로컬 씬으로 계산하므로 같은 브랜치/머지 상태를 맞춰야 한다. 별개로 서벽 모듈 콜라이더는 높이 2.5 m까지만 있어 사물함 위 점프로 넘을 수 있으나, 벽에 붙인 경계 콜라이더가 밖으로 나가는 것은 막는다(필요 시 벽 상단 콜라이더 추가).

### 10. 조명 2차와 정리 (2026-09-07, 614·609)

- **그림자 아틀라스 경고 해결**: 팩 조명은 램프마다 베이크용 스팟 + 실시간 스팟(`_Dynamic`) 쌍이고, 형광등 포인트 라이트 4개(Mixed)가 그림자 맵을 6장씩 요구해 총 33장 → 2048 아틀라스 초과로 매 프레임 해상도가 깎이며 경고가 740회 이상 쌓였다.
  - 포인트 라이트 4개를 **Baked**로 전환(베이크 그림자는 유지, 실시간 그림자 맵 24장 제거) → 실시간 그림자 캐스터 11개/11장.
  - `PC_RPAsset` Additional Lights 그림자 아틀라스 2048 → 4096.
- **창문 태양광 냉색**: Directional Light(Mixed) 흰색 1.5 → `#CCE0FF` 1.3. 전구(#F5DBBF 계열) 온광과 대비.
- **포스트프로세스 — 실제로는 미적용(2026-09-14 확정)**: `LobbyPostProcess.asset`은 컴포넌트가 하위 에셋으로 저장되지 않아 커밋된 파일이 비어 있다(`components: [{fileID: 0}]`). 즉 톤매핑·블룸·비네트·색 보정은 로비에 **걸려 있지 않고**, 카메라의 SMAA Medium만 동작한다. 사용자가 현재 밝기·톤이 딱 좋다고 판단해 **이 상태를 그대로 유지**한다(효과를 채우면 가장자리가 어두워지고 하이라이트가 눌림). 빈 프로필과 `Lobby Post Volume`은 효과가 없어 그대로 두었다. 나중에 효과를 넣으려면 `VolumeProfile.Add<T>()` 뒤 `AssetDatabase.AddObjectToAsset(component, profile)`로 저장해야 한다(마트 `MartPostProcess`에서 한 방식).
- 위 변경(라이트 모드·색) 반영 재베이크.
- **정리**: 미사용 창고 에셋 팩 `Assets/IGBlocks/IG_Warehouse`(129 MB) 삭제, `Assets/_Recovery`(크래시 복구 씬) 추적 해제 + gitignore.
- 남은 것: 벽 상단 콜라이더(사물함 위 점프로 벽 위에 걸치는 현상이 실제로 보일 때), Unity MCP 패키지(`com.coplaydev.unity-mcp`)를 manifest에 포함할지 팀 결정.

## 다음 단계

1. 계단 재테스트(끝까지 오르기, 층계참에서 서기, 점프 시 지붕에 막히는지) → 이상 없으면 617 종료.
2. 조명 3차(필요 시): 베이크 결과 미세 조정, 포스트프로세스 수치 조정.
3. 상자·콘·타이어·가스통을 Carryable 프리팹 변형으로 전환(615, 로비 네트워크 동기화 여부 확인 후).
4. 계획판 다인 테스트(동료가 같은 브랜치 상태에서 방장/비방장 프롬프트·읽기 전용 확인). 플레이 모드에서 실루엣 두께·색이 충분히 눈에 띄는지 보고 조정(619). 필요 시 계획판에 지도·메모 데칼로 시각 연출.
