# 캐릭터 애니메이션 체크리스트

## 관리 기준

- [x] : 초안 제작 완료
- [ ] : 미제작 또는 추가 작업 필요

- 게임 연결, 반복 재생, 발 미끄러짐, 메시 겹침은 별도 검수
- 실제 게임에 없는 행동은 목록에서 제외

## 현재 통합 파일

- 파일명: `PlayerCapsule_CuteJump.blend`
- 위치: `source/blender/characters/`
- 기존 걷기·달리기·점프 액션 포함
- 기본 선택 액션: `Jump`

## 기본 이동

- [x] 기본 대기
  - 파일명: `PlayerCapsule_Idle_2s.blend`
  - 액션명: `Idle`
  - 반복: O
  - 재생 구간: 약 2초
- [x] 제자리 걷기
  - 파일명: `PlayerCapsule_Walk_Forward.blend`
  - 액션명: `Walk_Forward`
  - 반복: O
  - 재생 구간: 1~24 / 30fps
  - 비고: 25프레임 클로저. 보폭 넓힘
- [x] 제자리 후진
  - 파일명: `PlayerCapsule_Walk_Forward.blend`
  - 액션명: `Walk_Back`
  - 반복: O
  - 재생 구간: 1~24 / 30fps
  - 비고: `Walk_Forward` 발 스윙을 뒤로 재배치. `Tools/make_first_walk_directions.py`
- [x] 제자리 좌측 걷기
  - 파일명: `PlayerCapsule_Walk_Forward.blend`
  - 액션명: `Walk_Left`
  - 반복: O
  - 재생 구간: 1~24 / 30fps
  - 비고: 얼굴은 정면. Left = +localX. 발끝은 전방
- [x] 제자리 우측 걷기
  - 파일명: `PlayerCapsule_Walk_Forward.blend`
  - 액션명: `Walk_Right`
  - 반복: O
  - 재생 구간: 1~24 / 30fps
  - 비고: 얼굴은 정면. 발끝은 전방
- [x] 전진 걷기
  - 파일명: `PlayerCapsule_Walk_Forward.blend`
  - 액션명: `Walk_Forward`
  - 반복: O
  - 재생 구간: 1~24 / 30fps
  - 비고: 전진 이동 포함. 사이클당 약 0.545 유닛
- [x] 제자리 달리기
  - 파일명: `PlayerCapsule_Run_Forward.blend`
  - 액션명: `Run_Forward`
  - 반복: O
  - 재생 구간: 1~20 / 30fps
  - 비고: 21프레임 클로저. Unity는 중복 끝 프레임을 빼고 0~19로 루프한다
- [x] 제자리 후진 달리기
  - 파일명: `PlayerCapsule_Run_Forward.blend`
  - 액션명: `Run_Back`
  - 반복: O
  - 재생 구간: 1~20 / 30fps
  - 비고: `Run_Forward` 발 스윙을 뒤로 재배치. `Tools/make_first_run_directions.py`
- [x] 제자리 좌측 달리기
  - 파일명: `PlayerCapsule_Run_Forward.blend`
  - 액션명: `Run_Left`
  - 반복: O
  - 재생 구간: 1~20 / 30fps
  - 비고: 얼굴은 정면. Left = +localX. 무릎은 앞으로만 접힘
- [x] 제자리 우측 달리기
  - 파일명: `PlayerCapsule_Run_Forward.blend`
  - 액션명: `Run_Right`
  - 반복: O
  - 재생 구간: 1~20 / 30fps
  - 비고: 얼굴은 정면. 무릎은 앞으로만 접힘
- [x] 전진 달리기
  - 파일명: `PlayerCapsule_Run_Forward.blend`
  - 액션명: `Run_Forward`
  - 반복: O
  - 재생 구간: 1~20 / 30fps
  - 비고: 전진 이동 포함. 사이클당 약 0.9 유닛



## 점프·낙하

- [x] 귀여운 점프 — 전체 동작
  - 파일명: `PlayerCapsule_CuteJump.blend`
  - 액션명: `Jump`
  - 반복: X
  - 재생 구간: 1~42 / 30fps
  - 비고: 준비→도약→공중→착지 포함. 수직 이동 포함.
- [ ] 도약
  - 파일명:
  - 액션명: `Jump`
  - 반복: X
  - 비고: 기존 점프에서 분리·보완
- [x] 공중 유지·낙하
  - 파일명: `PlayerCapsule_Fall.blend`
  - 액션명: `Fall`
  - 반복: O
  - 재생 구간: 1~48 / 30fps
  - 비고: 49프레임 클로저. 수직 이동 없음
- [x] 착지
  - 파일명: `PlayerCapsule_Land.blend`
  - 액션명: `Land`
  - 반복: X
  - 재생 구간: 1~21 / 30fps
  - 비고: 실제 지면 접촉 시 재생. 진입은 Fall 1프레임, 종료는 Idle과 맞춤



## 웅크리기

> 이동 중 몸을 낮추는 행동 기준.
> 바닥에 앉는 행동이 필요하면 Sit 계열을 별도로 추가.
> 한 blend에 같은 계열 액션을 모아 둔다. 자세·가중치가 맞고 Unity에서 클립만 나누면 된다.

