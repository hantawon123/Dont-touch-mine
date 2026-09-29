# 캐릭터 블렌더 원본

플레이어 캐릭터의 리깅·리타겟·액션 작업 파일(`.blend`)을 둡니다.

- 파일명은 영문과 언더스코어. 예: `PlayerCapsule_CuteJump.blend`
- `*.blend1` 백업은 커밋하지 않습니다.
- 액션 제작 현황은 `docs/design/character/animation/animation-checklist.md`에 적습니다.

## 게임 적용

- 메시·리그: `Assets/_Game/Content/Models/`
- 애니메이션 클립: `Assets/_Game/Content/Animations/`

## 목록

- `PlayerCapsule_CuteJump.blend` — 현재 통합 파일. 걷기·달리기·점프 액션 포함. 기본 액션 `Jump_Cute`
- `PlayerCapsule_Idle_Breathing_2s.blend` — 기본 대기. 액션 `Idle_Breathing`
- `PlayerCapsule_Fall_Flutter.blend` — 공중 유지·낙하. 액션 `Fall_Flutter`
- `PlayerCapsule_Land_Matched.blend` — 착지. 액션 `Land_Matched`
- `PlayerCapsule_Run_SideArms.blend` — 팔을 옆으로 벌린 달리기. 액션 `Run_SideArms`, `Run_SideArms_Forward`. 좌·우·후진은 `Tools/make_first_run_directions.py`로 First FBX 위에 제작한다.
- `PlayerCapsule_Walk_Wide_Clean.blend` — 보폭 넓힌 걷기. 액션 `Walk_Wide_Clean`, `Walk_Wide_Clean_Forward`. 좌·우·후진은 `Tools/make_first_walk_directions.py`로 First FBX 위에 제작한다.
- `PlayerCapsule_Crouch_KneesUp_Only.blend` — 무릎 든 웅크리기 세트. Idle·4방향 이동·일어서기/앉기 전환
- 엎드리기·기어가기는 `.blend` 없이 `Tools/make_first_prone.py`로 First 대기 FBX 위에 제작한다. 손은 바닥, 소총 포즈는 제외.
- 든 채 걷기·뛰기는 `Tools/make_first_carry_locomotion.py`로 기존 이동 FBX에 `Carry_Idle` 오른팔을 얹는다.
- 든 채 웅크리기·엎드리기는 `Tools/make_first_carry_postures.py`로 만든다. 엎드리기는 오른손만 가슴 앞으로 든다.
- 던지기는 `Tools/make_first_throw.py`로 들기에서 팔을 옆·위로 연 뒤 직선으로 던진다.
