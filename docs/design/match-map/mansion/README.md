# 매치 맵 2 — 저택 (Synty POLYGON Horror Mansion)

두 번째 매치 맵. 마트(`../README.md`)와 같은 절차로 가져오며, 마트에서 만든 도구(`Game/Match Map/…`)를 재사용한다.
맵 id `mansion`, 씬 `Assets/_Game/Content/Scenes/Mansion.unity`, 브랜치 `feature/client/match-map-mansion`.

## 제작 경과

### 1. 임포트·URP 검증 (2026-09-17)

- 팩 위치: `Assets/Synty/PolygonHorrorMansion/` (119 MB). 로컬 테스트 폴더(`Assets/LocalMapAssets`, git 제외)에 받아 둔 것을 마트 팩과 같은 추적 폴더로 옮겼다. 구성: FBX 691 · 프리팹 680(Buildings 208 · Props 333 · Environment 96 · Characters 20 · FX 14 · Weapons 9) · 재질 181 · 텍스처 171.
- **URP**: 재질 181개 중 153개가 공용 `Synty/Generic_Basic`(이미 추적 중인 `PolygonGeneric`), 4개가 `Generic_Decals`. 팩 자체 셰이더 그래프 `SyntyStudios_Grunge_01`(12개)·`Ghost_01`(1개)은 URP·BIRP 타깃을 둘 다 가진다. 내장 Standard 재질은 없어 **핑크 재질 위험 0**, 변환 불필요. 11개는 내장 기본 셰이더(스카이박스·파티클 계열)로 별도 확인.
- **FBX 임포터**: 라이트맵 UV(`generateSecondaryUV`) 전부 꺼짐, Read/Write는 절반가량 꺼짐(표본 50개 중 35개). 마트와 같은 교훈: 상호작용 대상(Carryable·파쇄기·문)은 BoxCollider로 대체하거나 해당 FBX만 Read/Write를 켠다. `Models/Collision/`에 전용 충돌 메시 472개(볼록 `Convex/` 포함) — 마트처럼 가구 안쪽이 막히면 시각 메시 콜라이더로 바꾼다.
- **데모 씬** `Scenes/Demo.unity`: 프리팹 인스턴스 4,235개(고유 509종), 라이트 40개(포인트 34·스팟 2·디렉셔널 4, 전부 Realtime), 베이크 데이터 있음, 안개 켜짐(파랑, 지수 0.01), 환경광 스카이박스 0.5, 후처리 볼륨 `HorrorMansion_demo`(어두운 공포 톤). `Lighting (URP)`·`Lighting (BIRP)` 두 묶음이 함께 들어 있다(BIRP 묶음은 우리 씬에서 제거).
  - 그룹별 범위: `Building`(본관 1층·지하) x -26~21, z -54~-7 → **약 47×47 m**; `Upstairs_Layer`(2층) x -12.5~12.5, z -32~-7; `Roof_Layer` y 최고 13 m; `Environment`(뜰·담·나무) x -48~40, z -69~66; `Background`(원경) ±200 m.
  - 마트(52×75 m)보다 조금 작지만 2층·지하가 있어 동선은 더 입체적.
- **씬 파일**: 데모를 그대로 복제해 `Mansion.unity`로 만들었다(원본 데모 씬은 수정 안 함). 이후 `Game/Match Map/Mansion/1. Setup Match Objects From Supermarket`로 마트의 매치 오브젝트(`MatchLifetimeScope`, `Main Camera`, `Shredder_A/B`, `SpawnPoints` 10, `WaitingSpawnPoints` 10, 후처리 볼륨)를 복사해 심는다. 스폰·파쇄기는 앞뜰 임시 격자 위치라 사용자가 옮긴다.

### 2. 맵 등록 `mansion` (2026-09-17)

- `MapCatalog.MansionId = "mansion"`, 로비 선택 목록(`LobbyMapIds`)에 마트 다음으로 추가. 기본 맵은 마트 그대로.
- `NetworkScenes.asset` `_mapScenes`에 mansion → `Mansion.unity`, 빌드 목록에 Supermarket 다음으로 추가.
- 미리보기 사진은 `Game/Match Map/Preview/Capture Map Preview From Main Camera`로 `Mansion.unity`의 Main Camera 시점을 찍어 `Resources/UI/Maps/MapPreview_mansion.png`에 저장한다.

