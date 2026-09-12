# 매치 맵 (마트) — 제작 경과

메인 매치 맵을 마트 테마 로우폴리로 새로 만든다. Jira 에픽 **S15P21D205-901 마트 맵 세팅**, 태스크 902~915.
태스크 상세·순서는 [../../planning/match-map-mart-jira-tasks.md](../../planning/match-map-mart-jira-tasks.md).

- 파일명은 영문 소문자와 하이픈. 예: `synty-shops-demo-market-aisles.png`
- 작업 브랜치 `feature/client/match-map-mart` (develop 기준)

## 이미지 목록

- `synty-shops-demo-mall-overview.png` — Shops Pack 데모(쇼핑몰) 기본 카메라 시점. 2층 몰·에스컬레이터·분수
- `synty-shops-demo-market-aisles.png` — 데모 안 슈퍼마켓 구역 눈높이. 진열대(Aisle) 5열·계산대·천장 안내판
- `synty-shops-demo-market-high.png` — 같은 구역을 위에서. 진열대 배열·카트·냉장 진열장 위치 참고
- `synty-plaza-demo-overview.png` — Shopping Plaza Map 데모 전경. 도심 블록 위 매장 7개 + 주차장(사전 조립 맵)
- `mart-boundary-colliders.png` — 사용자가 직접 배치한 경계 콜라이더 15개(연두 와이어). 마트 구역(ㄱ자 형태)을 벽면마다 상자로 둘러싼 최종 배치. 원점 재배치 이후 좌표
- `mart-boundary-shell.png` — (참고용, 폐기) 자동 생성 외곽선 추종 경계 시안

## 제작 경과

### 1. 에셋 선정·구매 (2026-09-10)

- 후보 검색 기준: 컨셉(도둑 아지트·경찰 도착·탈출), 숨길 곳 많은 실내, URP 지원, 기존 캡슐 캐릭터와 어울리는 스타일라이즈드.
- **결정**: Synty **Value Bundle — POLYGON Shops Pack + POLYGON Shopping Plaza Map**. 마트(슈퍼마켓) 테마.
  - 번들 상품을 임포트하면 `Assets/ShopsValueBundle/ReadMe.txt`만 들어오고, 실제 팩 2개는 Package Manager > My Assets에서 각각 임포트해야 한다.
  - Shops Pack: 228 MB, v1.6.6(2026-08-18). 프리팹 1,933(스토어 표기) / 실제 임포트 1,957. 슈퍼마켓 통로·계산대·냉장/냉동 진열장·청과대, 모듈러 건물, 간판 295, 캐릭터 14, 데모 쇼핑몰 씬.
  - Shopping Plaza Map: 126 MB. Shops Pack에 의존하는 사전 조립 도심 블록 맵(매장 7개·화장실 2·주차장).
- **라이선스**: Restricted Single Entity. 추가 제한 2개 — (1) 사용자 제작 콘텐츠(UGC)가 주목적인 앱에서의 수익화 금지, (2) NFT 발행·거래 상품 금지. 우리 게임은 정해진 규칙의 파티게임이라 해당 없음. 팀 내 같은 프로젝트 사용은 단일 조직 사용으로 봄. 원본 .unitypackage는 저장소 제외(.gitignore `*.unitypackage`).

### 2. 임포트·URP 검증 (2026-09-10, 902)

- 임포트 결과: `Assets/Synty/` 427 MB — `PolygonShops`(프리팹 1,957·재질 62), `PolygonMapsPlaza`(프리팹 34·재질 14), `PolygonGeneric`(공용, 프리팹 495·재질 128), `SyntyPackageHelper`(에디터 도구).
- **URP**: 재질은 Synty 자체 셰이더 그래프(`Synty/Generic_Basic` 대부분, `Generic_Decals`·`Generic_Standard`·파티클용)를 쓰고 프로젝트 URP 에셋(`PC_RPAsset`)에서 **핑크 재질 0개**. 변환 작업 불필요.
- **텍스처**: 팩 공용 팔레트/아틀라스 2048² 다수(예: `PolygonShops_Building_Brick_Coloured_01`). 소품은 팔레트 한 장을 공유하는 전형적 Synty 방식 → 로비 톤 통일(912)에서 로비 쪽을 팔레트 방식으로 맞추는 근거.
- **프리팹 분류(Shops Pack)**: Buildings 167 · Characters 34 · Environments 39 · FX 5 · Food 298 · Generic 28 · Products 93 · Props 977 · Signs 295 · Vehicles 3 · Weapons 18.
  - 마트 핵심 소품 `SM_Prop_Market_*` 77개: 진열대 Aisle 01~05(+End·Preset 조합), 계산대 Checkout(Large·Shelf·Light·Divider), Deli Fridge 01~02(+Insert), Drinks Fridge 01~03(+Insert), Freezer 01~03(+Insert·Shelf), Food Display 01~05(+Insert), Fruit Basket/Bowl, PriceTag.
  - 진열 상품은 `Insert` 프리팹으로 분리돼 있어 빈 진열대·채운 진열대를 선택 가능 → 숨김 장소(빈 칸) 설계에 유리.
  - 그 외 접두사: Prop_Food 297 · Product 94 · Cafe 89 · Clothes 88 · Computer 76 · Shop 56 · Kitchen 51 · Warehouse 23(창고·팔레트·상자) · Poster 20 · Lighting 38.
- **콜라이더 점검**
  - Props 977개 중 962개에 콜라이더, 대부분 **MeshCollider(convex)**, BoxCollider는 20개. Buildings는 Box 124·Mesh 44. 비균등 스케일 프리팹은 Buildings 1개만(로비 계단 교훈 대비 양호). LODGroup 없음.
  - FBX 임포터 `Read/Write`가 꺼져 있음(`isReadable=False`). 로비에서 확인한 대로 Read/Write 꺼진 convex 메시 콜라이더는 플레이 중 조준 광선에 잡히지 않을 수 있다 → **상호작용 대상(Carryable·파쇄기·열리는 문)은 BoxCollider로 대체**(로비 Carryable 메뉴 3번 방식) 또는 해당 FBX만 Read/Write 켜기.
  - 라이트맵 UV 생성(`generateSecondaryUV`)이 꺼져 있음 → 베이크(911) 전에 모듈·가구 FBX에 켜거나, Synty 방식대로 라이트맵 없이 실시간+프로브로 갈지 결정.
