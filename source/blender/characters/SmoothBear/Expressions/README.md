# 반짝이는 눈 표정 — Blender

## 추가 표정: 크게 뜬 갈색 눈

`BasicPlayerCapsule_Bear_BrownGlossyEyes.blend`: 위아래 눈꺼풀이 둥글게 감싸는 열린 눈, 따뜻한 갈색 홍채와 큰 동공, 오른쪽 위 반사광을 가진 별도 표정입니다. 홍채와 반사광은 흰자 표면 텍스처이며 `BrownGlossyEyes_BaseColor.png`가 파일에 포함됩니다.

편집 컬렉션: `EXPRESSION - Brown Glossy Eyes`.
생성: `Tools/build_drowsy_eye_expression.py -- --brown-open`.
미리보기: `artifacts/smooth-bear-unity/expressions/brown-glossy/`.

## 추가 표정: 윤기 있는 나른한 눈

최신 높이 조정본: `BasicPlayerCapsule_Bear_DrowsyGlossyEyes_NoLowerLids_Raised.blend`. 아랫눈두덩이 없는 상태에서 윗눈꺼풀 경계를 0.005 올렸습니다.
생성: `Tools/build_drowsy_eye_expression.py -- --raised-no-lower`.
미리보기: `artifacts/smooth-bear-unity/expressions/drowsy-raised-no-lower/`.

아랫눈두덩 제거본: `BasicPlayerCapsule_Bear_DrowsyGlossyEyes_NoLowerLids.blend`. 양쪽 아래 눈꺼풀 메시만 제거했습니다.
생성: `Tools/remove_drowsy_lower_lids.py`.
미리보기: `artifacts/smooth-bear-unity/expressions/drowsy-no-lower-lids/`.

`BasicPlayerCapsule_Bear_DrowsyGlossyEyes.blend`: 눈꼬리가 살짝 내려간 반쯤 감긴 눈과 둥근 아래 눈꺼풀, 회청색 홍채를 적용한 별도 표정입니다. 홍채의 짙은 테두리·동공·가는 방사형 무늬·흰색 반사광 두 곳은 모두 흰자 표면 텍스처로 표현합니다.

`DrowsyGlossyEyes_BaseColor.png`는 Blender 파일에도 포함되어 있습니다.
편집 컬렉션: `EXPRESSION - Drowsy Glossy Eyes`.
생성: `Tools/build_drowsy_eye_expression.py`.
미리보기: `artifacts/smooth-bear-unity/expressions/drowsy-glossy/`.

사나운 눈 확정본은 `BasicPlayerCapsule_Bear_FierceEyes_Bigger.blend`이며 그대로 보관했습니다.

## 추가 표정: 살짝 사나운 눈

눈 전체 확대본: `BasicPlayerCapsule_Bear_FierceEyes_Bigger.blend`. RaisedMore의 눈 중심 위치를 유지하면서 흰자·홍채·반사광·위아래 눈두덩을 함께 10% 키웠습니다. 눈매의 경사는 그대로입니다.
생성: `Tools/enlarge_fierce_eyes.py`.
미리보기 및 검증: `artifacts/smooth-bear-unity/expressions/fierce-bigger/`.

최신 높이 조정본: `BasicPlayerCapsule_Bear_FierceEyes_LargeIris_RaisedMore.blend`. Raised에서 윗눈꺼풀 경계를 0.004 더 올렸습니다. 경사, 홍채, 아래 눈두덩은 유지했습니다.
생성: `Tools/build_sleepy_eye_expression.py -- --fierce-lift-more`.
미리보기: `artifacts/smooth-bear-unity/expressions/fierce-raised-more/`.

윗눈두덩 높이 미세 조정본: `BasicPlayerCapsule_Bear_FierceEyes_LargeIris_Raised.blend`. LargeIris 버전에서 윗눈꺼풀 경계를 0.004 올렸습니다. 경사와 홍채 크기는 유지했습니다.

생성: `Tools/build_sleepy_eye_expression.py -- --fierce-lift`.
미리보기: `artifacts/smooth-bear-unity/expressions/fierce-raised/`.

최신 수정본: `BasicPlayerCapsule_Bear_FierceEyes_LargeIris.blend`.
윗눈꺼풀 경사를 13도에서 9도로 완만하게 줄이고, 경계를 0.013 내렸습니다. 인쇄형 홍채의 지름을 40% 키웠으며 작은 반사광이 눈꺼풀 아래에 보이도록 위치를 맞췄습니다.

생성: `Tools/build_sleepy_eye_expression.py -- --fierce-refined`.
미리보기: `artifacts/smooth-bear-unity/expressions/fierce-refined/`.

