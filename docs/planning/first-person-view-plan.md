# 1인칭 시점 연출 정비 — 준비 문서 (2026-09-16)

브랜치: `feature/client/first-person-view` (origin/develop ab573347에서 분기, 2026-09-16 캐릭터 교체 머지 96f66456까지 반영). Jira 번호 미발급.
목표: 1인칭(V 키)에서 내 양손·팔이 살짝 보이고, 때리기·던지기·집기 같은 동작이 1인칭 화면에서 자연스럽게 보이도록 카메라·몸 표시·HUD를 전체적으로 손본다.

---

## 1. 현재 상태 (코드 대조)

| 항목 | 현재 | 위치 |
| --- | --- | --- |
| 1인칭 몸 처리 | `Visual` 하위 렌더러 전부 `ShadowsOnly` → 몸이 통째로 안 보임(그림자만) | `Client/Cameras/PlayerCameraController.cs` `ApplyView()` |
| 카메라 위치 | `followTarget.position + (0, 눈높이, 0)`. 눈높이는 자세별 config(Stand/Crouch/Prone EyeHeight)를 Lerp | 같은 파일 `LateUpdate()` |
| 카메라 회전 | yaw/pitch만. 몸(본)은 카메라 피치에 반응하지 않음 | 같은 파일 |
| 1인칭 렌즈 | FOV 60, Near 0.05 (3인칭과 동일) | `Content/Prefabs/PlayerCameraRig.prefab` `FPS Camera` |
| 시점 전환 | Cinemachine 두 대 Priority 스왑, 블렌드는 Cut | 같은 파일 `CutViewBlend()` |
| 캐릭터 모델 | **SmoothBear**(매끈한 곰) `Content/Characters/SmoothBear/SmoothBear.fbx` — **Generic 리그**(Humanoid 아님). 2026-09-16 팀원 머지로 교체됨 | `PlayerCharacter.prefab`의 `Visual`, 문서 `docs/design/character/modeling/smooth-bear-unity.md` |
| 본 구조 | Hips → Spine → Neck → **Head** / Shoulder.L·R → UpperArm → Arm → **Hand.L·R** → Finger_* / UpperLeg → Leg → Foot | FBX 내부 |
| 메시 | `Body`(단일 스킨드 메시, 교정 블렌드셰이프 5개) + `Hood`, `ear`, `Eye_White_L/R`, `Eye_Pupil_L/R`, `Mouth_Smile`(Head 하위 별도 메시) | FBX 내부 |
| 착용물 | 후드 4종·표정 5종은 **Head 본**, 신발은 **Foot.L/R 본**에 런타임으로 `MeshRenderer`를 생성해 붙임(26개 Head, 2개 Foot). 색은 MaterialPropertyBlock | `Client/Character/AvatarAppearanceApplier.Wearables.cs`, `Content/Characters/SmoothBear/Wearables/*.asset` |
| 모션 | `.anim` 119개(상태 139개): Punch/Punch_Left(+Walk/Run/Crouch 변형), Punch_Combo, Throw, Pickup/PutDown(Low/Crouch/Prone), Carry/Carry_TwoHands 전 세트, Hit, Stun, Jump/Fall/Land | `Content/Characters/SmoothBear/Animations/`, 목록 `docs/design/character/current-bear-actions.md` |
| 공격 흐름 | 좌클릭 → `AttackPerformed` → `PlayAnimationDriver.PlayPunch()` (좌/우 교대) | `Client/Combat/PlayerCombatant.cs`, `Client/Players/PlayerAnimationDriver.cs` |
| 피격 표시 | 몸 머티리얼 색을 붉게(HitFlash) / 기절 시 회색 | `PlayerCombatant.cs` — **1인칭에선 내 몸이 안 보여 피드백이 사라짐** |

## 1.5 진행 기록 (2026-09-16)