- [x] 웅크리기 진입
  - 파일명: `PlayerCapsule_Crouch_KneesUp_Only.blend`
  - 액션명: `Crouch_Start`
  - 반복: X
  - 재생 구간: 24프레임 / 30fps
- [x] 웅크린 대기
  - 파일명: `PlayerCapsule_Crouch_KneesUp_Only.blend`
  - 액션명: `Crouch_Idle`
  - 반복: O
- [x] 웅크린 전진
  - 파일명: `PlayerCapsule_Crouch_KneesUp_Only.blend`
  - 액션명: `Crouch_Walk_Forward`
  - 반복: O
  - 재생 구간: 1~36 / 30fps
  - 비고: 제자리 루프. 이동은 코드에서 처리
- [x] 웅크린 후진
  - 파일명: `PlayerCapsule_Crouch_KneesUp_Only.blend`
  - 액션명: `Crouch_Walk_Back`
  - 반복: O
  - 재생 구간: 1~36 / 30fps
- [x] 웅크린 좌측 이동
  - 파일명: `PlayerCapsule_Crouch_KneesUp_Only.blend`
  - 액션명: `Crouch_Walk_Left`
  - 반복: O
  - 재생 구간: 1~36 / 30fps
  - 비고: 캐릭터 기준 Left = +localX
- [x] 웅크린 우측 이동
  - 파일명: `PlayerCapsule_Crouch_KneesUp_Only.blend`
  - 액션명: `Crouch_Walk_Right`
  - 반복: O
  - 재생 구간: 1~36 / 30fps
- [x] 웅크린 상태에서 일어서기
  - 파일명: `PlayerCapsule_Crouch_KneesUp_Only.blend`
  - 액션명: `Crouch_End`
  - 반복: X
  - 재생 구간: 24프레임 / 30fps



## 엎드리기·기어가기

배 바닥에 붙이고 손을 짚은 포복. Mixamo 소총 엎드리기는 쓰지 않는다.
`Tools/make_first_prone.py`로 First 대기 메시 위에 제작한다.

- [x] 엎드리기 진입
  - 파일명: `FirstPlayerCapsule_Prone_Start.fbx`
  - 액션명: `Prone_Start`
  - 반복: X
  - 재생 구간: 1~24 / 30fps
- [x] 엎드린 대기
  - 파일명: `FirstPlayerCapsule_Prone_Idle.fbx`
  - 액션명: `Prone_Idle`
  - 반복: O
  - 재생 구간: 1~60 / 30fps
- [x] 앞으로 기어가기
  - 파일명: `FirstPlayerCapsule_Crawl_Forward.fbx`
  - 액션명: `Crawl_Forward`
  - 반복: O
  - 재생 구간: 1~36 / 30fps
- [x] 뒤로 기어가기
  - 파일명: `FirstPlayerCapsule_Crawl_Back.fbx`
  - 액션명: `Crawl_Back`
  - 반복: O
  - 재생 구간: 1~36 / 30fps
- [x] 왼쪽 기어가기
  - 파일명: `FirstPlayerCapsule_Crawl_Left.fbx`
  - 액션명: `Crawl_Left`
  - 반복: O
  - 재생 구간: 1~36 / 30fps
- [x] 오른쪽 기어가기
  - 파일명: `FirstPlayerCapsule_Crawl_Right.fbx`
  - 액션명: `Crawl_Right`
  - 반복: O
  - 재생 구간: 1~36 / 30fps
- [x] 엎드린 상태에서 일어나기
  - 파일명: `FirstPlayerCapsule_Prone_End.fbx`
  - 액션명: `Prone_End`
  - 반복: X
  - 재생 구간: 1~24 / 30fps



## 공격

- [x] 기본 때리기
  - 파일명: `FirstPlayerCapsule_Punch.fbx`
  - 액션명: `Punch`
- [ ] 점프하면서 때리기
  - 파일명:
  - 액션명: `Punch_Air`
  - 비고: 전용 액션 또는 상체 레이어 조합 검토
- [ ] 웅크린 상태에서 때리기
  - 파일명:
  - 액션명: `Punch_Crouch`
  - 비고: 전용 액션 또는 상체 레이어 조합 검토



## 피격·기절

- [ ] 가벼운 피격
  - 파일명:
  - 액션명: `Hit_Light`
- [ ] 강한 피격
  - 파일명:
  - 액션명: `Hit`
- [ ] 배 피격 — 부위별 판정이 있을 때
  - 파일명:
  - 액션명: `Hit_Body`
- [x] 머리 피격 — 부위별 판정이 있을 때
  - 파일명: `source/blender/characters/SmoothBear/Combat/SmoothBear_Hit.blend` → `CombatSource/Hit.fbx`
  - 액션명: `Hit`
  - 비고: 머리·상체가 뒤로 젖혀지고 골반이 밀리며 무릎이 굽는 1초 반응. 제작 `Tools/build_smooth_bear_hit.py`, 내보내기 `Tools/export_smooth_bear_hit_clip.py`. Hit_Walk/Run/Crouch/Prone/Crawl·Carry_TwoHands_Hit*는 Unity의 CarryTwoHandsHitClipBaker가 Hit 위에 다시 굽는다.