- **문짝 분리 여부(열리는 가구 가능성)**
  - `SM_Prop_Market_Drinks_Fridge_01~03`: `..._Door_01`(+Glass) 자식으로 문이 **별도 메시** → 열기 상호작용 가능(피벗 확인 필요).
  - `SM_Prop_Market_Deli_Fridge`, `Freezer`, `Hunting_Cabinet`, `Mall_Kiosk_Cabinet`: 유리만 별도, 문 없음(개방형).
  - → 열리는 가구 태스크(문서 T7, 미발급)는 음료 냉장고 3종을 대상으로 범위를 잡을 수 있다.
- **데모 씬**: `PolygonShops/Scenes/Demo.unity`(쇼핑몰·소형 매장·버거·사냥용품점·주차장, 렌더러 10,709), `Demo_Lite`, `Overview`; `PolygonMapsPlaza/Scenes/Demo.unity`(도심 블록, 렌더러 11,424). 슈퍼마켓 구역은 Shops 데모의 `Mall_Downstairs_Props` 아래 (18, 1, -5) 부근, 진열대 25개 밀집.
- **다음(903)**: 데모의 슈퍼마켓 구역(약 8×9 m 진열대 블록 + 계산대)을 참고해 우리 규모(로비 2~3배, 6인)로 레이아웃 도면 작성. 창고(`Prop_Warehouse`)·하역장·사무실·화장실(Plaza의 Toilet 프리팹 세트) 존을 붙인다.

### 3. 조립 씬과 에디터 도구 (2026-09-10, 904)

- 조립 씬 `Assets/_Game/Content/Scenes/Supermarket.unity`. 데모 씬(쇼핑몰·플라자)에서 필요한 매장을 복사해 와 조립(루트 오브젝트 약 1만 개, 팩 프리팹 인스턴스). 씬 안에서 필요한 구역만 골라 온 것이라 원본 데모 씬은 그대로.
- **합쳐진 소품 분해** `Game/Match Map/Explode Merged Props…` (`MergedPropExplodeMenu.cs`): Synty의 Preset/Insert/Stacked 프리팹은 진열대+상품, 상자 더미가 한 메시라 플레이어가 개별 상품과 상호작용할 수 없다. 메시를 정점 위치 용접→연결 조각으로 나눠 팩의 개별 프리팹과 형태 매칭해 프리팹 인스턴스로 세운다.
  - 매칭: 회전 불변 지표(정점 수·중심 거리 분포)로 후보를 고르고 Y회전 탐색 → 실패 시 임의 3D 회전(PCA 초기값 + ICP). 여러 조각 프리팹(꽃다발=화분+꽃, 파인애플=과육+잎, 팔레트)은 가장 큰 조각으로 위치·회전을 잡고 나머지 조각이 예측 위치에 있는지 검증해 통째로 잡는다.
  - 팩에 없는 모양(인서트 전용 병·캔·상자)은 조각 지오메트리로 생성 프리팹을 만들어 `Assets/_Game/Content/MatchMap/GeneratedProps/`에 저장·재사용(BoxCollider 포함).
  - 결과는 `<원본>_Exploded/{Structure, Products}`. 구조물 판정: Products/Food 폴더는 상품, 생성 프리팹은 크기(0.6 m/0.03 m³), 팩 Props는 키워드(Stand·Shelf·Rack·Aisle·Checkout·Fridge·Freezer·Display·Pallet·Trolley·Cart·Counter·Table·Cabinet·Kiosk) 또는 1.2 m.
  - 일괄 처리는 `(Background)` 메뉴(에디터 틱마다 하나씩, 에디터가 멈추지 않음). `Revert All Exploded`로 되돌리기, `Reclassify`로 분류만 재적용.
  - **필수 설정**: Synty 모델 FBX의 Read/Write를 켰다(1,965개). 꺼진 상태에서 대량 처리하면 Unity가 CPU 메시 데이터를 해제해 카탈로그를 못 읽고 전부 생성 프리팹이 되는 사고가 있었다. 메시 콜라이더 조준(907)에도 필요.
  - 결과(씬 전체 243개): 상품 10,607개(팩 프리팹 1,684·생성 8,923), 구조물 1,287개, 생성 프리팹 511종. 꽃다발 54·파인애플 32·창고 상자 46은 팩 프리팹. 팔레트 스택은 판재로 부서져 제외(`Pallet` 이름). 양배추 일부는 크기 변형으로 미매칭 → 수동 처리.
  - 성능 주의: 활성 렌더러 약 2.4만 개. 906/907에서 상호작용 물건만 남기고 나머지는 Static 배칭.
- **인접 복제** `Game/Match Map/Duplicate Adjacent/…` (`DuplicateAdjacentMenu.cs`): 선택 조각을 로컬 축 방향으로 자기 크기만큼 옆에 복제(Alt+Shift+방향키, PgUp/PgDn). 천장·바닥 타일 붙이기용, 프리팹 연결 유지.
- **선택을 프리팹으로** `Game/Match Map/Make Prefab From Selection…` (`MakePrefabFromSelectionMenu.cs`, Ctrl+Shift+Alt+P): 선택 묶음을 `Assets/_Game/Content/Prefabs/Mart/<이름>.prefab`으로 저장·연결. 피벗은 바닥 중앙, 종류별(Modules/Env/Props) 하위 그룹 옵션.
- 로비 로우폴리 변환기(`LobbyLowPolyTestMenu.cs`)도 912 대비로 함께 보관.

### 4. 콜라이더·경계 1차 검증 (2026-09-10, 905) — 자동 경계는 되돌림

> **결정(2026-09-10)**: 아래에서 만든 자동 경계 상자(254개)와 벽 콜라이더 보정 2개는 사용자 요청으로 **모두 제거**하고 씬을 콜라이더 작업 전 상태(커밋 61911520)로 되돌렸다. 경계·콜라이더는 사용자가 세운 벽과 직접 지정한 콜라이더만 쓴다. 아래 내용은 검증 결과와 절차 기록으로만 남긴다.
>
> **최종 경계(사용자 배치, 2026-09-10)**: `MartEnvironment/Boundary` 아래 BoxCollider 15개 — 서·남·북 1면씩, 동쪽은 ㄱ자 꺾임을 따라 세운 벽 상자 10개, 천장·바닥 판 2개. 두께 2 m, 높이 y −0.5~7. 원점 재배치(+108.49, 0, −0.98) 이후 좌표 기준이며 스크린샷 `mart-boundary-colliders.png`.
>
> **원점 재배치(2026-09-10)**: 씬 뷰에서 보던 마트 안 지점을 (0, y, 0)으로 두기 위해 씬 루트 10,192개를 X·Z만 (+108.49, −0.98) 이동(바닥 y 유지). Main Camera는 그 시점에 정렬. 이 절 아래의 이전 좌표(x −133.5 등)는 옛 기준이다.