### 3. 매치 오브젝트와 대기 구역 (2026-09-17)

- 파쇄기: 사용자가 배치한 팩 소품 `SM_Prop_Furnace_01` 2개(화로)를 `Game/Match Map/Mansion/2. Adopt Selected Objects As Shredders`로 `Shredder_A`(1층 (-1.4, 1.1, -10.1))·`Shredder_B`(2층 (-0.2, 6.1, -25.1))로 감쌌다. 루트에 `ShredderInteractable` + 렌더러 크기 BoxCollider(1.13×2.42×0.88), 자식 `ShredderSpot`·`ShredderTarget`. 마트의 PurpleBear 외형은 쓰지 않는다.
- `1. Setup Match Objects From Supermarket`로 마트의 `MatchLifetimeScope`·`Main Camera`·`SpawnPoints`(10)·`WaitingSpawnPoints`(10)·후처리 볼륨을 복사(이미 있는 파쇄기는 건너뜀). 데모 `Lighting (BIRP)`·데모 카메라 삭제. **배경은 원본 데모 그대로**: 데모 `Global Volume`(밤 톤 색 보정·비네트)을 켜 두고 마트 볼륨은 넣지 않는다(`4. Restore Demo Look (Volume)`로 되돌릴 수 있음). 안개·환경광·스카이박스는 복제 시점부터 데모와 동일.
- 스폰: 1층 5개(y 1.4, z -16~-26)·2층 5개(y 6.1). **대기 구역은 3층 다락**(대기 스폰 10개, y 8.9, x -2.6~1.3, z -17~-21).
- **다락 밀폐 검증(2026-09-17)**: 마트의 `WaitingBound_*` 같은 막음 콜라이더가 **필요 없다**. 스폰 중심에서 0.5 m 격자로 보행 가능 영역을 넓혀 간 결과(점프 0.9 m·구 캐스트 무릎/가슴 높이) 다락 약 256 m²(x -10.7~10.9, z -28.5~-11.0, 바닥 y 8.51~9.63)만 닿고 내려가는 가장자리가 0곳이었다(유일한 1.1 m 낙차 2칸은 상자 더미 위→바닥). 엎드린 자세(높이 0.3 m 구 캐스트)로도 같은 범위. 데모 저택에는 다락으로 오르는 계단 자체가 없어(계단은 1층↔2층만) 대기자는 텔레포트로만 드나든다. 다락 북쪽 z > -11 처마 밑 띠와 x 3.5~7.5·z -9~-11.5의 2층 뚫린 구간은 경사 지붕에 막혀 닿지 않는다.
- 데모 `Global Volume`(어두운 색 보정)은 **꺼 둠**(2026-09-17): Scene 뷰에서 보이는 밝기를 Game에서도 그대로 쓰기로 함. 안개·전등·환경광은 데모 그대로.
- 지하실은 플레이 구역이 아니라 **이 맵 전용 엔딩(결과) 무대**로 쓴다(아래 4절).

### 4. 맵 전용 결과 씬 `MansionResult` (2026-09-17)