- [x] 기절 진입
  - 파일명: `source/blender/characters/SmoothBear/Combat/SmoothBear_Hit.blend` → `CombatSource/Stun_Start.fbx`
  - 액션명: `Stun_Start`
  - 비고: 새 Hit 충격(0.3초) → 바로 서려다 머리가 앞으로 떨어짐(0.6초) → 뒤로 넘어짐(1.25초) → Stun_Idle 첫 프레임과 같은 자세 유지(2.2초). 제작 `Tools/build_smooth_bear_stun_start.py`.
- [x] 기절 유지
  - 파일명: `FirstPlayerCapsule_Stun_Idle.fbx`
  - 액션명: `Stun_Idle`
  - 반복: O
- [x] 기절 회복·일어나기
  - 파일명: `FirstPlayerCapsule_Stun_End.fbx`
  - 액션명: `Stun_End`



## 물건 잡기·들기

> 이동 동작은 기존 하체 애니메이션 + 상체 들기 자세 조합부터 검토.

- [ ] 물건 집기
  - 파일명:
  - 액션명: `Pickup`
- [x] 물건 들고 대기
  - 파일명: `FirstPlayerCapsule_Carry_Idle.fbx`
  - 액션명: `Carry_Idle`
  - 반복: O
- [x] 물건 들고 걷기
  - 파일명: `FirstPlayerCapsule_Carry_Walk.fbx`
  - 액션명: `Carry_Walk`, `Carry_Walk_Back`, `Carry_Walk_Left`, `Carry_Walk_Right`
  - 반복: O
  - 비고: 기존 걷기 + `Carry_Idle` 오른팔. `Tools/make_first_carry_locomotion.py`
- [x] 물건 들고 뛰기
  - 파일명: `FirstPlayerCapsule_Carry_Run.fbx`
  - 액션명: `Carry_Run`, `Carry_Run_Back`, `Carry_Run_Left`, `Carry_Run_Right`
  - 반복: O
  - 비고: 기존 달리기 + `Carry_Idle` 오른팔
- [ ] 물건 들고 점프
  - 파일명:
  - 액션명 / 조합:
- [x] 물건 들고 웅크리기
  - 파일명: `FirstPlayerCapsule_Carry_Crouch.fbx`
  - 액션명: `Carry_Crouch`, `Carry_Crouch_Walk_Forward`, `Carry_Crouch_Walk_Back`, `Carry_Crouch_Walk_Left`, `Carry_Crouch_Walk_Right`
  - 반복: O
  - 비고: 기본 웅크리기 팔보다 오른팔을 위로. `Tools/make_first_carry_postures.py`
- [x] 물건 들고 엎드리기
  - 파일명: `FirstPlayerCapsule_Carry_Prone.fbx`
  - 액션명: `Carry_Prone`
  - 반복: O
  - 비고: 왼손은 바닥, 오른팔은 기본 포복보다 살짝 들고 손은 옆으로 세움
- [x] 물건 잡고 기어가기
  - 파일명: `FirstPlayerCapsule_Carry_Crawl_Forward.fbx`
  - 액션명: `Carry_Crawl_Forward`, `Carry_Crawl_Back`, `Carry_Crawl_Left`, `Carry_Crawl_Right`
  - 반복: O
  - 비고: 한 손 들기. 왼손·하체는 기존 포복
- [ ] 물건 내려놓기
  - 파일명:
  - 액션명: `PutDown`
- [x] 물건 던지기
  - 파일명: `FirstPlayerCapsule_Throw.fbx`
  - 액션명: `Throw`
  - 반복: X
  - 재생 구간: 1~41 / 30fps
  - 비고: 들기→상완 옆열기→팔 올리기→직선으로 던짐. 26프레임에서 놓음. `Tools/make_first_throw.py`



## 계단 — 기존 이동으로 테스트 후 필요 시 제작

- [ ] 계단 오르기
  - 파일명:
  - 액션명 / 재사용 방식:
- [ ] 계단 뛰어 오르기
  - 파일명:
  - 액션명 / 재사용 방식:
- [ ] 계단 내려가기
  - 파일명:
  - 액션명 / 재사용 방식:
- [ ] 계단 뛰어 내려가기
  - 파일명:
  - 액션명 / 재사용 방식:



## 게임 적용·최종 검수

- [ ] 이동을 코드 / 루트 모션 중 무엇으로 처리할지 결정
- [ ] 점프 수직 이동의 이중 적용 방지
- [ ] 점프 도약·공중·착지 상태 연결
- [ ] 걷기·달리기 반복 경계 확인
- [ ] 이동 속도와 보폭 맞추기
- [ ] 발 미끄러짐·지면 관통 확인
- [ ] 손·팔·몸통의 메시 겹침 확인
- [ ] 공격 타격 시점 연결
- [ ] 기절 중 행동 제한 및 회복 연결
- [ ] 물건을 잡고 놓는 시점 연결
- [ ] 애니메이션 전환 시 자세 튐 확인



## 작업 메모

- 