- 플레이 구역: 사용자가 마트 구역을 벽으로 막아 둔 상태. 구역 안 기준점(-106.8, -14.7)에서 플레이어 크기(폭 0.4 m·높이 1.1 m 상자, 0.5 m 격자)로 걸어갈 수 있는 1층 바닥을 BFS로 탐색한 결과 **x[-133.5, -87.5] × z[-27, 9.5] (약 46×37 m, 905 m²)로 닫혀 있고 밖으로 새는 곳 없음**. 구역 안에 계단·에스컬레이터·사다리는 없음.
- 모듈 콜라이더: 구역 안 벽 347개 중 2개(서쪽 끝 x -133.65/-136.15)만 콜라이더가 없어 BoxCollider를 붙였고, 바닥 431·유리벽 133·문 10·기둥 62는 모두 콜라이더 있음. 천장은 구역 안에 5개(서쪽 x -136, 구역 밖)만 콜라이더 없음.
- 천장: 대부분 5.5~5.9 m, 일부 3.0 m, 열린 곳(하늘) 몇 군데. 낮은 벽(상단 2.0~2.9 m) 10개가 있어 선반(2.1 m) 위에서 넘어갈 수 있으므로 경계 콜라이더가 필수.
- **경계 콜라이더(외곽선 추종)**: 구역이 직사각형이 아니라(꺾임·돌출 있음) 큰 6면 상자 대신, 도달 영역 R의 바깥 셀 중 R에 인접한 "껍질" 셀(0.5 m)마다 상자를 놓고 행 단위로 이어 붙였다 → `MartEnvironment/Boundary` 아래 벽 상자 254개(높이 y −0.5~7) + 천장·바닥 판 2개. 벽 안쪽 면 바로 뒤에 붙으므로 사용자가 세운 벽과 같은 모양으로 막힌다(`mart-boundary-shell.png`). 재검증: 도달 영역 3,618셀 동일, 경계가 도달 영역을 침범한 셀 0.
  - 만드는 법(execute_code 스크립트, 메뉴화 예정): ① 기준점에서 걷기 BFS로 R ② 바운딩 박스 모서리에서 R이 아닌 셀로 BFS → 바깥 O(선반 등 내부 장애물은 O에 안 들어감) ③ O 중 R에 8방향 인접한 셀 = 껍질 ④ 행별 연속 셀을 한 BoxCollider로. 구역을 바꾸면 같은 절차로 다시 생성.
  - BoxCollider는 두께 있는 상자라 씬 뷰에 선이 두 줄 보이며, 막는 면은 안쪽 선이다.
- 콜라이더 수: 구역 안 6,779개(convex 메시 1,356·박스 5,400). Read/Write를 켠 뒤 상품 메시 콜라이더가 조준 광선에 잡히는 것을 확인(가격표 레이캐스트 OK) → 907에서 BoxCollider로 굳이 바꾸지 않아도 됨. 단 convex 메시 콜라이더 수천 개는 물리 비용이 있어 상호작용 없는 장식은 콜라이더 제거 검토.
- 남은 것(사용자 직접): 스폰 6·대기 스폰 6·파쇄기·탈출 지점 배치(909·908), 진열대 위 올라가기 정책, 통로 폭 확인.

### 5. 스폰과 파쇄기 (2026-09-11, 909·908)

- **스폰 10개(909)**: `SpawnPoints/SpawnPoint_1~10`은 사용자가 직접 배치("이걸로 갈거야"). 검증: 전부 바닥 위(y 0.85), 막힘 없음, 경계 안, `MatchSceneConfiguration.spawnPoints` 10/10 연결. 대기 스폰은 만들지 않음(경계 안에서 시작).
  - **무작위 배정**: `MatchSceneConfiguration.shuffleSpawnPoints`(opt-in, 마트 `SpawnPoints`에 켬). 인스턴스별 첫 `CaptureSpawnPoses()`에서 피셔-예이츠 순열을 캐시해 스포너·매치 런타임·재배치가 같은 순서를 본다. 스포너는 `seat % count`라 10개 중 인원수만큼이 매치마다 다르게 쓰인다. Playground·로비는 꺼짐.
- **파쇄기 2대(908)**: 후보 두 곳(서쪽 벽 A, 동쪽 벽 B)을 제안했고 사용자가 **둘 다 쓰기로 결정** → 같은 외형의 임시 박스 파쇄기 `Shredder_A`(−16.35, 0.45, −11.42)·`Shredder_B`(10.78, 0.45, −6.19, y 270°). 구성: 큐브 0.8×0.9×0.8 + BoxCollider + `ShredderInteractable`, 자식 `ShredderSpot`(앞 0.9 m, y 0.6 = 튕김 지점)·`ShredderTarget`(앞 2.5 m). 실제 모델은 907/912에서 교체.
  - **코드(다중 파쇄기 지원)**: 씬 캡처가 `ShredderSpot` 이름 전부를 모아 `NetworkMatchRuntimeConfiguration.ShredderEjectionPoses`로 넘기고, `INetworkMatchAuthority.BindMatchSession`이 목록을 받는다. 서버 `MatchStarter.TryUseShredder`는 RPC를 바꾸지 않고 **요청한 플레이어와 상호작용 거리 안에서 가장 가까운 튕김 지점**을 고른다(`TrySelectShredderEjectionPose`). 전제: 두 파쇄기가 상호작용 거리 안에 겹치지 않게 배치. HUD 마커는 카메라(로컬 플레이어)에서 가장 가까운 파쇄기를 가리킨다. 기존 단일 `ShredderEjectionPose`는 첫 파쇄기를 가리켜 Playground 호환.
  - 검증: EditMode 테스트 `ShredderSelection_*` 2개, `Configuration_ExposesEveryShredderEjectionPose_*` 추가.
- 남은 것: 탈출 지점 마커(913, 규칙 파트와 이름 협의), 마트 씬 LifetimeScope와 `NetworkScenes` 연결(910) 후 실제 플레이로 파쇄기 두 대 동작 확인.

### 6. 냉장고·냉동고 상품 채우기 (2026-09-11, 907 선행)