- 1차: 몸을 그대로 그리고 Head 본만 숨기는 방식(아래 A) → 손이 화면에 들어오지 않음. 팔을 카메라에 붙이려 본을 잡아당기면 어깨가 늘어나는 한계 확인.
- **결정: 3인칭 몸과 1인칭 팔을 분리(아래 B로 전환).** 지환 요청: "3인칭 화면이랑 1인칭 화면이랑 다르게 구성".
- 팔 모델: 원본 `SmoothBear.blend`은 건드리지 않고 복제본에서 팔만 잘라 냄. 스크립트 `Tools/build_first_person_arms.py`(팔 본 가중치 ≥0.3 + 위팔 축 수직 어깨 절단면 bisect + 구멍 메우기). 결과 `source/blender/characters/SmoothBear/FirstPerson/SmoothBear_FirstPersonArms.blend`, FBX `Assets/_Game/Content/Resources/FirstPerson/SmoothBear_FirstPersonArms.fbx`(9,940 정점, 뼈대 전체 유지 → 기존 `.anim` 경로 호환).
- Unity: `Client/Cameras/FirstPersonArmsView.cs` — 카메라 리그 아래에 팔 인스턴스를 만들고(Resources.Load), 매 LateUpdate 몸 리그의 팔 본(Shoulder/UpperArm/Arm/Hand/Finger/Volume_*) 로컬 회전을 복사 + 카메라 기준 자세 오프셋(lift/spread/bend) 적용. 머리 본을 카메라 앵커(`eyeAnchor`)에 맞춰 어디를 봐도 손이 같은 자리에 남음. 재질·몸 색은 Body 렌더러에서 복사, 그림자 없음. 3인칭 몸은 1인칭에서 예전처럼 ShadowsOnly.
- **캐릭터 크기 0.65 (2026-09-16, 지환 결정; 0.7에서 재조정)**: `PlayerCharacter.prefab`의 `Visual` 스케일 0.55→0.65. 캡슐 1.3/0.21(중심 0.65), 키 1.3/0.86/0.44. 눈높이는 팀 테스트 후 **모델 눈 기준으로 확정: 서기 1.04(모델 눈 1.60×0.65)·앉기 0.65·엎드리기 0.35**. 1.6으로 두면 다른 플레이어 머리 위에서 내려다보게 되어 되돌림. 늦게 생성되는 표정 눈·후드·신발이 1인칭에서 보이던 버그는 렌더러 목록 0.5초 주기 재수집으로 수정. 1인칭 팔 색이 몸 색을 따르지 않던 버그(슬롯 인덱스 없는 PropertyBlock 복사)도 수정. 모델 실측(Blender, 스케일 1): 몸 높이 1.964 m, 후드 포함 2.094, 눈 중심 1.60, 가슴 반폭 0.323. 이에 맞춰 충돌 캡슐 1.8/0.3 → **1.4/0.23**(CharacterController·KCC 둘 다), `MovementConfig` 키 1.4/0.93/0.47. **눈높이는 지환 판단으로 기존 1.6/1.0/0.45 유지**(모델 눈 위치 1.12보다 높지만 실제 눈 위치에 두면 시야가 낮게 느껴짐 — 1인칭은 몸이 숨겨지므로 게임플레이 시야 우선). 카메라 리그 headOffset도 1.6 유지. 공격 범위(0.8/0.7)·상호작용 거리(2.0)·들기 오프셋은 밸런스 항목이라 그대로 두고 표시.
- **스케일이 0.55로 자꾸 되돌아가던 원인(2026-09-16)**: `Assets/_Game/Editor/FirstInGameSetup.cs`가 도메인 리로드마다 `[InitializeOnLoadMethod]`로 실행되며 Visual 스케일이 0.55가 아니면 0.55로 고쳐 프리팹을 저장했음. 기존 Visual의 크기는 건드리지 않도록 수정(새로 끼울 때만 0.65).
- 던지기: 눈높이 앞 0.35 m에서 놓고 조준점을 향해 수평 이하로만 던짐(머리가 최고점, 위로 솟는 포물선 없음). 속도 8→6, 위쪽 편향 0.
- **1인칭 팔 2차(2026-09-16)**: 동작별 자세 프로필(Locomotion/Carry/Crouch/Prone, 0.15s 블렌드) + 펀치 연출(조준점 보정·목표 손 거리·주먹 쥠 곡선(클립 샘플링)·시작 오프셋·팔꿈치 안쪽·위팔 길이) + **오버레이 카메라**(레이어 `FirstPersonView`(9), URP 카메라 스택, 벽 근접 시 팔 가림 해결). Play 중 조정값은 메뉴 `Game > First Person > Save Arm And Hold Settings To Prefab`으로 PlayerCameraRig 프리팹에 저장(Play 중 Inspector 값은 Stop 시 사라짐). 지환 조정값 프리팹 커밋.
- **캐릭터 크기 0.85 (2026-09-16, 지환 재조정 0.8→0.85; 브랜치 feature/client/character-item-fixes)**: sound-effect 머지가 프리팹 스케일을 0.55로 되돌린 것을 발견 → 0.85로 확정. 캡슐 1.67/0.27(중심 0.835, KCC 동일), 키 1.67/1.10/0.56, 눈높이 1.42/0.85/0.46(모델 눈 1.60×0.85=1.36에 지환이 원한 '살짝 위' 비율 반영, 앉기·엎드리기는 이전 비율), 카메라 폴백 1.42, FirstInGameSetup 기본 0.85.
- **Playground 씬 = 마트 복제본(2026-09-16, 지환 결정)**: 단독 Play 테스트를 마트 환경에서 하기 위해 `Playground.unity`를 `Supermarket.unity` 복제 + 테스트용 `PlayerCharacter`(SpawnPoint_9)로 교체. 기존 집 맵 Playground는 git 이력(333602c3 이전)에 있음. 마트 씬 원본은 건드리지 않음. 맵 id `playground`도 이제 마트 환경을 보여줌. 씬 파일 37 MB.
- **스케일이 머지마다 되돌아가는 원인과 방지(2026-09-16)**: `FirstInGameSetup`이 리로드마다 `PlayerCharacter.prefab`·`SmoothBear.prefab`·컨트롤러를 **항상 다시 저장**해 팀원 커밋에 프리팹 변경이 섞였고, 옛 스크립트(0.55 강제)를 가진 머신에서 저장된 프리팹이 머지로 들어오며 크기가 튀었음(sound-effect 머지 f10e0f86에서 0.65→0.55). 방지: (1) 자동 실행은 읽기 검사만 하고 이미 연결돼 있으면 저장하지 않음(전체 재적용은 메뉴에서만), (2) 팀 결정 크기를 `Game.Client.Players.PlayerVisualScale.Value`(0.85) 한 곳에 두고 에디터 도구가 참조, (3) EditMode 테스트 `PlayerCharacterPrefabScaleTests`가 PlayerCharacter·NetworkedPlayer 프리팹의 Visual 스케일을 검사. 팀원들이 이 변경을 pull하기 전까지는 그 머신의 옛 스크립트가 여전히 프리팹을 되돌릴 수 있음 → 머지 후 테스트 실행으로 확인.
- **팀 테스트 대형 버그(2026-09-16): 놓기·분쇄·던지기 후 물건이 손에 붙어 있고 로비까지 따라옴** — 클라이언트 로그에 `[Interaction] drop/release/throw rejected by authority` 91건(drop 10·release 38·throw 43). 즉 호스트가 놓기 요청을 거부해 '들고 있는' 상태가 유지된 것. 호스트 검증은 (1) 플레이어 발 위치에서 2 m 이내·회전 정규화, (2) 페이즈(숨기기는 자기 차례만, 탐색은 기절 아닐 때), (3) 배치는 호스트 물리 검사(겹침·받침). 어느 조건인지 로그에 없어 **거부 이유를 호스트 로그와 클라이언트 경고에 붙이는 진단**을 추가(`MatchStarter.TryPrepareRelease`, `MatchSessionCoordinator.DescribeReleaseBlock`). 확정 원인 후보: 배치 홀로그램이 수평 1.5 m + 선반 높이로 3D 거리 2 m 초과(클라는 파란색, 호스트 거부) → 클라 배치 유효성에 3D 거리 검사 추가. 로비 따라옴은 씬 이탈 시 정리 누락 → 브릿지 Dispose에서 CarriedItem·HoldPoint 하위 물건 강제 제거 + 로비 진입 시 안전망 제거.
- 남은 것: 물건이 1인칭에서 커 보임(기본 거리·크기별 보정), 동작별 팔 프로필, Unity에서 실제 확인(지환), 각도 기본값 확정, 펀치·들기 모션 1인칭 확인(T4), CCTV·리플레이 카메라에 팔이 찍히지 않는지(현재는 1인칭 아닐 때·리플레이·리그 비활성 시 숨김으로 처리).

