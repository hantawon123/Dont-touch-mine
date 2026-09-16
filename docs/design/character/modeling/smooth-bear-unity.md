# SmoothBear Unity 캐릭터

## 후드와 색상 커스터마이징

왼쪽 커스터마이징 메뉴는 **몸 색상 / 후드 / 신발 / 표정**이다.
후드를 선택하면 오른쪽 상단의 **후드 모양 / 후드 색상** 탭으로 항목을 나눈다.
왼쪽 후드를 누르면 기본적으로 모양 탭이 열리고, 탭 전환 시 고른 모양과 색상은 유지한다.
기존 신발·표정 항목은 유지한다. 후드는 곰·고양이·강아지·토끼 4종이며,
몸·후드·신발은 각각 28색이다. 팔레트 순서를 바꿔도 카테고리별 기본 선택은 유지한다.
확정된 팔레트와 기본 선택 ID는 `SmoothBearPalette.json`과 카탈로그를 기준으로 한다.

- 후드 메시: `Assets/_Game/Content/Characters/SmoothBear/Hoods/AnimalHoods.fbx`
- 제공된 후드 원본 보관: `source/blender/characters/SmoothBear/Hoods/` (원본 그대로 복사)
- 선택 목록과 메시/썸네일 연결: `Assets/_Game/Content/Config/AvatarPartCatalog.asset`
- 팔레트 원본: `Assets/_Game/Content/Config/SmoothBearPalette.json`
- 화면 미리보기 프리팹: `Assets/_Game/Content/Resources/AvatarPreview.prefab`
- 색상표: `artifacts/smooth-bear-unity/customization/palette.html`, `palette.png`

제공받은 Cat/Dog/Rabbit Blender 파일에서 후드만 추출했다. 원본 파일을 수정하지 않고,
현재 캐릭터 Hood의 로컬 좌표로 변환하여 같은 Head 뼈에 붙인다. 몸과 뼈대,
119개 애니메이션은 공통으로 사용하며 후드 MeshFilter의 메시만 교체한다.
`AvatarAppearanceApplier`의 BodyColor/HoodColor 두 대상이 각 재질의 색을
MaterialPropertyBlock으로 바꾼다. 후드의 니트 질감은 유지한다.

기존 계정 API의 네 필드를 유지한다. `hood` 값에 `hood_cat_ice`처럼 모양과 색을
함께 저장하고, 화면은 이를 두 선택으로 나눈다. 기존 `hood_pink` 등의 저장값은
곰 후드 + 해당 색으로 읽는다. 모든 생성 ID는 서버의 32자 제한 안에 있다.
계정 저장 요청은 기존 ClosetAppearanceSaver를 사용한다. 이번 검증은 로컬 화면과
저장값 왕복까지이며, 실제 서버 저장 및 다른 플레이어에게 외형을 복제하는 기능은
검증 범위에 포함하지 않는다.

`Game > Preview > Animal hood customization`으로 현재 씬 위에 로컬 미리보기를
열 수 있다. 실제 CharacterClosetView/Presenter를 사용하며, 여기의 적용 버튼은
미리보기 메모리에만 저장한다. Play 종료 시 미리보기가 닫힌다. 실제 Character 씬과
로비의 커스터마이징은 기존 계정 저장 경로를 사용한다.

재생성 순서:

1. Blender background에서 `Tools/export_custom_hoods.py` 실행.
2. 출력 AnimalHoods.fbx를 위 Hoods 폴더로 복사.
3. Unity 메뉴 `Game > Setup > Animal hood customization` 실행.
4. `Tools/generate_avatar_palette.py`로 팔레트 PNG/HTML 생성.

초기 12색 버전의 설정 도구는 576개 조합의 메시·독립 색상·저장값 왕복과, 119개 클립 × 4개 후드의
머리 부착 위치를 검사한다. 결과는 `customization-report.json`에 기록한다.
이후 확장된 커스터마이징 회귀 테스트는 51개다. 실행 결과의 시점과 범위는 XML을 확인한다.

## 최종 신발·표정·니트 질감

승인 소스와 재생성 절차는 `source/blender/characters/SmoothBear/APPROVED_CUSTOMIZATION.md`에 정리한다.
표정은 기본·반짝반짝·나른한 눈·사나운 눈·갈색 눈망울 5종을 제공한다.
좌우 드래그로만 회전하며 두 번 클릭하면 정면으로 돌아온다. 휠 확대와 세로 회전은 사용하지 않는다.
후드 모양과 표정에는 필요 없는 스크롤 표시를 숨기고 표정 썸네일은 선택 테두리 안쪽의 둥근 모서리에 맞춰 자른다.

후드 네 종류와 양쪽 신발은 `Game/Character/Straight Knit` 재질을 사용한다.
머리 중심을 따라 퍼지는 UV 대신 모델의 기본 자세에 고정한 두 방향의 세로 결을 섞어,
귀와 복면의 간격을 맞추고 옆면 늘어짐을 줄인다. 색상은 기존 `_BaseColor` 속성으로 독립 변경한다.
`Game > Setup > Approved straight hood and shoe knit`에서 재적용과 Unity 렌더 검증을 실행한다.
신발 에셋을 다시 생성해도 니트 재질 설정이 유지된다.
니트 적용 검증은 후드 색상 112건, 양쪽 신발 색상 56건, 대기·걷기·crawl 샘플 12건을 포함한다.