- **문제**: Synty 냉동고 `Freezer_03`은 유리문 뒤 선반 5칸 상품이 문 하나당 8 cm 두께 판 한 장에 그림으로 그려져 있고, 벽 냉장고 `Wall_Fridge_02` 프리셋에는 3 cm 두께 납작 상자가 많다. 플레이어가 집을 수 있는 물건이 아니라서 실제 상품 프리팹으로 바꿔야 한다. 음료 냉장고·델리 냉장고는 이미 3D 물건이라 그대로 둔다.
- **도구** `Game > Match Map > Refill Fridge (Selected)…` ([FridgeRefillMenu.cs](../../../Assets/_Game/Editor/FridgeRefillMenu.cs)): 선택한 냉장고(또는 그 안의 조각)를 대상으로
  1. 정면 = 본체 메시에서 정점이 적은(뚫린) 면, 선반 = 본체·구조 조각의 위를 보는 면을 1 cm 높이 단위로 묶고 가로 8 cm 이상 끊기면 칸을 나눔. 위 여유는 그 위 첫 아래보기 면(선반 밑·천장)까지.
  2. 그림 상품 판정: 폭 0.4 m 이상·두께 12 cm 이하 판(냉동고), 두께 4 cm 이하로 세워진 납작 상자(벽 냉장고, 얇은 축이 깊이 방향인 것만 → 칸막이 제외). 치우는 대신 비활성화 + 이름 뒤 `[RefillHidden]`.
  3. 팔레트(냉동: 상자 `Product_09~14/31~36`·통 `06/17/18/19/39/40`·도시락 트레이 / 냉장: 우유·저그·카톤·병·통·치즈·캔 / 음료)에서 2~5개씩 묶어 무작위로, 앞줄부터 최대 N줄, 남은 3D 물건·칸막이·문은 장애물로 피함. 결과는 `…_Exploded/Refill`(없으면 `<본체>_Refill` 루트) 아래 프리팹 인스턴스.
  4. **평대형**(높이 1.3 m 미만, `Freezer_01/02`): 그림이 바닥 위에 떠 있는 수평 판(6 cm 두께, 0.56×1.31 m) 4장이라 이것을 치우고, 바닥 전체를 줄 수 제한 없이 채우며, 깊이가 높이의 절반 미만인 얇은 상자는 라벨이 위를 보게 눕힌다. 가느다란 막대(칸막이 틀, 폭 12 cm 미만)는 장애물로만 보고 선반·천장 계산에서는 뺀다.
  5. `Revert Fridge Refill (Selected)`로 원상복구. 이미 채운 냉장고는 다시 채우지 않음(먼저 되돌리기).
- **적용 결과(경계 안 전부)**: 냉동고 14대(판 10장씩 → 2줄 84~91개, 한 줄 42~43개), 벽 냉장고 3대(납작 상자 10~12개 → 89~191개), 평대 냉동고 2대(수평 그림 판 4장씩 → 눕힌 상자·통 65·70개). 총 **19대, 배치 1,682개, 숨김 182개**, 활성 렌더러 약 20k. 기본은 2줄·밀도 1·3D 잔존 물건 유지이고, 변화를 주기 위해 냉동고 3대(동쪽 벽 (21,−4), 블록 (12,−13)·(16,1))와 남쪽 벽 냉장고 1대((3,−20))는 **한 줄**(시드 7). 냉동 팔레트에서 도시락 트레이는 빈 쟁반처럼 보여 제외. 냉장 팔레트의 치즈 슬라이스 더미(`Food_Cheese_Stack_01/02`)는 사용자 요청으로 큰 병 `Product_45/46`·샌드위치 `Food_Sandwich_01`로 교체(씬의 기존 33개는 제자리에서 교체, 자리가 좁은 10개는 작은 병·통 `Product_47/48/19/41`).
- **유리**: 냉동고 `Freezer_03`·평대 냉동고 `Freezer_01/02`의 유리는 사용자가 제거(문틀만 남음). 남아 있던 `Freezer_03_Door_02_Glass_02` 한 장도 제거. 상품 프리팹은 convex 메시 콜라이더를 갖고 있어 907에서 상호작용 대상 정리와 함께 콜라이더 정책을 정한다.

### 7. 맵 등록 `supermarket` (2026-09-11, 910)

- **맵 id**: `MapCatalog.SupermarketId = "supermarket"`. 기본 맵은 playground 그대로. 방 설정 UI(`PlaySettingsMapCatalog`)와 로비 맵 카드는 카탈로그에서 자동으로 옵션이 늘어난다(라벨은 id).
- **맵 → 씬**: `NetworkScenes` 에셋에 `_mapScenes` 목록(mapId + SceneAsset)을 추가하고 playground→`Playground.unity`, supermarket→`Supermarket.unity`(구 MartBuild)를 연결. `MatchSceneFor(mapId)`가 씬을 고르고, 목록에 없는 id는 기존 `MatchScene`(playground)으로 떨어지며 경고. `IsMatchScene(SceneRef)`로 어느 맵 씬이 올라와 있는지 판별(로비 복귀 때 내릴 씬 찾기).
- **매치 진입**: `NetworkRunnerService.EnterMatchScene`이 방 설정 mapId로 씬을 고른다. 설정이 랜덤("")이면 이 순간 `MapCatalog.PickRandom()`으로 결정하고 설정은 랜덤 그대로 둔다(다음 매치도 다시 뽑힘). 결정된 맵은 `_activeMapId`에 보관되어 `AnalyticsMapId`로 나간다. 호스트가 씬을 바꾸는 구조라 서버 파트 코드 변경은 없었다.
- **빌드 목록**: `Supermarket.unity`(구 MartBuild)를 Playground 뒤(index 4)에 추가.
- **마트 씬에 넣은 매치 구성**(Playground와 같은 이름 규약): `MatchLifetimeScope`(`PlaygroundLifetimeScope` 컴포넌트, MatchRules·InputSystem_Actions 연결), `InGameHud`(메뉴 `Game > InGame > Build HUD Layout (Active Scene)`로 생성 — 이 메뉴를 새로 추가, 대기 스폰은 만들지 않음), `PlayerCameraRig` 프리팹 인스턴스, `Main Camera`에 AudioListener·CinemachineBrain(데모 `Main Camera (1)` 삭제), 계산대 위 임시 카탈로그 물건 8개(`CatalogItems_Temp`: Soda·Burger·Pineapple·Cup·Plate·Plant·Kettle·Toaster — `PlaygroundMatchScene.Capture`가 `ItemCatalog` 전 항목을 요구해서 넣은 것, 907에서 마트 상품으로 교체).
- **검증**: EditMode 133개 통과(`MapCatalogTests`, `NetworkContractTests`의 맵별 씬 테스트 포함). 이후 사용자가 실제로 supermarket을 골라 입장 확인.
- **develop 머지 (2026-09-11)**: 동료(hantawon123)의 카테고리별 아이템 카탈로그(`ItemCatalogSO`, `Resources/Items/ItemCatalog.asset`, 활성 카테고리 5개·물건 284개)가 들어와 **배정 물건은 카탈로그 프리팹에서 생성**되므로 맵 씬에 물건을 미리 놓을 필요가 없어졌다. Supermarket의 임시 `CatalogItems_Temp`는 제거. 머지 충돌은 `PlaygroundMatchScene.cs` 1건(develop 방식 채택). 스폰 지점이 6개를 넘는 맵에서 대기 지점 수 검사에 걸리던 것은 스폰 수만큼 채우도록 고쳤다. 머지 후 EditMode 1,335개 통과, Supermarket 캡처 = 스폰 10·대기 10·배정 284·파쇄기 2.
- **알려진 경고**: 매치 씬이 내려갈 때 `NetworkMatchHudPresenter.Dispose`가 이미 파괴된 `NetworkMatchHudView`를 만져 `MissingReferenceException`(develop 코드, 게임 진행에는 영향 없음). 별도 수정 필요.
- **남은 것**: 맵 카드 썸네일, 라벨 한글화 여부, WebGL 빌드 크기, 대기 스폰 정책(현재 SpawnPoint_1~6 fallback), `Global Volume`(데모 포스트프로세스) 유지 여부는 911에서.

