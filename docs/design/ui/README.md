# UI 시안 (UI)

2026-09-03 와이어프레임. 화면 구성·상태·카피를 맞추기 위한 1차 시안이며, 비주얼 확정본은 아닙니다.

시안 원본: [`images/260903_wireframe/`](images/260903_wireframe/)

기획 상세는 [`docs/planning/기획_내용_정리_0901.md`](../../planning/기획_내용_정리_0901.md), 톤앤매너는 [`docs/design/style-guide.md`](../style-guide.md)를 함께 봅니다.

## 화면 흐름

```text
Main ── 방 만들기 / 게임 찾기 ──► Lobby ── 게임 시작 ──► Hide ──► Playing ──► Final 30s
  │                                  │
  ├─ 캐릭터 커스텀                    ├─ 방 설정 / 친구 초대 (방장)
  ├─ 환경 설정                        ├─ 환경 설정 / 캐릭터 설정
  ├─ 친구 / 닉네임 / 서버              └─ 채팅 · 키 가이드
  └─ 게임 종료
```

파일명의 앞 두 자리는 화면 흐름 순서입니다.

| 번호 | 씬 | 역할 |
| --- | --- | --- |
| `01`~`04` | Main | 허브. 방 생성·참가, 커스텀, 설정, 소셜 |
| `05`~`08` | Lobby | 대기실. 참가자·채팅·방 설정, 카운트다운 |
| `09`~`11` | InGame | 숨기기 → 진행 → 마지막 30초 HUD |

## 파일 규칙

- 형식: `순서-화면-상태.png` (영문 소문자 `kebab-case`)
- `-01`, `-02` 같은 접미사는 호버·스크롤·포커스·확인 모달 등 **같은 화면의 상태 변형**입니다.
- `03-customize-section-header.png`처럼 이름에 `section-header`가 있는 파일은 Figma 구분용이며 구현 참고 화면이 아닙니다.
- 확정 시안이 생기면 아래 목록에 `✅`를 붙입니다. 현재는 전부 초안입니다.

모든 이미지 파일은 공백 없는 영문 `kebab-case`로 관리하며, 아래 미리보기는 원본 파일을 직접 참조합니다.

---

## Scene #1 Main

메인 허브는 야간 골목 3D 배경 위에 왼쪽 텍스트 메뉴, 중앙 캐릭터, 우측 프로필/소셜을 올리는 레이아웃입니다. 액센트 컬러는 오렌지입니다.

### #1-1 메인 (default)

왼쪽: **방 만들기 / 게임 찾기 / 캐릭터 / 환경 설정**. 하단 왼쪽 **게임 종료**. 하단 오른쪽 프로필(아바타·닉네임·프로필 아이콘). 우측 상단 지구본(서버).

![메인 기본 화면](images/260903_wireframe/01-main-default.png)

| 파일 | 상태 |
| --- | --- |
| `01-main-default.png` | 기본 |
| `01-main-menu-hover.png` | 메뉴 호버. 선택 항목이 오렌지로 강조 |

### 닉네임 / 프로필

프로필 영역에서 연다. 닉네임 2~12자, 중복 확인 통과 후에만 적용 활성화. **닉네임 검색 허용** 토글.

| 파일 | 상태 |
| --- | --- |
| `01-main-nickname-search-enabled.png` | 검색 허용 ON |
| `01-main-nickname-search-disabled.png` | 검색 허용 OFF |
| `01-main-nickname-available.png` | 중복 확인 성공. `사용 가능한 닉네임입니다` |
| `01-main-nickname-duplicate.png` | 중복. 적용 비활성 |
| `01-main-nickname-max-length.png` 외 `-01`, `-02` | 12자 입력·카운터 `12/12` 상태 |

### 친구

우측 패널. 탭: **친구 목록 / 친구 요청**. 닉네임 검색, 새로고침. 온라인 → 오프라인 순.

![친구 목록](images/260903_wireframe/01-main-friends-list.png)

| 파일 | 상태 |
| --- | --- |
| `01-main-friends-list.png` | 친구 목록 탭 |
| `01-main-friend-requests-empty.png` | 요청 탭 (빈 상태) |
| `01-main-friend-requests-received.png` | 신규 `N` 뱃지, 수락/거절, 검색 결과 없음 |

### 방 만들기

모달. **PRIVATE / PUBLIC**, 방 이름 `0/20`, 인원 스테퍼(시안 기본 6). 제목 미입력 시 생성 버튼 비활성.

![방 만들기](images/260903_wireframe/01-main-create-room.png)

| 파일 | 상태 |
| --- | --- |
| `01-main-create-room.png` | 기본 (PUBLIC, 생성 가능) |
| `01-main-create-room-01.png`, `01-main-create-room-02.png` | 입력·토글·버튼 활성/비활성 변형 |

### 서버 설정

우측 상단 지구본. 아시아 / 북미 / 오세아니아 / 유럽. 현재 리전 체크 표시.

