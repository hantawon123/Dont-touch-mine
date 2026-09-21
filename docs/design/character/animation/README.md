# 캐릭터 애니메이션 (Animation)

리깅·리타겟·액션 제작 현황을 둡니다.

- 파일명은 영문 소문자와 하이픈. 예: `animation-checklist.md`
- `.blend`는 `source/blender/characters/`에 두고, 이 폴더에는 파일명·액션명만 적습니다.
- 스크린샷이 생기면 같은 폴더에 두고 상대경로로 링크합니다.
- 제작·검수 상태는 `animation-checklist.md`를 봅니다.

## 필요한 액션

할당된 Jira 스토리 기준으로 게임에 들어가야 하는 클립입니다.

### 기본 이동 — [S15P21D205-646](https://ssafy.atlassian.net/browse/S15P21D205-646)

제자리 대기, WASD 네 방향 걷기·달리기.

| 액션명 | 용도 |
| --- | --- |
| `Idle` | 제자리 숨쉬기 대기 |
| `Walk_Forward` | 걷기 앞 |
| `Walk_Back` | 걷기 뒤 |
| `Walk_Left` | 걷기 왼쪽 |
| `Walk_Right` | 걷기 오른쪽 |
| `Run_Forward` | 달리기 앞 |
| `Run_Back` | 달리기 뒤 |
| `Run_Left` | 달리기 왼쪽 |
| `Run_Right` | 달리기 오른쪽 |

### 점프·낙하 — [S15P21D205-652](https://ssafy.atlassian.net/browse/S15P21D205-652)

도약 → 공중·낙하 → 착지.

| 액션명 | 용도 |
| --- | --- |
| `Jump` | 도약 |
| `Fall` | 공중 유지·낙하 |
| `Land` | 착지 |

### 웅크리기 — [S15P21D205-661](https://ssafy.atlassian.net/browse/S15P21D205-661)

앉기 키로 몸을 낮추고, 웅크린 채 대기·네 방향 이동 후 일어서기.

| 액션명 | 용도 |
| --- | --- |
| `Crouch_Start` | 웅크리기 진입 |
| `Crouch_Idle` | 웅크린 대기 |
| `Crouch_Walk_Forward` | 웅크린 전진 |
| `Crouch_Walk_Back` | 웅크린 후진 |
| `Crouch_Walk_Left` | 웅크린 좌측 |
| `Crouch_Walk_Right` | 웅크린 우측 |
| `Crouch_End` | 일어서기 |

### 엎드리기·기어가기 — [S15P21D205-677](https://ssafy.atlassian.net/browse/S15P21D205-677)

엎드리기 키로 눕고, 엎드린 채 대기·네 방향 기어가기 후 일어나기.
배는 바닥에 붙이고 손을 짚은 포복으로 만든다. 소총을 겨누는 Mixamo 엎드리기는 쓰지 않는다.

| 액션명 | 용도 |
| --- | --- |
| `Prone_Start` | 엎드리기 진입 |
| `Prone_Idle` | 엎드린 대기 |
| `Crawl_Forward` | 기어가기 앞 |
| `Crawl_Back` | 기어가기 뒤 |
| `Crawl_Left` | 기어가기 왼쪽 |
| `Crawl_Right` | 기어가기 오른쪽 |
| `Prone_End` | 일어나기 |

### 공격 — [S15P21D205-685](https://ssafy.atlassian.net/browse/S15P21D205-685)

서서, 점프 중, 웅크린 채로 때리기.

| 액션명 | 용도 |
| --- | --- |
| `Punch` | 오른손 펀치 (Party Animals 참고 시안 확정, 24프레임) |
| `Punch_Left` | 왼손 펀치 (리그 rest 공간 기준 반전, 24프레임) |
| `Punch_Combo` | 좌우 연타 루프: 12프레임 간격, 타격 직후 반대손이 나감 |
| `Punch_Air` | 점프 중 때리기 |
| `Punch_Crouch` | 웅크린 채 때리기 |

펀치 세트는 `Punch`, `Punch_Left`, `Punch_Combo` 각각에 `_Walk`, `_Run`,
`_Crouch`, `_Crouch_Walk` 변형이 있습니다. 총 15클립이며 CharacterTest의 전투 그룹에서 확인합니다.
단발은 0.8초, 연타는 양손 한 주기가 0.8초입니다. 이동 연타 파일은 원래 보행 주기와
24프레임 펀치 주기의 공배수 길이로 베이크하여 다리가 반복 경계에서 재시작하지 않습니다.

하체는 타격 때 반대쪽 무릎을 굽히고, 때리는 쪽 엉덩이를 살짝 반대·위로 올리며 다리가 그
방향을 따라 펴집니다. 발은 제자리에 두고 골반 롤·회전만 주고, 이동형은 원래 발 궤적에
골반 반동만 작게 더합니다. 다리 2본 IK로 발 위치를 맞추며 뼈 길이·발 목표 오차·루프 경계를 검증합니다.