### 9. 들 수 있는 소품 전환과 정적 배칭 (2026-09-11, 907)

- **정책(사용자 결정)**: 진열 상품·봉지·꽃다발·상자류는 들 수 있게(Carryable), 선반·냉장고·가구·쇼핑카트·설비는 고정.
- **도구** `Game > Match Map > Carryable > 1. Report Targets / 2. Convert Targets (Now, Blocking) / 3. Apply Static Batching To Fixed Props` ([MartCarryableSetupMenu.cs](../../../Assets/_Game/Editor/MartCarryableSetupMenu.cs)).
  - 대상 = 경계 안 + (분해 상품 `Products`·채움 상품 `Refill` 자식 | 원본 프리팹 이름 포함 키워드(Product·Food·Bag·Flower·Box·Lettuce·Shoe…) | 분류 불가라도 최대 변 0.5 m 이하) − 제외 키워드(Shelf·Aisle·Fridge·Counter·Cart·Bld_·Env_·Planter·Mannequin류 등). 가격표(`PriceTag`)는 진열 그룹 안에 있어도 항상 고정.
  - 우리 프리팹(생성 상품 `Gen_*` 206종, `Prefabs/Mart/Mart_Lettuce_*` 11종)은 프리팹 자체에 Rigidbody(kinematic, 질량 1)·CarryableItem을 넣었다(인스턴스 자동 반영). Synty 팩 프리팹 162종은 `Prefabs/Carryable/Mart/<이름> Carryable.prefab` 변형을 만들어 씬 인스턴스 2,355개를 교체(`ReplacePrefabAssetOfPrefabInstance`, 오버라이드 유지).
  - 표시명: Gen_/Product→상품, Food→식품, Box/Cardboard→상자, Bag→봉지, Flower→꽃, Bouquet→꽃다발, Lettuce→양배추, Shoe→신발, 기타→물건.
  - 물건 id는 `CarryableItem`의 씬 계층 해시(8자리)로, 모든 피어가 같은 씬을 로드하므로 일치한다. 중복 0 확인.
  - 배경 처리 큐는 Play 진입·재컴파일(도메인 리로드)에 사라지므로 기본 메뉴는 동기(블로킹)로 바꿨다(2,355개 교체 13초).
- **결과**: Supermarket 씬 Carryable **7,099개**(경계 안 6,144 + 프리팹을 직접 고친 탓에 따라온 경계 밖 955; 전부 kinematic, 콜라이더 있음), 고정 소품 2,723개(355종), 분류 불가 76개(48종, 고정 유지 — 컴퓨터·의류·마네킹 등, 목록은 `carryable-props.md`), 경계 밖 9,028개는 손대지 않음. 정적 배칭 플래그 12,907개(렌더러 20,021 중 Carryable·파쇄기 제외). 런타임 캡처 = 월드 오브젝트 7,099·배정 284·스폰 10·파쇄기 2.
- **주의**: 네트워크 상태는 한 매치에서 건드린(들거나 옮긴) 물건 최대 256개까지만 복제한다(`MatchSessionState.MaxReplicatedObjects`). 6인 플레이에서 부족하면 상한 조정 필요. 정적 배칭은 메모리(결합 메시)를 늘리니 WebGL 측정(914) 후 유지 여부 결정.
- 목록 문서: [carryable-props.md](carryable-props.md) (메뉴 1로 재생성).

- **추가 대상(2026-09-11, 사용자 요청)**: 창고 발판 `SM_Prop_Warehouse_Safety_Step_01`(0.8 m) — ExcludeKeywords에서 빼고 IncludeKeywords에 넣어 변환(표시명 "발판", 2개). Carryable 합계 7,101개.

#### 9-1. 가구 콜라이더 수정 (2026-09-11) — "상품 한 종류만 잡힘" 원인
- **증상**: Carryable 7,099개 중 조준 광선이 닿는 것은 272개. 나머지는 가구 콜라이더가 상품을 통째로 감싸서 광선이 가구에 먼저 맞았다.
  - Synty 가구의 MeshCollider는 시각 메시가 아니라 **별도 충돌 메시 `Models/Collision/Convex/*_Convex.asset`(닫힌 껍질)** 을 쓴다. convex를 꺼도 앞면이 막힌 채다.
  - 분해로 생성한 구조물 조각(`Gen_*` 계산대 선반·벽 냉장고 본체 등)은 바운드 크기 BoxCollider였다.
- **수정** `Game > Match Map > Carryable > 4. Fix Furniture Colliders (Non-Convex)` ([MartFurnitureColliderMenu.cs](../../../Assets/_Game/Editor/MartFurnitureColliderMenu.cs)):
  - 생성 구조물 프리팹 307종: BoxCollider → 자기 메시의 non-convex MeshCollider(프리팹 에셋 수정).
  - 경계 안 가구 인스턴스 1,301개: MeshCollider의 메시를 **시각 메시(MeshFilter)** 로 바꾸고 convex 해제(인스턴스 오버라이드, 팩 에셋은 그대로). 움직이지 않는 가구라 non-convex 허용.
  - `5. Reachability Report`: 상품마다 여러 방향·눈높이에서 광선을 쏘아 닿는 수와 막는 콜라이더를 집계.