## 2. 접근 방식 비교

### A. 진짜 몸을 그대로 보여주고 머리만 숨기기 (권장)
- 1인칭에서 `Body` 렌더러를 켜 두고 **Head 본 스케일을 0**으로 만들어(LateUpdate, Animator 이후) 머리·눈·입만 사라지게 한다. Generic 리그라 본 트랜스폼 직접 조작이 가능하다.
- 장점: 이미 만든 69종 모션이 그대로 1인칭 팔 모션이 된다(펀치·던지기·두 손 들기·집기). 별도 에셋·애니메이터 불필요. 아래를 보면 내 몸통·발이 보여 존재감이 생긴다.
- 단점: 카메라가 몸과 독립적으로 움직이므로 위치·피치를 맞추는 튜닝이 필요하다. 머리 그림자가 사라진다(허용 가능).
- 로컬 플레이어의 `Visual`에만 적용해야 한다. 3인칭·엔딩 무대 오버라이드(`SetBodyVisibleOverride`)·리플레이에서는 반드시 복원.
- 후드·귀·눈·입·표정은 전부 Head 본 하위라 Head 스케일 0으로 **함께 사라진다**(별도 처리 불필요). 신발은 Foot 본이라 그대로 보인다(아래를 보면 내 신발이 보임 — 의도에 맞음).