| 파일 | 상태 |
| --- | --- |
| `01-main-server-settings.png` | 아시아 선택 |

### #1-2 방 찾기

탐정 책상 배경. 왼쪽 **방 코드 6칸 + 입장**, 오른쪽 방 목록(검색·새로고침). 방 아이템: 대기중(초록) / 게임중(주황), 제목(최대 20자), 맵, 인원, 방장.

![방 찾기](images/260903_wireframe/02-room-search-default.png)

| 파일 | 상태 |
| --- | --- |
| `02-room-search-default.png` | 기본 목록 |
| `02-room-search-item-hover.png` 외 `-01`, `-02` | 목록 행 호버 |
| `02-room-search-code-partial.png` | 코드 일부 입력 (`ABC1__`) |
| `02-room-search-code-complete.png` | 6자리 입력 완료, 입장 가능 |

### #1-3 캐릭터 커스텀

아지트(창고) 배경. 중앙 캐릭터 프리뷰. 왼쪽 카테고리: **몸 색상 / 후드 / 신발 / 표정**. 오른쪽 그리드. 하단 초기화·적용. Q/E 회전, 휠 줌.

변경이 없으면 초기화·적용 비활성. 변경 후 이전/초기화 시 저장 여부 확인 모달.

![캐릭터 커스텀](images/260903_wireframe/03-customize-unchanged.png)

| 파일 | 상태 |
| --- | --- |
| `03-customize-unchanged.png` | 변경 없음. 초기화·적용 비활성 |
| `03-customize-changed.png` | 변경 있음. 적용 버튼 오렌지 |
| `03-customize-discard-confirm.png` | 미적용 이탈 확인. `적용하지 않고 나가시겠습니까?` |
| `03-customize-reset-confirm.png` | 초기화 확인 모달 |

### #1-4 환경 설정

메인·로비 공통 패널. 왼쪽 탭 6개: **일반 / 그래픽 / 인터페이스 / 사운드 / 컨트롤 / 알림**. 상단 `← 이전`, `전체설정 초기화`. 하단 탭 단위 **초기화 / 적용하기**.

![환경 설정 — 그래픽](images/260903_wireframe/04-settings-graphics.png)

| 탭 | 시안에 보이는 항목 | 파일 |
| --- | --- | --- |
| 일반 | 언어, 피드백 보내기 | `04-settings-general.png` 외 `-01`~`-03` |
| 그래픽 | 디스플레이 모드, 해상도, FPS 제한, 안티앨리어싱, HBAO, 텍스처/그림자 품질 등 | `04-settings-graphics.png` 외 `-01`~`-03` |
| 인터페이스 | UI 크기, 게임 내 UI, FPS·핑 표시, 플레이어 이름/내 닉네임 표시 | `04-settings-graphics-01.png` (인터페이스 탭 캡처) |
| 사운드 | 마스터/BGM/환경음/효과음, 마이크 | `04-settings-sound.png` 외 `-01`~`-04` |
| 컨트롤 | 마이크 송출 키, WASD 등 키 바인딩 | `04-settings-controls.png` 외 `-01`~`-03` |
| 알림 | 게임중 / 친구 요청 / 게임 초대 on-off | `04-settings-notifications.png` |

로비에서 연 설정(`08-lobby-settings.png`)은 같은 패널에 사이드바 하단 **게임 나가기**가 추가됩니다.

---

## Scene #2 Lobby

창고(아지트) 3D 배경 위에 HUD를 올립니다. 일반 유저와 방장 화면이 갈립니다.

공통 요소: 우측 카테고리(맵 썸네일), 참여 플레이어 목록, 하단 채팅, 키 가이드(`L` 토글), 하단 단축키 `1` 캐릭터 설정 / `2` 플레이어 / `ESC` 환경설정.

### #2-1 로비 (일반)

나 자신은 닉네임 오렌지. 방장은 왕관. 키 가이드 on/off.

![로비 기본 화면](images/260903_wireframe/05-lobby-player-key-guide-on.png)

| 파일 | 상태 |
| --- | --- |
| `05-lobby-player-key-guide-on.png` | 키 가이드 표시 |
| `05-lobby-player-key-guide-off.png` | 키 가이드 숨김 |
| `05-lobby-player-invite-friends.png` | 초대 진입. 인원 여유(예: 4/6)일 때 |
| `05-lobby-player-invite-friends-list.png` | 초대용 친구 목록 |
| `05-lobby-player-chat-active.png` 외 `-01`, `-02` | 채팅 입력 활성·전송 |

### #2-2 로비 (방장)

참가자 행에 **강퇴**. 방 설정·맵/카테고리 변경 가능. 게임 시작은 방장만.