- **결과**: 현실적 검사(8방향 × 서서/앉아서, 0.9 m) 기준 닿는 상품 272 → **4,301개**(경계 안 6,144의 70%). 남은 막힘은 선반 판·벽·냉동고 몸체 뒤쪽 등 실제 기하에 의한 것이 대부분(더 가까이·다른 각도에서는 닿음). 냉동고 안 상품은 정면에서 바로 닿고, 음료 냉장고 문은 콜라이더가 없어 유리를 통해 잡힌다.
- **조준 하이라이트가 안 보이는 문제 → 가려져도 보이는 실루엣(2026-09-11)**: 하이라이트는 `InteractableFocusOutline`의 주황 2 px 뒤집힌 껍질(inverted hull) 윤곽선인데(2026-09-06 chrin105가 `_BaseColor` 밝게 하기에서 교체), 진열대처럼 옆 물건·선반 판·뒷줄 상품이 딱 붙어 있으면 껍질이 전부 그 안에 파묻혀 깊이 검사에 걸러진다. 플레이 모드 렌더 검증(정육면체 3개를 붙여 놓고 위·아래 판·뒷벽을 댄 진열 모형, 640×480): 가운데 상자의 기존 윤곽선 주황 픽셀 1개 vs 아래 SeeThrough 567개. 같은 상자를 혼자 두면 기존 윤곽선도 1,342픽셀 고리로 정상 표시 → 셰이더 문제가 아니라 순수 가림 문제.
  - 해결: `InteractableFocusOutline.SeeThrough`(스텐실 2단계) 추가. 1단계 물건 메시를 색 없이·깊이 무시·양면으로 그려 스텐실 비트(1)만 표시, 2단계 껍질을 깊이 무시 + `Stencil NotEqual`로 그려 실루엣 바깥 2 px 고리만 남긴다 → 가려진 부분까지 물건 윤곽이 보인다. 셰이더 `Game/AssignedItemOutline`에 렌더 상태 프로퍼티(`_Cull/_ZTest/_ColorMask/_Stencil*`)를 추가했고 기본값은 기존 동작과 같다. `CarryableItem`이 새로 붙이는 실루엣만 SeeThrough를 켜고, 로비 계획판(상시 흰 5 px)·지정 물건 빨간 윤곽선은 그대로.
  - 테스트: `AssignedItemOutlineTests`(SeeThrough 마스크·껍질 재질 상태, 기본 모드 무변경).
  - **렌더 큐 문제(2026-09-11)**: 위 수정 뒤에도 사용자 화면에서 윤곽선이 안 보였다. 원인은 그리는 순서. Synty(Polygon) 재질은 알파 클립 때문에 **큐 2450(AlphaTest)** 에 그려지는데 윤곽선 셰이더는 `Geometry+1`(2001)이라 먼저 그려지고, 그 뒤에 그려지는 앞쪽 소품·선반·옆 물건이 윤곽선을 덮어썼다(URP Lit 상자로 만든 테스트 장면은 큐 2000이라 재현이 안 됐음). 셰이더 큐를 `AlphaTest+49`(2499), SeeThrough 마스크·껍질 재질 큐를 2498·2499로 옮겨 해결. 검증: 냉장고 안 상품 8방향 중 앞에 소품이 있는 5방향이 0 px → 수정 후 전 방향 표시. 이 문제는 chrin105의 원래 윤곽선(로비 계획판·지정 물건 빨간 윤곽선)에도 같은 조건이면 생기므로 공유 필요.
  - 검증 시 주의: 에디터에서 갓 임포트/수정한 셰이더는 비동기 컴파일 중이라 첫 `Camera.Render()`에 아무것도 그려지지 않는다(에디트 모드 검증이 전부 0으로 나온 원인). 두 번 렌더하거나 플레이 모드에서 확인할 것.

### 10. 조명 베이크 1차 (2026-09-11, 911)

- 메뉴 `Game/Match Map/Lighting/`([MartLightingSetupMenu.cs](../../../Assets/_Game/Editor/MartLightingSetupMenu.cs)). 로비 도구를 마트 규모(경계 안 52×75 m, 로비의 25배)와 평평한 씬 구조(경계 박스 15개 합집합)에 맞게 옮긴 것.
  0. **Generate Lightmap UVs**: Synty FBX는 라이트맵 UV(UV2)가 없어(3,757개 중 3,712개) 1·2차 베이크가 아틀라스 UV0로 구워져 라이트맵이 거의 비어 있었다. FBX 임포터 383개 Generate Lightmap UVs 켜서 재임포트 + 분해 메시 에셋 89개 `Unwrapping.GenerateSecondaryUVSet`.
  1. **Mark Static**: 경계 안 고정 소품 3,757개에 ContributeGI·ReflectionProbeStatic·OccludeeStatic(경계 밖 외부 지형 9,148개·움직이는 상품 7,109개 제외).
  2. **Convert Lights**: 태양광 Mixed(부드러운 그림자), 팩 스팟 7·포인트 6은 Baked, 스팟 세기 5, 창고 구역(x < -40) 포인트 세기 절반.
  2b. **Fixture Lights**: 마트는 지붕이 닫혀 환경광이 실내로 안 들어와 조명 14개로는 어두웠다(라이트맵 평균 0.08). 전등 소품(천장 바·스팟·벽등) 79개 자리에 Baked 포인트(범위 9 m, 세기 3.5, 난색) 자동 생성 → 조명 93개.
  3. **Setup**: `MartLighting.lighting`(Progressive GPU, 8 texels/m, 최대 4096, Shadowmask, AO 0.5 m, 샘플 32/256/128, 바운스 2), 평면 환경광, 라이트 프로브 격자 3.5 m × 높이 0.4/1.6/3.2 m → 902개, 리플렉션 프로브 3×4 = 12개(Baked 128).
  4. **Post-Process**: 로비 프로필 복제 `MartPostProcess.asset`(Neutral 톤매핑·블룸·비네트·색보정) → `Mart Post Volume`(global), Synty 데모 `Global Volume` 제거, Main Camera PP + SMAA Medium.
  5/6. Bake / Clear, Report.
- 결과: 라이트맵 2장(1024), 산출물 `Assets/_Game/Content/Scenes/Supermarket/`(약 22 MB, exr는 LFS). 매장 밝기 평균 45 → 71(계산대), 49 → 79(진열대). 창고는 갓등 몇 개만 남아 어둡게 유지(숨기기 유리 구역).
  - `supermarket-lighting-bake-v1-checkout.png`, `-shelf.png`, `-warehouse.png`.