인게임 공격 이벤트는 오른손/왼손 단발을 번갈아 재생하며, 공격 중 이동 자세가 바뀌어도
펀치 진행 시간을 유지합니다. `Punch_Combo*`는 연속 동작 검수용 루프입니다.
공격 판정·입력 쿨다운은 기존 전투 설정을 따릅니다.
재생성: `Tools/bake_party_punch_set.py`, 적용: `Tools/install_party_punch_set.py`.

### 피격·기절 — [S15P21D205-697](https://ssafy.atlassian.net/browse/S15P21D205-697)

가벼운·강한 피격. 기절하면 쓰러진 채 유지되다가 다시 일어나기.

| 액션명 | 용도 |
| --- | --- |
| `Hit_Light` | 가벼운 피격 |
| `Hit` | 강한 피격 |
| `Stun_Start` | 기절 진입 |
| `Stun_Idle` | 기절 유지 |
| `Stun_End` | 기절 회복·일어나기 |

`Hit`은 첫 4프레임에 머리·목·척추가 뒤로 젖혀지고 골반이 뒤·아래로 밀리며(발은 IK로 고정, 무릎만
굽음) 0.3초 유지 후 멍하게 앞으로 한 번 끄덕이며 회복합니다. 팔 휘두름은 이전 Mixamo 기반 클립을
타임워프해 유지합니다. 원본 `source/blender/characters/SmoothBear/Combat/SmoothBear_Hit.blend`,
재생성 `Tools/build_smooth_bear_hit.py`, 내보내기 `Tools/export_smooth_bear_hit_clip.py`.
자세별 변형(Hit_Walk·Run·Crouch·Crouch_Walk·Prone·Crawl, Carry_TwoHands_Hit*)은 Unity의
CarryTwoHandsHitClipBaker가 Hit 상체를 각 자세 베이스 위에 얹어 만듭니다.

`Stun_Start`는 같은 blend의 `Hit` 위에 이어 만듭니다(`Tools/build_smooth_bear_stun_start.py`).
첫 0.3초는 Hit 충격 그대로, 0.6초까지 바로 서려다 머리가 앞으로 떨어지고, 1.25초까지 뒤로 넘어져
착지 반동 뒤 `Stun_Idle` 첫 프레임과 같은 자세를 2.2초까지 유지합니다. 넉아웃 타격은 Hit 대신
이 클립만 재생하므로(`PlayerAnimationDriver.PlayHit`) 충격 연출을 여기 포함합니다.

### 물건 집기·내려놓기 — [S15P21D205-710](https://ssafy.atlassian.net/browse/S15P21D205-710)

낮은 곳, 허리 높이, 높은 곳에서 집어 들고 같은 높이로 내려놓기.

| 액션명 | 용도 |
| --- | --- |
| `Pickup_Low` | 낮은 곳 집기 |
| `Pickup` | 허리 높이 집기 |
| `Pickup_High` | 높은 곳 집기 |
| `PutDown_Low` | 낮은 곳 내려놓기 |
| `PutDown` | 허리 높이 내려놓기 |
| `PutDown_High` | 높은 곳 내려놓기 |

### 물건 들고 이동·던지기 — [S15P21D205-718](https://ssafy.atlassian.net/browse/S15P21D205-718)

든 채 대기·기존 이동과 조합, 좌클릭으로 던지기.

| 액션명 | 용도 |
| --- | --- |
| `Carry_Idle` | 물건 들고 대기 |
| `Carry_Walk` / `Carry_Run` | 든 채 걷기·뛰기. 미리보기는 기존 이동에 오른팔 들기를 베이크 |
| `Carry_Upper` | 서서 이동할 때 상체 들기 자세 |
| `Carry_Crouch_Idle` | 웅크린 채 들기. 오리걸음은 `Carry_Crouch_Idle_Walk_*` |
| `Carry_Prone_Idle` | 엎드린 채 들기. 기어가기는 `Carry_Crawl_*` |
| `Throw` | 던지기 |

### 물건 들기 연결 규칙

- `Pickup_Low`는 물건을 손에 붙이는 프레임을 기준으로, 앞뒤 몸통 흔들림을 작게 유지한다.
- 집기 완료 시 `Carry_Idle`로 전환한다. 서서 걷기·뛰기는 `Carry_Upper`를 상체 레이어로
  겹치고, 웅크리기·엎드리기는 각각 `Carry_Crouch_Idle`, `Carry_Prone_Idle`을 사용한다.
- 게임 연결 시 배치와 일반 드롭은 `PutDown_*`의 물건 접촉 프레임에서 HoldPoint 부모
  연결을 해제한다. 드롭은 해당 위치에서 물리를 켜며, 배치는 배치 미리보기에서 확정한
  위치·회전으로 해제한다.

## 목록

- `animation-checklist.md` — 플레이어 액션 제작·검수 체크리스트