`BasicPlayerCapsule_Bear_FierceEyes.blend`: 윗눈꺼풀을 미간 쪽으로 13도 내려 기울이고, 아래 눈꺼풀을 완만한 곡선으로 다듬었습니다. 확정된 눈두덩 볼륨을 유지하며, 둥근 눈동자와 작은 반사광은 `FierceEyes_Print_BaseColor.png`로 흰자 표면에 인쇄됩니다. 텍스처는 Blender 파일에 포함됩니다.

편집 컬렉션: `EXPRESSION - Fierce Eyelids`.
생성: `Tools/build_sleepy_eye_expression.py -- --fierce`.
미리보기 및 검증: `artifacts/smooth-bear-unity/expressions/fierce/`.

## 추가 표정: 반쯤 감긴 눈

최신 수정본: `BasicPlayerCapsule_Bear_SleepyEyes_Volume.blend`.
위·아래 눈두덩을 도톰하게 만들고 아래쪽 경계를 0.01 올렸습니다. 눈동자 모서리를 더 둥글게 다듬고 오른쪽 위에 작은 흰색 반사광을 인쇄했습니다. 사용 텍스처는 `SleepyEyes_RoundedGlint_BaseColor.png`이며 파일에 포함됩니다.

생성: `Tools/build_sleepy_eye_expression.py -- --refined`.
미리보기: `artifacts/smooth-bear-unity/expressions/sleepy-refined/`.

`BasicPlayerCapsule_Bear_SleepyEyes.blend`는 윗눈꺼풀이 내려온 무심하고 졸린 눈 표정입니다. 모서리가 둥근 사각 눈동자는 흰자 표면의 `SleepyEyes_Print_BaseColor.png` 텍스처로 표현하며 파일 안에도 포함됩니다.

`EXPRESSION - Sleepy Eyelids` 컬렉션의 위·아래 눈꺼풀을 각각 편집할 수 있습니다. 눈꺼풀의 `MAT_Eyelids_Skin` 재질은 몸색과 맞춰 사용합니다. 확정한 인쇄형 반짝이 눈 파일과 신발은 유지했습니다.

생성: `Tools/build_sleepy_eye_expression.py`.
미리보기 및 검증: `artifacts/smooth-bear-unity/expressions/sleepy/`.

## 흰자 표면 인쇄형 (최신)

`BasicPlayerCapsule_Bear_SparkleEyes_Printed.blend`: 검은 눈동자, 별빛, 반사광을 흰자 메시의 Base Color 텍스처로 표현합니다. 별도의 눈동자 구체나 돌출된 별 메시를 사용하지 않습니다. `SparkleEyes_Print_BaseColor.png`는 Blender 파일에도 포함되어 있습니다.

아이라인은 흰자와 얼굴의 실제 교차 경계를 따라 붙이고, 속눈썹은 양쪽 두 가닥씩 그 경계에서 이어집니다. 기존 눈 크기·미간과 흰자 메시, 확정 신발을 유지했습니다.

생성: `Tools/print_sparkle_eyes.py`.
미리보기 및 경계 검증: `artifacts/smooth-bear-unity/expressions/sparkle-printed/`.

## 속눈썹·눈 간격 수정본 (최신)

`BasicPlayerCapsule_Bear_SparkleEyes_Lashes.blend`: 눈 크기 8% 축소, 양쪽 눈을 각각 바깥쪽으로 0.01 이동, 아이라인을 각각 바깥쪽으로 8도 회전하고 눈마다 속눈썹 두 가닥을 추가했습니다. 눈동자와 별빛도 함께 축소·이동했습니다.

속눈썹은 `Eye_Sparkle_Lash1_L/R`, `Eye_Sparkle_Lash2_L/R` 메시로 각각 편집할 수 있습니다.
생성: `Tools/refine_sparkle_eye_expression.py`.
미리보기 및 검증: `artifacts/smooth-bear-unity/expressions/sparkle-refined/`.

`BasicPlayerCapsule_Bear_SparkleEyes.blend`는 확정된 Compact 신발을 신은 캐릭터에 반짝이는 눈을 추가한 파일입니다.

- `EXPRESSION - Sparkle Eyes`: 큰 검은 눈동자, 연노란색 큰 별/작은 별, 흰색 반사광, 위쪽 눈 윤곽선.
- 추가한 요소는 각각 편집 가능한 메시이며 `Head` 뼈를 따라갑니다.
- 기본 눈동자 `Eye_Pupil_L/R`는 숨김 상태로 보관했습니다. 기본 눈으로 돌아가려면 Sparkle 컬렉션의 화면/렌더 표시를 끄고 기본 눈동자의 표시를 켭니다. 흰자는 공통으로 사용합니다.
- 몸, 후드, 확정 신발의 정점은 유지했습니다.
- 현재 결과는 Blender 표정 초안입니다.

생성 스크립트: `Tools/build_sparkle_eye_expression.py`.
미리보기 및 검증: `artifacts/smooth-bear-unity/expressions/sparkle/`.