- **재베이크 규칙**: 선반·벽·전등·고정 소품 이동/추가/삭제, Static 플래그 변경 뒤 5번 재실행. 들 수 있는 상품 7,101개는 동적(라이트 프로브로 조명)이라 배치를 바꿔도 재베이크 불필요. 새 고정 소품을 넣으면 0·1번을 먼저 다시 돌려 UV2·플래그를 채운다.
- **밝기 상향 2차(2026-09-11, 사용자 "어두운 곳이 너무 많다")**: 태양을 실내로 옮기는 건 효과가 없다(Directional은 위치가 없는 평행광, 지붕에 막힘). 대신 (1) 간접광 세기(Indirect Intensity) 1 → 2, (2) 전등 라이트 범위 9 → 13 m·세기 3.5 → 5, (3) `2c. Add Fill Lights In Dark Areas`: 바닥 4 m 격자 표본 중 어느 실내 라이트 범위(×0.75)에도 안 드는 지점에 Baked 포인트(범위 12, 세기 3) 자동 추가(창고 제외, 5개). 조명 98개. 결과 평균 밝기 계산대 71 → 96, 진열대 79 → 106, 통로 125. `supermarket-lighting-bake-v2-checkout.png`, `-shelf.png`.
- **3차(2026-09-11, 사용자 "창고·입구가 빛이 없어 너무 어둡다, 무서워진다")**: 창고 어둡게 두기 해제(`DimWarehouse = false`, 창고 포인트 세기 복구·채움 라이트 창고 포함), 채움 라이트를 **6 m 격자 천장 전등**으로 바꿈(판정 거리 0.3, 범위 11, 세기 3.5 → 51개). 조명 144개. 하역장 평균 밝기 34 → 65(눈높이 109), 계산대 104, 진열대 113. `supermarket-lighting-bake-v3-loading.png`, `-checkout.png`.
  - 대안 메뉴 `7. Revert To Realtime Lighting (No Bake)`: 베이크 전처럼 실시간 조명 + 스카이박스 환경광(지붕에 가려지지 않아 실내가 균일하게 밝음)으로 되돌린다. 그림자·접촉 음영은 사라진다. 자동 생성 라이트 제거·ContributeGI 해제·베이크 데이터 삭제까지 한 번에.
- **현재 상태 = 실시간 조명(베이크 없음, 2026-09-11 사용자 결정)**: 3차까지 봐도 "실내라서 베이크 전처럼 균일하게 밝은 쪽이 낫다"는 판단으로 `7. Revert To Realtime Lighting`을 적용. 팩 조명 14개 Realtime(스팟 세기 5 유지), 스카이박스 환경광 세기 1.3, 자동 생성 라이트·프로브·라이트맵·ContributeGI 모두 제거, 포스트프로세스(SMAA)는 유지. 평균 밝기 계산대 94, 하역장 88.
  - 함정: `Lightmapping.ClearLightingDataAsset()` 뒤에는 스카이박스 환경광 프로브가 사라져 실내가 어두워진다(34). `DynamicGI.UpdateEnvironment()`로 즉시 재생성(메뉴에 포함). 씬 로드 시에는 Unity가 자동 생성한다.
  - **플레이 모드 함정(2026-09-11)**: 에디터에서는 `DynamicGI.UpdateEnvironment()`로 환경광이 보였지만 **플레이 모드에서는 환경광 프로브가 0**이었다(라이팅 데이터가 없으면 런타임에 생성되지 않음). 해결: GI 대상 0개 상태에서 `Lightmapping.Bake()`(2.5초) → `Supermarket/LightingData.asset`에 환경광 프로브만 저장(라이트맵 0장). 이 에셋은 커밋 대상. 스카이박스·환경광 세기를 바꾸면 다시 생성해야 한다.
  - **어두운 곳 확인용 실시간 전등(2026-09-11)**: 전등 소품 79개 자리의 `FixtureLight_*`를 Realtime(세기 2.5, 범위 10, 그림자 없음)으로 켜 두었다(`MartEnvironment/Lights`). 어디가 어두운지 보려는 용도이며 켜고 끄면서 비교 가능. 조명 93개. 플레이 모드 계산대 밝기 115.
  - 베이크 도구(0~6번)는 그대로 남겨 두었다. 나중에 그림자·음영이 필요하면 0 → 1 → 2/2b/2c → 3 → 4 → 5 순서로 다시 굽는다. 라이트맵 UV(FBX 383개)는 이미 켜져 있어 재사용된다.
  - `supermarket-lighting-realtime-checkout.png`, `-loading.png`.
- **최종 확정(2026-09-11 사용자 "밝기는 괜찮다")** — 실시간 조명, 베이크 없음:
  | 요소 | 값 |
  |---|---|
  | 팩 조명 14개 | 원본 그대로(스팟 2.0~3.12, 포인트 3.3~7.5, 태양광 1.35 Realtime) |
  | 전등 소품 자리 라이트 `FixtureLight_*` 79개 | Realtime 포인트, 세기 1.5, 범위 11 m, 그림자 없음 (`MartEnvironment/Lights`) |
  | 수직 보정등 `Overhead Fill Light` | Realtime Directional, X 90° 수직 아래, 세기 0.75, 그림자 없음 — 지붕에 막히지 않는 채움광(사용자 제안) |
  | 환경광 | Skybox, 세기 1.35 + `Supermarket/LightingData.asset`(환경광 프로브만, 라이트맵 0장) |
  | 포스트프로세스 `MartPostProcess` | Tonemapping Neutral, ColorAdjustments 노출 +0.25·대비 5·채도 6, Bloom 0.3(threshold 1, scatter 0.6), Vignette 0.12 |
  | 계산대 뷰 평균 밝기 | 138 (가장자리 117) |
  - 조절 손잡이: 전체 바탕(벽 포함) = 환경광 세기(바꾸면 `Lightmapping.Bake()`로 환경광 데이터 재생성), 바닥·윗면 = 보정등, 전등 웅덩이 = FixtureLight 세기.
  - **프로필 함정**: `VolumeProfile.Add<T>()`만 하면 컴포넌트가 서브에셋으로 저장되지 않아 커밋된 에셋이 빈 채(`components: [{fileID: 0}]`)가 된다. `AssetDatabase.AddObjectToAsset(component, profile)` 후 SaveAssets 필수. 로비 `LobbyPostProcess.asset`이 이 상태(비어 있음) → 별도 수정 예정.
- 베이크로 돌아갈 때 남은 조정: 해상도 8 → 12 texels/m 검토(얼룩 보이면), 캐릭터 실시간 그림자(태양광 Mixed만) 확인, 드로우콜·WebGL 프레임 측정.

### 11. 파쇄기 외형 (2026-09-11, 908)