### B. 카메라에 붙는 별도 팔 모델(FPS 뷰모델)
- 팔만 있는 메시를 FPS Camera 자식으로 두고 전용 애니메이터로 돌린다.
- 장점: 팔 위치를 화면 기준으로 완전히 통제.
- 단점: 팔 메시·리그 신규 제작, 펀치/던지기/집기/들기 모션을 뷰모델용으로 다시 만들어야 함. 두 손 들기 물건 위치도 이중 관리. 이 프로젝트 일정과 안 맞음.

**추천: A.** B는 A로 원하는 그림이 안 나올 때만 검토.

## 3. 태스크 분해 (초안, 번호 미발급)

### T1. [CL] 1인칭 몸 표시 전환 — 머리만 숨기기
- `ApplyView()`의 전체 `ShadowsOnly`를 제거하고, 1인칭이면 `Head` 본 localScale을 0(또는 0.0001)으로, 아니면 원래 값으로 복원. Animator가 매 프레임 본을 덮어쓰므로 `LateUpdate`에서 적용.
- `SetFollowTarget`에서 `Visual` 하위 `Head` 트랜스폼을 찾아 캐시(이름으로 찾기: 기존 `Visual` 찾기와 같은 방식).
- `bodyVisibleOverride`·3인칭·리플레이(`BeginReplay/EndReplay`)에서 복원되는지 확인.
- 네트워크 상대 플레이어(`NetworkedPlayer`)에는 영향 없어야 함.
- ⚠️ 점검: 현재 `bodyRenderers`는 `SetFollowTarget` 시점에 한 번 수집한다. 착용물(후드·신발)은 `AvatarAppearanceApplier`가 나중에 런타임 생성하므로, 지금 방식(ShadowsOnly)에서는 1인칭에서 후드·신발만 떠 보일 수 있다. A로 바꾸면 Head 본 스케일이 후드를 함께 처리하니 이 문제도 자연히 사라지는지 확인.
- 완료 조건: 1인칭에서 아래를 보면 몸통·팔·발이 보이고, 정면에서는 머리 안쪽이 안 보임. 3인칭 전환 시 머리 복원.

### T2. [CL] 1인칭 카메라 위치·렌즈 튜닝
- `headOffset.z`를 약간 앞으로(0.05~0.12) 두어 목·어깨 안쪽 면이 화면에 안 걸리게.
- FPS Camera FOV 결정(60 유지 vs 70~75: 넓을수록 팔이 더 보임), Near Clip 확인.
- 자세 전환(서기↔앉기↔엎드리기) 시 카메라 눈높이 Lerp(8/s)와 몸 애니 전환 속도의 어긋남 확인.
- 완료 조건: 걷기·달리기·앉기·엎드리기 각각에서 몸 클리핑 없음, 손이 화면 하단에 살짝 보임.