- 결과 흐름은 매치 씬 위에 결과 씬을 추가 로드하고 `EndingStage`를 전용 카메라로 찍는 방식(`docs/design/ending/README.md`). 저택은 유치장 대신 **지하실**을 무대로 쓰기 위해 맵별 결과 씬을 도입했다.
- 코드: `NetworkScenes._mapResultScenes`(mapId → 씬) + `ResultSceneFor(mapId)`(없으면 기본 `ResultScene`)·`IsResultScene(SceneRef)`. `NetworkRunnerService.EnterResultScene`은 `AnalyticsMapId`의 결과 씬을 올리고, 하이라이트 전 언로드와 `OnSceneLoadDone`의 결과 단계 판정(`IsResultSceneLoaded`), 호스트 이관 시 결과 단계 복원도 맵별 씬을 본다. 마트·놀이터는 기본 결과 씬 그대로.
- 씬: `MansionResult.unity`는 `Result.unity` 복제본(`Result Lifetime Scope`·`Main Camera` 유지)에 **사용자가 지하실 조각(프리팹 78개)·스포트라이트 4개를 직접 옮겨 방을 꾸민 것**이다(높이 판정 자동 분리 도구는 뜰 나무·1층 바닥과 경계가 애매해 쓰지 않고 지움). `Game/Match Map/Mansion/3. Wire Ending Stage In MansionResult`가 유치장 무대를 지우고, 스코프·Main Camera를 제외한 모든 루트를 `EndingStage/Basement` 아래로 묶고, 사용자가 놓은 Main Camera 자리를 카메라 앵커로 삼아 전용 `EndingCamera`(같은 화각)를 만들고, 카메라 정면 4 m·7 m 바닥에 탈출 자리 6·체포 자리 6을 임시로 놓은 뒤 스코프에 연결한다. 자리·앵커는 씬에서 옮긴다.
- 스코프가 결과 씬의 다른 카메라·라이트를 끄므로 지하 조명은 반드시 `EndingStage` 아래에 있어야 한다(위 메뉴가 함께 묶는다).
- **무대는 y −300에 둔다**(유치장 무대와 같은 오프셋). 결과 씬은 매치 씬 위에 추가 로드되므로 방을 원래 좌표(저택 1층과 겹침)에 두면 엔딩 카메라가 1층 복도 벽을 찍는다(2026-09-17 개발 서버 테스트에서 확인: `MansionResult` 로드·스코프 준비는 정상, 화면만 1층 소품). 연결 메뉴가 묶은 뒤 `EndingStage`를 −300으로 내리고, 앵커·자리는 그 기준으로 놓는다. 최종 구도: 카메라 서쪽 홀 (−5.0, 2.22, −23.2)+오프셋에서 동쪽 철창을 8° 내려봄(화각 55), 탈출 줄 복도 x −2.2, 체포 줄 감방 안 x 0.75(사용자 조정 후 저장). 플레이 씬 `Mansion.unity`의 지하는 그대로 두었고(지하 계단·출입구 막음은 사용자 결정), 결과 씬의 방은 별도 복사본이다.
- 빌드 목록·`NetworkScenes.asset`에 `MansionResult` 추가. 테스트 `NetworkScenes_MansionUsesItsOwnResultSceneAndOtherMapsFallBackToDefault`.

### 5. 들 수 있는 소품 전환 준비 (2026-09-17)

마트 도구를 그대로 쓰되 저택 팩에 맞게 손본 것:
- **FBX Read/Write**: `Assets/Synty/PolygonHorrorMansion/Models/**/*.fbx` 593개의 임포터 `isReadable`을 켰다(마트 교훈: 꺼진 채로 분해하면 카탈로그를 못 읽어 전부 생성 프리팹이 됨. 메시 콜라이더 조준에도 필요).
- **합쳐진 소품 분해**(`MergedPropExplodeMenu`): 카탈로그 폴더에 저택 `Prefabs/Props` 추가. 분해 대상 이름에 `_Set`(접시 세트)·`Book_Line`(책꽂이 한 줄)을 더해 기존 `_Pile`·`_Stack`과 함께 잡는다. 저택 씬의 대상: `Book_Pile_01~05` 29개, `Plate_Set_01` 6, `Paper_Stack_01` 5, `Can_Stack_01` 3, `Box_Pile_01~02` 2, `Book_Line*`.
- **Carryable 전환**(`MartCarryableSetupMenu`, 메뉴 `Game/Match Map/Carryable/`): 열린 씬이 `Mansion`이면 변형 프리팹을 `Prefabs/Carryable/Mansion/`에, 보고서를 `docs/design/match-map/mansion/carryable-props.md`에 쓴다. 저택 전용 키워드 — 고정 추가: 그림·액자·커튼·러그·천·데칼·문양·전기함·족쇄·샹들리에·횃불·사다리·서랍장·옷장·침대·욕조·벽난로·화로·피아노·관·비석·조각상·장작 더미·북엔드. 들 수 있음 추가: 책·양초(낱개)·잔·접시(낱개)·캔·상자·병·항아리·가방·가면·두개골·열쇠·숟가락·팬·화분·장식 천·서류·그릇·컵·금고·오르골·인형·휴지. 마트 제외 목록의 `Papers`는 저택에서 해제.
- 실행 순서(Unity): ① `Game/Match Map/Rebuild Explode Catalog` → ② Mansion 씬에서 `Explode All Merged Props In Scene (Background)` → ③ `Carryable/1. Report Targets`로 보고서 확인·키워드 조정 → ④ `2b. Convert Targets (Background)` → ⑤ `3. Apply Static Batching To Fixed Props` → ⑥ `4. Fix Furniture Colliders (Non-Convex)`(저택 팩도 `Models/Collision/Convex/*_Convex.asset` 구조가 같다) → ⑦ `5. Reachability Report`.
- 경계(`MansionEnvironment/Boundary`) 콜라이더가 아직 없어 경계 판정 없이 전체 씬을 대상으로 한다. 뜰 소품까지 들 수 있게 되니, 플레이 구역을 정하면 경계를 두고 다시 보고서를 뽑는다.