- 임시 박스(0.8×0.9×0.8 큐브)였던 `Shredder_A`/`Shredder_B`에 팀원(hantawon123, d21adf0a)이 올린 `Assets/PurpleBearShredder/Prefabs/PurpleBear_Shredder.prefab`(보라색 파쇄기, Animator `Spin` 칼날 회전, BoxCollider 5개 + `ShredZone` 트리거)을 자식으로 붙였다. Playground는 같은 프리팹을 Scale 0.3으로 쓴다.
- 루트 구조는 그대로(`ShredderInteractable` + 루트 BoxCollider + `ShredderSpot`/`ShredderTarget` 자식). 루트 스케일을 (1,1,1)·바닥(y 0)으로 정리하고 큐브 MeshFilter/MeshRenderer만 제거, 모델은 자식 `PurpleBear_Shredder` Scale **0.45**(가로 1.33 × 높이 0.76 × 깊이 1.02 m). 루트 BoxCollider를 모델 바운드로 맞춰 조준·프롬프트("파괴하기")가 그대로 동작. 튕김 지점·목표점은 월드 위치를 보존.
- `Shredder_B`는 냉동고 사이 틈이라 모델을 로컬 z +0.2 m로 밀어 겹침 0으로 맞춤(자동 탐색: 회전 0/90° × x/z 오프셋 조합 중 첫 무충돌). 재질은 URP Lit(팩 동봉)이라 마트 조명과 톤이 맞는다.
- 남은 것: HUD 파쇄기 마커 표시 확인, 인당 5회 제한·전체 공지 재확인(플레이 테스트에서).

### 12. 숨기기 대기 구역 — 헬스장 (2026-09-11, 909)

- 규칙: 숨기기 단계가 시작되면 첫 숨는 사람을 뺀 전원이 `WaitingSpawnPoint_N`(자기 번호)으로 텔레포트되고, 한 명이 숨기기를 끝내면 그 사람만 자기 대기 지점으로 옮겨진다(`NetworkMatchRuntimeCoordinator`, role=waiting). 대기 지점이 없으면 같은 번호의 `SpawnPoint_N`(매장 안)으로 대체되어 대기자가 숨기는 모습을 볼 수 있었다.
- **대기 공간 = 헬스장**(사용자 선택; 2층 푸드코트 자동 후보는 폐기). 방 범위 x -49.1~-37.2, z -17.1~-8.6(약 12×8.5 m), 바닥 y 0. 현재 매치 경계(Boundary, x ≥ -27.5) **밖**이라 대기자는 텔레포트로만 들어오고 경계 벽 때문에 매장으로 걸어갈 수 없다.
- **밀폐 콜라이더** `MartEnvironment/WaitingArea/WaitingBound_{West,East,North,South,Ceiling,Floor}`: 벽 면에 두께 0.4 m 박스, 천장 y 3.0, 바닥 아래 y -0.5. 문·유리문·천장 덕트 틈으로 나갈 수 없게 6면 전부 막음.
- **대기 스폰 10개** `WaitingSpawnPoints/WaitingSpawnPoint_1~10`: 바닥 +0.85 m, 서로 2 m 이상, 소품과 겹침 없음, 방 중심을 바라봄. 검증: 이름 중복 0, 10개 모두 방 안, 눈높이에서 매장 스폰 10곳 시야 0/10, 26방향 30 m 광선 새는 곳 0/260.
- 숨기기 단계 UI 안내(N/6명 순서)와 대기자 이동은 규칙 코드 그대로. 탐색 시작 때 전원 매장 안 탐색 스폰으로 다시 이동.
- 참고: `PlaygroundMatchScene.Capture`를 에디트 모드에서 호출하면 Carryable 오브젝트 id 미해석으로 예외("Object id is required")가 나지만 런타임에는 Awake에서 해석되므로 문제 없음(이전 플레이 확인).

### 13. 배치(놓기) 조준 로직 수정 (2026-09-12, 906)

- 증상(사용자): 진열대 안쪽을 노리면 고스트가 엉뚱한 곳(진열대 앞 바닥)에 뜨고, 초록/빨강이 의도와 안 맞음.
- 원인(`ItemPlacementController.UpdatePreviewPose`): 조준점이 플레이어 발 중심에서 수평 1 m를 넘으면 1 m 지점으로 **끌어당긴 뒤 아래로 투영**했다. 캡슐 반지름 0.3 m + 선반 받침 때문에 플레이어 중심은 선반 면에서 0.5 m 이상 떨어져 있어 선반 안쪽(깊이 0.4~0.6 m)은 거의 항상 초과 → 허공 → 바닥으로 투영. 또 광선이 첫 표면(선반 앞면·앞줄 상품)에서 멈춰 안쪽 선반 판을 못 잡았다.
- 수정(2차, 같은 날): 1차로 "거리 초과 시 끌어당기지 않고 빨간 고스트 + 너무 멀어요"를 넣었다가 **바닥 조준까지 전부 빨갱이 되어 되돌렸다**(가슴 높이에서 3차원 거리를 재니 1 m 앞 바닥이 1.56 m로 계산됨). 최종:
  1. 거리 초과 시 예전처럼 플레이어 방향 한계선 안쪽으로 끌어와 "지금 놓을 수 있는 자리"를 보여 준다(수평 거리, `InteractionConfig.placementMaxDistance` 1 → **1.3 m**).
  2. 수직면(법선 |y| < 0.5)을 조준하면 광선 방향으로 0.15 m 안쪽에서 위 0.35 m / 아래 0.6 m 범위로 윗면을 찾아 그 위에 놓는다(선반 판·상품 위). 끌어당김이 선반 안쪽 점을 선반 밖으로 밀어내면 예전처럼 바닥에 놓인다 — 이때는 한 걸음 더 다가가면 된다.
  3. 서버 판정(`PhysicsPlacementValidator`: 겹침·받침면·차례 규칙)은 변경 없음. 거리 한도는 클라이언트 UX 규칙(서버에 거리 검사 없음).
- 검증 수치: 컨트롤러 배치 수학은 0/30/60/90° 기울기에서 겉모습 최저점이 바닥 아래 최대 6 mm(뚫림 없음). 배정 물건 프리팹 318종은 콜라이더=겉모습. 마트 진열 상품 중 콜라이더가 겉모습보다 작은 프리팹 **59종**(파인애플 10 cm 등)에 겉모습 크기 BoxCollider를 추가해 놓았을 때 바닥에 잠기지 않게 함.
- 남은 한계: 30~60° 기울기에서는 판정이 상자(AABB) 기준이라 모서리로 떠 보임. 해결은 90° 단위 스냅(클라이언트) 또는 실제 모양 판정(서버, hantawon123 협의) 중 선택 대기.