## 사용 위치

- 캐릭터 프리팹: `Assets/_Game/Content/Characters/SmoothBear/SmoothBear.prefab`
- 공통 모델: `Assets/_Game/Content/Characters/SmoothBear/SmoothBear.fbx`
- 애니메이션: 같은 폴더의 `Animations/`에 독립된 `.anim` 119개
- 동작 미리보기: `Assets/Scenes/CharacterTest.unity`에서 Play 후 동작 버튼 선택
- 실제 플레이어: `Assets/_Game/Content/Prefabs/PlayerCharacter.prefab`
- 네트워크 플레이어: 기존 `NetworkedPlayer.prefab`이 위 플레이어의 새 Visual을 사용
- Unity용 Blender 원본: `source/blender/characters/SmoothBear/SmoothBear.blend`

## 구성

사용자가 승인한 매끈한 곰 캐릭터의 기본 메시와 팔 실루엣을 사용한다.
몸통 중심은 Spine에 고정하고 팔·다리 연결 부위는 부드럽게 가중치를 연결하여
crawl 중 다리 움직임이 옆구리를 끌어당기지 않도록 했다.
겨드랑이와 어깨는 승인된 Blender 파일의 원래 뼈 영향값을 사용한다.
몸통 고정을 겨드랑이까지 확장하면 팔을 내릴 때 둥근 빈 공간이 생기므로,
위쪽은 원본의 팔·어깨 변형을 복원하고 다리 뼈의 영향만 Spine으로 옮긴다.
겨드랑이 복원은 `Tools/smooth_bear_skinning.py`에 있다.
골반은 원본의 고관절 영향값으로 되돌리고, 고정 영역은 몸통 중앙에 한정한다.
서 있는 자세에서 고관절의 오목한 주름을 부드럽게 채운 뒤, 그 보정을 기본 메시와
기존 블렌드셰이프에 함께 반영하여 허리·엉덩이·다리가 자연스럽게 이어지게 한다.

어깨·팔꿈치·고관절·무릎에 좌우 8개의 `Volume_` 보조 뼈를 사용한다.
부모 뼈와 자식 뼈를 반씩 따라가던 관절 표면은 보조 뼈가 받치며,
관절 중간 각도로 회전하여 크게 굽혔을 때 단면이 납작해지는 것을 줄인다.
이 회전은 119개 `.anim` 안에 구워 넣어 별도 런타임 스크립트 없이 재생된다.
모델 생성은 `Tools/build_smooth_bear_joint_volume.py`, 애니메이션 생성과 검증은
`Tools/SmoothBearPackage.cs`가 담당한다.
기존 숨쉬기와 웅크리기 보정은 새 메시로 옮겼다.
실제 사용하는 블렌드셰이프는 `Belly_Breath`, `Crouch_Groin_Flat` 두 개다.
모든 클립에 두 채널의 값을 명시하여 동작 전환 시 이전 값이 남지 않게 한다.

뼈대의 이름과 기본 행렬이 기존 동작과 일치하는 것을 확인했다.
기존 동작의 곡선만 독립 `.anim`으로 옮기고 반복 끝부분의 이음새를 보정했다.
복면·눈·입 등 부속 메시의 이전 모델 기준 위치/회전/크기 곡선 7,140개는 제거하여,
새 모델의 기본 부착 위치를 유지하면서 뼈대를 따라 움직이게 했다.
정적인 키와 중복된 FBX 내부 데이터는 줄였다. 이동/회전 곡선의 프레임 샘플
축약 오차는 0.00001, 블렌드셰이프 값은 0.001 이내다.

몸은 단일 서브메시와 `MAT_Capsule_Character` 재질을 사용한다. 몸 위에 추가 외곽선
재질을 다시 그리는 이전 설정을 제거했다. 기본 몸 색은 `#F3D646`, 복면은 `#D4ECFF`다.
게임과 테스트 씬은 공통 `SmoothBearAssets` 설정으로 같은 재질과 클립을 사용한다.

## 검증

- Unity 6000.3.22f1에서 119개 클립, 4,077프레임의 메시와 바인딩 확인
- 반복 끝/시작 연결과 crawl의 몸통 변형 확인
- 실제 프로젝트의 미리보기 139개, 플레이어 141개 Animator 상태 확인
- 설치된 Unity 프리팹으로 대기·걷기·웅크리기·기어가기·양손 들기 렌더 확인
- 플레이어·네트워크·단독 캐릭터 프리팹의 모델 참조, 재질 및 색상 확인
- 이전 CharacterTest 모델·테스트 애니메이션·Mixamo 임시 동작 등 참조가 정리된 에셋 182개 제거

검증 도구는 `Tools/SmoothBearPackage.cs`와
`Assets/_Game/Editor/SmoothBearProjectValidation.cs`다.
편집기 메뉴 `Game > Setup > Validate SmoothBear`로 프로젝트 연결을 다시 검사할 수 있다.
`Tools/build_smooth_bear_unity.py` 등은 이번 이전 작업의 생성·검증 스크립트다.
이전 입력 에셋은 Unity Assets 밖에 별도로 보관했으므로, 해당 이전 스크립트를
다시 실행할 때는 보관된 입력 경로가 필요하다.
