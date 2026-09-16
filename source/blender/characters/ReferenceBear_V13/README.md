# Reference Bear V13 — Blender 캐릭터

`ReferenceBear_V13.blend`를 Blender 5.2에서 여세요. 제공된 V12의 Bear_Body 메시·가중치를 활용하고 프로젝트 First의 28개 본과 기존 액션 12개를 연결했습니다. 참고 이미지는 파일에 포함되어 있습니다.

## 커스터마이징

Outliner의 `CUSTOMIZE` 선택 → Object Properties → Custom Properties에서 값을 조절합니다. 기본값은 모두 0입니다.

- 복면: `Ears_Small`, `Ears_Rabbit`로 귀 형태 조절. `SLOT | Hood`에 복면 본체와 짧은 섬유가 함께 있습니다.
- 눈: `Blink_L/R`로 좌우 눈 감기, `Look_X/Y`로 시선, `Angry`로 눈 기울기 조절.
- 입: 기본 미소, `Mouth_Frown`으로 찡그림, `Mouth_Open`으로 O형 입.
- 신발: `Shoes_Wide`로 폭 조절. 좌우 신발은 별도 메시입니다.
- 색상: `V13 |`로 시작하는 재질의 Principled BSDF → Base Color 변경.

눈 감기와 입 모양은 셰이프 키 방식입니다. 눈·입은 함께 얼굴에 포함된 한 장의 텍스처가 아니라 각각 교체 가능한 메시입니다. 복면/표정/신발 슬롯은 서로 독립적입니다. 복면을 교체할 때는 본체와 섬유를 함께 교체하세요. 입 모양 및 귀 변형은 한 종류씩 0–1로 사용하는 것을 권장합니다.

편리한 패널이 필요하면 Blender Text Editor에서 내장 `OPEN_CUSTOMIZER.py`를 선택하여 Run Script를 누르세요. 3D View의 N 사이드바 → Character에 슬라이더·색상·액션 버튼이 나타납니다. 재실행할 수 있으며, Blender를 다시 열면 패널 스크립트를 다시 실행해야 합니다. 일반 Custom Properties 슬라이더는 패널 없이 작동합니다.

## 액션

패널 버튼 또는 내장 `SELECT_ACTION.py`에서 CLIP을 선택합니다. 리그 액션과 몸체 셰이프 액션을 함께 선택합니다.

Idle, Walk_Forward, Run_Forward, Jump, Land, Crouch_Idle, Crawl_Forward, Carry_TwoHands, Carry_TwoHands_Crouch_Idle, Throw_TwoHands, Punch, Stun_Idle.

First의 본 이름·계층·기준 행렬을 바꾸지 않고, 새 몸체의 기준 메시를 First Idle에 맞췄습니다. 원래 몸체용 보정 델타는 새 토폴로지에 맞지 않아 제거했고, 호흡 변형을 새로 적용했습니다. 나머지 보정 채널은 이름을 유지한 중립 상태이며 포즈 표면 완화를 적용했습니다.

## 검증 및 범위

`verification.json`은 각 액션의 시작·중간·끝 프레임 메시 좌표, 파츠의 가중치·리그 연결, 10개 조절값의 드라이버 검사를 기록합니다. `preview-front.png`와 액션별 PNG는 실제 Blender 렌더입니다.

모든 포즈에서 관통이나 접촉이 정확함을 보증하는 충돌 검사는 아닙니다. First와 참고 이미지의 관절·체형 차이 때문에 극단적인 자세에는 추가 보정 조형이 필요할 수 있습니다. 고해상도 Blender 편집본이며, Unity 런타임 의상 교체 시스템·LOD·재질 베이크·FBX 내보내기는 포함하지 않습니다.

## 재생성

저장소 루트에서 Blender 백그라운드 모드로 다음 스크립트를 순서대로 실행합니다. 생성본에 수동 수정이 있다면 별도 저장 후 실행하세요.

1. Tools/build_reference_bear_v13.py
2. Tools/finish_reference_bear_v13.py
3. Tools/repair_reference_bear_v13.py
4. Tools/polish_reference_bear_v13.py
5. Tools/verify_reference_bear_v13.py

원본 Downloads의 V12와 기존 ModularThief_01 파일은 변경하지 않았습니다.