### 6. 경계·이동 검증 (2026-09-17)

- **본관은 자체로 밀폐**: 1층 스폰(`SpawnPoint_1`)과 2층 스폰(`SpawnPoint_6`)에서 0.5 m 격자 보행 탐색(점프 0.9 m, 무릎 0.7·가슴 1.3 m 구 캐스트)을 돌린 결과 본관 밖(뜰)으로 나가는 칸 0, 지하 칸 0, 1 m 넘는 낙하는 실내 단 차이(계단 옆·현관 단)뿐. 바깥으로 통하는 문 3곳(정문 양문 (−0.1/−2.4, 1, −9.5), 남쪽 (−10.7, 1, −32), 동쪽 (12.5, 1, −18.8))은 문짝(BoxCollider)이 닫혀 있고 창은 콜라이더가 있어 **막음 콜라이더 없이도 나갈 수 없다**.
- 도달 범위: 1층 약 345 m² + 2층 약 224 m²(트리밍 후 본관 x −13.5~13.7, z −32.5~−6.4). 문틀 벽 모듈(`SM_Bld_Base_Wall_Door_01` 등 22개)은 콜라이더 보정(시각 메시) 뒤 통로 1.1 m가 열려 있음을 광선으로 확인. 양문 틀(`Wall_Door_Double_01`)은 콜라이더 없음.
- **닫힌 실내 문짝**(플레이어가 열 수 없음 → 방이 막힘): 1층 `SM_Bld_Door_05` (−1.8, 1, −24.5), 1층 양문 (0.6/1.9, 1, −29.5), 2층 양문 5쌍 — (0.6/1.9, 5.5, −29.5), (−6.9/−5.5, 5.5, −29.5), (8.1/9.4, 5.5, −29.5), (12.5, 5.5, −16.4/−15.1), (12.5, 5.5, −23.9/−22.6), (−10.6/−12, 5.5, −12), 비밀 책장문 (10, 1, −29.5). 열어 둘지는 사용자 결정(씬에서 문짝을 힌지 기준으로 돌려 두면 됨). 동쪽 x 12.5·북쪽 z −12의 2층 양문은 발코니로 나가는 문이라 열면 낙하 검증을 다시 한다.
- **경계·대기 구역 콜라이더(사용자 조정 후 최종, 2026-09-17)**: `MansionEnvironment/Boundary` — 벽 `Boundary_West/East` x −13.0~−12.5 / 12.7~13.2, `Boundary_South/North` z −32.7~−32.2 / −9.3~−8.8(정문 앞 현관 제외), 높이 y −3~16; 판 `Boundary_Ceiling` y 11.9~12.4, `Boundary_Floor` y 0.2~0.7(1층 바닥 1.01 아래 → **지하 차단**), `Boundary_AtticFloor` y 8.15~8.45(2층↔다락 차단, 다락 바닥 8.51 아래). `MansionEnvironment/WaitingArea` — `WaitingBound_West/East` x ±12.0~12.5, `South/North` z −30.5~−30.0 / −10.0~−9.5, 높이 8.0~11.8, `Ceiling` 11.3~11.8, `Floor` 7.7~8.2. 대기 스폰 10/10이 안에 있고, 재검증에서 1층 1,379칸·2층 1,294칸·다락 1,023칸 모두 밖으로 나가는 칸 0. 2층 남서쪽 단(y 6.86, 약 6 m²)은 다락 바닥까지 1.65 m뿐이라 원래도 서 있을 수 없는 곳이며 판 때문에 탐색에서 빠진 것은 문제 없음. 마트처럼 Carryable 보고서·도달 검사의 "경계 안" 판정에도 쓰인다.
- **조준 막힘 원인 1건(2026-09-17 플레이 확인)**: 비밀 책장문 `SM_Bld_Bookshelf_Door_01`(1층 (10, 1, −29.5))은 책장 전체를 덮는 BoxCollider 하나라 그 안에 놓인 책·소품 34개가 조준 광선에 닿지 않았다(같은 높이의 다른 책장은 잡히는데 이 책장만 안 잡힘). 콜라이더 도구(`4. Fix Furniture Colliders`)에 "들 수 있는 물건을 상자 안에 품은 고정 가구의 BoxCollider → 시각 메시 non-convex MeshCollider" 규칙을 추가하고 씬에 적용했다(책장문·벽 찬장 `Wall_Cupboard_01`·벽 벤치 `Wall_Bench_01` 3개).
- **단독 테스트 구성**: `PlayerCharacter` 프리팹 인스턴스(1층 `SpawnPoint_1` 자리)와 `PlayerCameraRig` 프리팹 인스턴스를 두고, `MatchLifetimeScope`의 Auto Inject 목록에 캐릭터를 등록, `Game/InGame/Build HUD Layout`으로 `InGameHud`를 만들어 스코프의 HUD·보이스 뷰를 연결했다(없으면 스코프 조립이 `IVoiceControl` 미등록으로 실패하고 씬 카메라만 보인다). Playground·마트와 같은 구성이며 네트워크 매치에는 쓰이지 않는다. 플레이 확인: 1층→2층 계단 이동 정상.
- 1층→2층 계단은 플레이로 정상 확인(탐색이 층계참에서 멈춘 것은 탐색 반지름 한계).

