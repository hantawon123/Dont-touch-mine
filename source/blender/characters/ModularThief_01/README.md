# Modular Thief 01 — Blender 검토본

First 대기 FBX의 리그와 몸통을 바탕으로 만든 분리형 캐릭터. 기존 게임 모델을 교체하지 않는다.

- `ModularThief_01.blend`: 편집 원본, 스튜디오, 기존 액션 12개.
- `ModularThief_01_Idle.fbx`: 캐릭터 메시·리그·대기 애니메이션. 스튜디오 오브젝트 제외.
- `preview-hero.png`: 실제 Blender 렌더.
- `preview-backpack.png`: 등 뒤의 독립 백팩 구조 확인용 렌더.
- `preview-Crouch_Idle.png`, `preview-Crawl_Forward.png`, `preview-Carry_TwoHands.png`: 실제 기존 액션을 적용한 렌더. 운반물은 표시하지 않았다.
- `validation.json`, `verification.json`: 리그 비교, 프레임 샘플, FBX 재임포트 검사 기록.

## 부품 편집

Outliner에서 Body, Hood, Feet_L/R, Eye_White/Pupil/Glint_L/R, Mouth_Smile을 선택한다. 눈은 여러 메시로 구성되는 하나의 논리 부품이다. 각 재질의 Base Color로 색을 바꿀 수 있다.

Object Data Properties → Shape Keys에서 `Custom_`으로 시작하는 키를 0–1 범위로 조절한다.

| 부품 | 조절값 |
|---|---|
| Body | Custom_Belly: 배 볼륨 |
| Hood | Custom_SmallEars: 귀 크기 |
| Eyes | Custom_EyeSize: 눈 크기. 좌우 흰자·동공·반짝임을 함께 조정 |
| Mouth | Custom_WideSmile: 미소 폭 |
| Feet | Custom_RoundFeet: 발의 폭·길이 |

형태가 크게 다른 메시로 교체할 때도 동일 리그·바인드 포즈·부착 기준을 사용해야 한다. 이 파일은 Unity에서의 파츠 교체 UI나 저장 기능을 구현한 것이 아니다. 커스텀 셰이프의 최대값과 모든 액션 조합에 대한 관통 검사는 아직 수행하지 않았다.

## 액션 보기

Blender Text Editor에서 내장 `SELECT_ACTION.py`를 선택한다. `CLIP = "Idle"`을 아래 이름 중 하나로 바꾸고 Run Script를 누른 다음 타임라인을 재생한다. 리그 액션과 몸통 보정 셰이프 액션을 같이 선택하므로 Action 드롭다운만 바꾸는 것보다 안전하다.

Idle, Walk_Forward, Run_Forward, Jump, Land, Crouch_Idle, Crawl_Forward, Carry_TwoHands, Carry_TwoHands_Crouch_Idle, Throw_TwoHands, Punch, Stun_Idle.

## 검증 범위와 남은 작업

- First의 본 28개 이름·계층 보존. 선택한 원본 액션의 바인드 행렬 차이는 0.0001 미만.
- 각 액션의 대표 7개 프레임에서 평가된 메시 좌표가 유한하고 비정상적으로 발산하지 않음을 확인.
- FBX 재임포트에서 모든 부품 이름·셰이프 키·리그 연결 보존, 바인드 행렬 최대 차이 약 0.00000853.
- 대기·웅크리기·포복·양손 들기의 실제 렌더를 확인했다. 손 접지·복면과 어깨·운반물과 손의 접촉을 보증하는 충돌 검사는 아니다.
- First의 관절 위치를 유지한 결과 스케치보다 팔과 몸통이 길다. 더 짧은 체형에는 리그/액션 보정 또는 추가 메시 조형이 필요하다.
- 목/어깨의 표면 흐름, 극단적인 자세의 겹침, 니트 디테일은 후속 조형 대상이다.
- Blender의 절차적 표면 재질은 Unity용으로 베이크하거나 Unity 재질로 재구성해야 한다.
- Unity Generic 리그 연결, 런타임 파츠 교체와 Eyes/Mouth 별도 카테고리는 후속 통합 작업이다.

## 재생성

저장소 루트에서 Blender를 `--background --factory-startup --python Tools/build_modular_thief.py`로 실행한다. 이 폴더의 생성물을 다시 쓴다. 수동으로 수정한 원본은 별도 버전으로 저장한다.

검증은 `--background --factory-startup --python Tools/verify_modular_thief.py`로 실행한다.
