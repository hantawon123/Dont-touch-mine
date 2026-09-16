# 둥근 슬립온 신발 — Blender 초안

## 짧고 둥근 신발 수정본 (최신)

`BasicPlayerCapsule_WithWalk_Bear_Ears_Shoes_Compact.blend`는 LowTop보다 앞뒤 길이를 약 16% 줄이고, 앞코와 발등을 살짝 볼록하게 다듬은 수정본입니다. 낮은 발목 높이와 파란색 재질을 유지했습니다.

생성: `Tools/round_smooth_bear_shoe_uppers.py -- --compact`.
확대/측면 렌더 및 메시 검증: `artifacts/smooth-bear-unity/shoes/compact/`.

## 낮은 신발과 발등 수정본

`BasicPlayerCapsule_WithWalk_Bear_Ears_Shoes_LowTop.blend`는 전체 높이를 약 29% 낮추고 앞코를 앞으로 늘려 발목과 앞코 사이에 완만한 발등 곡면을 만든 수정본입니다.

생성: `Tools/round_smooth_bear_shoe_uppers.py -- --low-top`.
확대/측면 렌더 및 메시 검증: `artifacts/smooth-bear-unity/shoes/low-top/`.

## 둥근 윗부분 수정본

`BasicPlayerCapsule_WithWalk_Bear_Ears_Shoes_Rounded.blend`는 발등을 볼록하게 다듬고 발목 입구를 두툼한 곡면으로 마감한 수정본입니다. 기존 파란색과 얇은 밑창을 유지했습니다.

수정 스크립트: `Tools/round_smooth_bear_shoe_uppers.py`.
확대/측면 렌더 및 메시 검증: `artifacts/smooth-bear-unity/shoes/rounded/`.

## 공통 구성

`BasicPlayerCapsule_WithWalk_Bear_Ears_Shoes.blend`를 열면 기존 캐릭터가 신발을 신은 상태로 표시됩니다.

- `CUSTOMIZATION - Shoes` 컬렉션의 `Shoes.L`, `Shoes.R`가 좌우 신발입니다.
- `MAT_Shoes_Color`의 Base Color로 신발 색상을 바꿉니다. 기본색: `#789BC8`.
- `MAT_Shoes_Sole`은 얇은 밑창 경계입니다. 기본색: `#6586B3`.
- Subdivision 수정자를 적용하지 않아 기본 메시를 계속 편집할 수 있습니다.
- 발목 입구와 안쪽 공간을 가진 닫힌 메시이며, UV가 포함되어 있습니다.
- 각 신발은 해당 `Foot.L` / `Foot.R` 뼈를 따라갑니다.
- 캐릭터 몸의 정점은 원본 그대로입니다. 이 초안의 Unity 에셋 교체는 아직 하지 않았습니다.

생성: `Tools/build_smooth_bear_shoes.py` → `Tools/finish_smooth_bear_shoes.py`.
착용/확대 렌더와 검증 기록: `artifacts/smooth-bear-unity/shoes/`.