### 7. 1층 하이라이트 CCTV (2026-09-18, S15P21D205-1082)

- 마트처럼 `Resources/CCTV/Mansion.prefab` 을 씬 이름으로 자동 로드한다. 지점 24개, 전부 1층(y 4.01, 바닥 위 3.0 m), 시야각 65도, 허리 높이(바닥 위 0.7 m)를 내려보는 마트 각도.
- 도구 `Game > Highlight > Mansion CCTV` (`Assets/_Game/Editor/MansionCctvPlanner.cs`): ① `1. Bake 1F Map` 이 y 4.4 에서 내려 쏜 광선으로 0.25 m 격자를 바닥·가구·벽으로 나누고 스폰·파쇄기·열어 둔 방 씨앗에서 걸어 닿는 칸을 표시해 `cctv-1f-map.png` 로 쓴다. ② `2. Auto Place` 가 벽 0.75 m 안쪽 후보 × 방향 24 × 초점 거리(3·4.5·7 m) 가운데 사각을 가장 많이 줄이는 지점을 탐욕으로 고르고 교환으로 다듬어 `cctv-1f-autoplace.md` 에 좌표표를 쓴다(앞 2개 파쇄기 카메라는 고정). ③ 좌표표를 `Mounts` 에 옮겨 이름을 붙이고 `3. Check Coverage` 로 `cctv-1f-coverage.png/.md` 를 뽑는다. ④ `4. Save Mansion CCTV Prefab`.
- 가림 판정은 런타임 `HighlightCameraDirector` 와 같은 정적 렌더러 경계 상자(높이 0.5 m 이상, 카메라 원점을 품은 상자는 무시)라, 하늘 돔처럼 맵 전체를 덮는 상자는 영향이 없고 문틀 벽 모듈 상자는 문 구멍을 막는다.
- 결과: 걸어 닿는 5,365칸(335 m²) 사각 0, 두 대 이상 62%. 피아노·책장 상자 안쪽 42칸은 규칙상 볼 수 없는 자리. 상세는 `../../../highlight-cctv-rules.md` 의 저택 절.
- 전제: 1층 문은 전부 열어 두고 비밀 책장문은 통과 가능(사용자 결정). 2층은 임시 구조라 플레이 구역이 아니며 CCTV 도 없다. `Shredder_B` 는 2층에 남아 있다.

## 열어 둔 결정

- 플레이 구역은 본관 1·2층(지하는 엔딩 무대, 다락은 대기 구역). 앞뜰까지 열지와 경계 콜라이더는 사용자가 배치.
- 분위기: 조명·안개는 데모 그대로, 어두운 후처리(`Global Volume`)만 꺼서 Scene 뷰 밝기로 확정(2026-09-17).
- 탈출 지점 위치. 닫힌 실내 문짝을 열어 둘지(6절 목록). 파쇄기 2대는 화로로 확정, 다락·본관 밀폐는 검증 완료.
- 들 수 있는 소품 분류(Props 333종) — 마트 Carryable 메뉴 재사용.