| 파일 | 상태 |
| --- | --- |
| `06-lobby-host-01.png`, `06-lobby-host-02.png` | 방장 대기 화면 |
| `06-lobby-host-invite-friends.png` | 방장 친구 초대 |
| `06-lobby-host-room-settings.png` | 맵 선택, 카테고리(랜덤), 방 제목 `n/20`, 방 코드 복사 |
| `06-lobby-host-room-settings-01.png`, `06-lobby-host-room-settings-02.png` | 인원, 파괴 가능 횟수, 숨기는 시간, 찾는 시간 등 스크롤 |

방 설정 시안에 보이는 값 예: 인원 6명, 파괴 5회, 숨기기 30초, 찾기 5분.

### #2-3 게임 시작

상단 `10초 뒤 게임이 시작됩니다`. 카운트다운 중 UI는 유지.

| 파일 | 상태 |
| --- | --- |
| `07-lobby-game-start-player.png` | 일반 유저 카운트다운 |
| `07-lobby-game-start-host.png` | 방장 카운트다운 (강퇴 버튼 유지) |

### #2-4 로비 모달

| 파일 | 상태 |
| --- | --- |
| `08-lobby-settings.png` | 로비 설정. **게임 나가기** 포함 |
| `08-lobby-customize.png` | 로비에서 연 커스텀. 여기서 바꾼 캐릭터는 게임 종료 후에도 유지 |

---

## Scene #3 InGame

HUD는 화면 가장자리만 쓰고, 중앙은 월드를 비웁니다. 키 가이드는 우측.

### 로딩 / 물건 배정

검정 배경. 배정된 물건 안내 후 숨기기 페이즈로 진입합니다.

![물건 배정](images/260903_wireframe/09-hide-loading-item-assignment.png)

카피 예: `당신이 훔친 물건은 🍎 사과입니다.` / `다른 도둑들에게 빼앗기지 않도록 비밀 장소에 잘 챙겨두세요.`

### #3-1 Hide (숨기기)

**숨기는 플레이어:** 상단 타이머 + `제한 시간 안에 물건을 숨겨주세요!`. `F` 물건 배치(바닥 초록 존), `Y` 숨기기 완료. 10초 남으면 타이머 오렌지, `시간 초과 시 마지막 위치에 물건이 배치됩니다`.

![숨기기](images/260903_wireframe/09-hide-active-player.png)

**대기 플레이어:** 좌상단 완료 체크 목록, 상단 `N / 6` + `○○님이 물건을 숨기는 중`. 이동·채팅 가능. 다음 차례면 별도 안내.

| 파일 | 상태 |
| --- | --- |
| `09-hide-active-player.png`, `09-hide-active-player-01.png` | 숨기는 중 (30초) |
| `09-hide-active-player-pickup.png` | 물건 집기 |
| `09-hide-active-player-placement.png` | 배치 가능 존 + `F 물건 배치` |
| `09-hide-active-player-ten-seconds.png` | 타이머 경고 |
| `09-hide-waiting-player.png` | 대기 (다른 사람 숨기는 중) |
| `09-hide-waiting-player-next-turn.png` | 다음이 내 차례 |

### #3-2 Playing (진행)

좌상단 물건 슬롯 6개(확보=아이콘, 미확보=`?`). 상단 타이머(예: 06:30). 하단 스테미나 `5/5` · 체력 `3/3`. 채팅·키 가이드.

![진행](images/260903_wireframe/10-playing-default.png)

| 파일 | 상태 |
| --- | --- |
| `10-playing-default.png` | 기본 HUD |
| `10-playing-chat-active.png` | 채팅 열린 상태 |
| `10-playing-transition.png` | Hide → Playing 전환 |

### #3-3 Final 30s

진행 HUD와 동일 구성. 타이머·안내 문구만 경고 색(오렌지). `00:30` + `서둘러 자신의 물건을 확보하세요!`

![마지막 30초](images/260903_wireframe/11-playing-final-thirty-seconds.png)

| 파일 | 상태 |
| --- | --- |
| `11-playing-final-thirty-seconds.png` | 마지막 30초 HUD |

엔딩·하이라이트 화면은 이번 시안에 없습니다. 기획서 13·14절을 따릅니다.

---

## 시안에서 읽히는 UI 규칙

- **액센트:** 호버, 선택, Primary 버튼, 타이머 경고는 오렌지.
- **패널:** 반투명 다크 + 라운드. 설정 모달은 오렌지 외곽 글로우.
- **확인 모달:** 이탈·초기화 시 `예`(오렌지) / `아니오`. 미저장 변경은 버린다는 서브카피.
- **비활성:** 변경 없음·필수값 미입력·중복 닉네임이면 적용/생성 비활성.
- **HUD:** 가장자리만. 키 가이드는 `L`로 끄고 켤 수 있음.
- **채팅:** Enter로 활성화/전송. 로비·인게임 모두 좌하단.

로비 키 가이드 (시안 기준): 공격 좌클릭 · 앉기 `C` · 엎드리기 `Z` · 시점 `V` · 달리기 `Shift` · 점프 `Space`.

---

## 섹션 헤더

`03-customize-section-header.png`는 Figma 구분용 이미지이며 구현 참고 화면이 아닙니다.