### T3. [CL] 시선 피치에 상체 따라가기
- 카메라 pitch의 일부(예: 40~60%)를 `Spine`(또는 Neck) 본 회전에 더해 위·아래를 볼 때 팔이 시야를 따라오게 한다. `LateUpdate`에서 T1과 함께 적용.
- 3인칭에서도 적용할지 결정(3인칭은 몸이 보이므로 자연스러움에 도움이 되나 네트워크 동기화 대상은 아님).
- 완료 조건: 위를 볼 때 팔이 화면에서 완전히 사라지지 않고, 아래를 볼 때 팔이 얼굴을 가리지 않음.

### T4. [CL] 동작별 1인칭 확인·조정
- 확인 목록: Punch / Punch_Left (+Walk/Run/Crouch 변형), Throw / Throw_TwoHands, Pickup·PutDown(Low/Crouch/Prone), Carry_Idle / Carry_TwoHands(물건이 화면 어디에 보이는지), Hit, Stun_Start~End, Jump/Fall/Land, Crouch/Prone 전환.
- 문제가 있는 클립은 1인칭 전용 변형을 만들지(예: `Tools/make_first_punch_locomotion.py`처럼 베이크), 카메라 쪽에서 해결할지 결정.
- 완료 조건: 위 목록 전부 1인칭 캡처 1장씩 → `docs/design/first-person/`.

### T5. [CL] 몸 회전과 카메라 yaw 동기화 점검
- 1인칭에서 마우스 좌우 회전 시 몸이 지연 회전하면 팔이 옆으로 밀려 보임. `PlayerMovement`의 회전 방식을 확인해 1인칭에서는 즉시(또는 매우 빠르게) 몸을 카메라 yaw에 맞춘다.
- 완료 조건: 빠르게 좌우를 돌려도 손 위치가 화면에서 흔들리지 않음.

### T6. [CL] 1인칭 HUD·화면 피드백
- 피격: 몸 색 HitFlash는 1인칭에서 안 보임 → 화면 가장자리 붉은 비네트/테두리(`MatchUrgencyBorderView` 방식 재사용 검토).
- 기절: 몸 회색 대신 화면 흐림/어둡게(`ScreenBlur` 재사용 검토).
- 조준점: 1인칭 전용 작은 점 표시 여부, `InteractionPromptView` 위치 확인.
- 완료 조건: 1인칭에서 맞았을 때·기절했을 때 화면만 보고 알 수 있음.

### T7. [CL] 검증·문서
- `CharacterTest.unity`(동작 미리보기, 139 상태 버튼) + `Playground`/마트 맵에서 1인칭 플레이 체크리스트. 후드 4종·표정 5종·신발을 바꿔도 1인칭에서 이상 없는지 포함.
- 설정 화면의 1인칭 감도(`CameraLookScale`)와 충돌 없음 확인.
- 이 문서에 결과 기록, MR 설명 작성.

## 4. 결정이 필요한 것 (구현 직전에 함께 확인)

1. 접근법 A(진짜 몸+머리 숨김)로 갈지 — 권장 A.
2. 아래를 볼 때 몸통·발까지 보이게 둘지, 팔만 남길지(팔만이면 다리·몸통 본도 숨겨야 해서 A의 장점이 줄어듦) — 권장 전신.
3. 1인칭 FOV: 60 유지 vs 70~75.
4. 피격·기절 화면 피드백 형태(비네트 / 테두리 / 흐림).
5. 상체 피치 추종(T3)을 3인칭에도 적용할지.

## 5. 참고
- 카메라 Look 감도 스케일: `Core/Settings/CameraLookScale.cs` (1인칭/3인칭 별도).
- 이전 몸 숨김 근거: "1인칭에서는 내 몸이 화면을 가리지 않게 숨긴다. 그림자는 남겨 존재감을 유지한다." — A로 바꾸면 이 주석·정책도 갱신.
- Unity MCP가 이 세션에서 연결되지 않아 에디터 확인은 하지 않았다. T1부터는 에디터에서 직접 보며 진행.
