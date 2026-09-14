# 플레이 로그 이벤트 명세 v2

Unity 클라이언트와 백엔드가 주고받는 행동 이벤트의 규약입니다.

**여기 있는 것** — 어떤 이벤트를 언제 누가 보내는지, 왜 그 이벤트가 필요한지.
**여기 없는 것** — 엔드포인트 스펙. 그건 [`openapi.json`](openapi.json)에 있고 코드에서 자동 생성됩니다.

저장 구조는 `d205_analytics.game_event` 한 테이블입니다. 게임 DB(`d205`)와 같은 MySQL
인스턴스를 쓰지만 스키마도 커넥션 풀도 분리되어 있어서, 이벤트 수집이 막혀도 게임 API는
영향을 받지 않습니다. 수집 코드는 같은 Spring 앱의 `domain/analytics` 패키지에 있습니다.
별도 서비스로 나누지 않은 이유와 나눌 시점은 지라 에픽 S15P21D205-780 에 있습니다.

**이 문서는 v2 기준입니다.** 서버가 받는 이벤트 이름은 19개지만 클라이언트가 실제로 보내는
것은 5종(`match_start`, `phase_change`, `position_sample`, `player_result`, `match_end`)입니다.
v1 이 개별 행동 이벤트로 찍으려 했던 사실은 1초 위치 샘플의 누적값에서 SQL 로 유도합니다.
5절이 그 다섯 종과 유도 방법을, 5.3 절이 아직 발행하지 않는 14개를 부록으로 담습니다.

필드 구조와 파일·재전송은 [경기 분석 수집 v2](match-analytics.md), 실제 대시보드 쿼리는
[대시보드 쿼리](analytics-dashboards.md)에 있습니다.

---

## 1. 이 로그로 답하려는 질문

**이벤트는 이 목록에서 역산해서 만들었습니다.** 여기에 답하지 않는 이벤트는 넣지 않습니다.
"일단 다 찍어두고 나중에 보자"는 거의 항상 쓰이지 않습니다.

| # | 질문 | 왜 알아야 하나 |
| --- | --- | --- |
| 1 | 숨는 시간 기본값(한 사람당 30초, 단계 전체는 인원수 × 30초)이 맞나 | 짧으면 못 숨긴 채 시작하고, 길면 지루하다 |
| 2 | 맵의 어느 구역이 안 쓰이나 | 안 쓰이는 구역은 만든 값을 못 한다 |
| 3 | 너무 좋거나 나쁜 은신처는 어디인가 | 아무도 못 찾는 자리가 있으면 게임이 성립하지 않는다 |
| 4 | 찾는 시간 기본 5분이 맞나 | 6인 기준 전체 플레이의 약 60%다. 여기가 비면 게임이 비는 것이다 |
| 5 | 기절 펀치 3회가 맞나 | 전투가 너무 빨리 끝나거나 너무 안 끝난다 |
| 6 | 조작이 학습되나 | 키가 12개다. 파티게임치고 많다 |
| 7 | 어느 단계에서 이탈하나 | 이탈 지점이 곧 가장 재미없는 구간이다 |

---

## 2. 공통 봉투

모든 이벤트가 아래 필드를 갖습니다. 각 이벤트가 따로 갖는 것은 `params`에만 넣습니다.

| 필드 | 타입 | 설명 |
| --- | --- | --- |
| `occurred_at` | epoch ms | **클라이언트 시각.** 조작될 수 있다. 3절 시각 규칙 참고 |
| `client_session_id` | uuid | **보낸 쪽**의 앱 실행 단위. 재시작하면 바뀐다. 호스트가 대신 보낸 이벤트면 호스트의 세션이다 |
| `client_seq` | int | 세션 안에서 0부터 단조 증가. **재전송 중복 제거의 키**다 |
| `room_code` | char(6) | Photon 방 코드. 방에 없으면 null. 매치 시작 전 로비 이탈을 묶는 유일한 열쇠다 |
| `match_id` | uuid | 경기 단위. 호스트가 만들어 전원에게 공유한다. 인게임 밖이면 null |
| `match_time_ms` | int | `NetworkRunner.SimulationTime`(MatchStarter 의 `ServerTime` 별칭) 기준 경기 시각. **호스트 이벤트만** 채운다 |
| `user_public_id` | uuid | **행동의 주체.** `users.public_id`. 계정 발급 응답의 `userId`와 같은 값 |
| `event_name` | string | 5절 목록 중 하나 |
| `phase` | enum | `Waiting` / `Hiding` / `Searching` / `Highlight` / `Result`. 코드의 `MatchPhase` 이름 그대로. 인게임 밖이면 null |
| `map_id` | string | |
| `pos_x` `pos_y` `pos_z` | float | 이벤트가 일어난 좌표 |
| `from_host` | bool | 호스트(StateAuthority)가 보냈는지 |
| `schema_ver` | short | 현재 `2`. `1` 은 개별 행동 이벤트를 찍으려던 계획이고 실제 수집은 없었다 |
| `params` | json | 이벤트별 추가 값 |

**`user_public_id`는 `users_seq`가 아닙니다.** 클라이언트는 `public_id`만 알고,
`users_seq`는 가입자 수가 드러나서 외부로 내보내지 않는 값입니다. FK 는 걸지 않습니다.

**`client_session_id`는 `user_presence.session_id`와 다른 것입니다.** 그쪽은 Photon 방
코드입니다. 이름을 다르게 둔 이유가 그것이고, 방 코드는 여기서 `room_code`입니다.

**`phase`에 FINAL 이 없습니다.** 코드에서 마지막 30초는 단계가 아니라 `Searching` 안의 구간
(`FinalWarningStartedEvent`)입니다. 그 경계는 `final_warning` 이벤트가 찍습니다.
`Highlight`와 `Result`를 빼지 않은 것은 질문 7(이탈 지점)이 그 구간에서도 일어나기 때문입니다.

**좌표는 `params`가 아니라 최상위 컬럼입니다.** JSON 안에 넣으면 히트맵 집계마다
`JSON_EXTRACT`가 붙어 인덱스를 못 씁니다. 이 로그의 핵심이 공간 분석이므로 여기서 타협하지 않습니다.

`received_at`은 클라이언트가 보내지 않습니다. **서버가 받은 시각을 따로 찍습니다.**

---

## 3. 시각 규칙

시각이 세 개 있고 셋은 쓰는 곳이 다릅니다.

| 시각 | 누가 찍나 | 어디에 쓰나 |
| --- | --- | --- |
| `occurred_at` | 클라이언트 벽시계 | 날짜별 집계, 파티션 키. **믿지 않는다** |
| `received_at` | 서버 | 수집 상태 확인. `occurred_at`과 크게 벌어지면 스풀 재전송이거나 시계가 틀린 것 |
| `match_time_ms` | 호스트의 `NetworkRunner.SimulationTime`(MatchStarter 의 `ServerTime` 별칭) | **경기 안 시간 계산 전부.** "숨기기 시작 후 몇 초에 숨겼나"는 이 값으로 구한다 |

`occurred_at`은 UTC epoch ms 로 보내고 서버가 UTC `DATETIME(3)`으로 넣습니다. 기존 게임
테이블은 CHAR(14) UTC 문자열이고 JDBC URL 은 `serverTimezone=Asia/Seoul`이라, 규칙을 적어두지
않으면 KST 로 들어가는 행과 UTC 로 들어가는 행이 섞입니다. **`game_event`의 DATETIME 은 전부 UTC 입니다.**

서버는 `occurred_at`이 `지금 - 7일` 보다 이르거나 `지금 + 5분` 보다 늦은 이벤트가 하나라도
있으면 **그 배치를 400 으로 거부합니다.** 파티션 키가 클라이언트 값이라, 1970년 시각 하나가
들어오면 파티션이 없어 insert 가 실패하고 그 배치 전체가 죽습니다. 7일은 스풀이 묵을 수 있는
최대 기간을 넉넉히 잡은 것입니다.

경기 안 분석에 `occurred_at`을 쓰지 않는 이유가 하나 더 있습니다. `MatchSessionCoordinator`가
모든 판정을 `ServerTime` 기준으로 하므로, 같은 축으로 기록해야 "규칙상 30초"와 "로그상 30초"가
같은 30초가 됩니다.

---

## 4. 누가 보내는가 — 가장 중요한 규칙

Photon Fusion 이라 **이 규칙을 어기면 데이터가 통째로 못 쓰게 됩니다.**
6명이 각자 "라운드 종료"를 보내면 이벤트가 6개 생기고, 중간 이탈자가 있으면
6으로 나누는 보정조차 맞지 않습니다.

### StateAuthority(호스트)만 보낸다 — 경기 사실

단계 전환, 물건 배치·집기·파괴, 명중, 기절, 최종 판정, 위치 샘플.

호스트는 전원의 상태를 알고 있으므로 **다른 플레이어를 대신해서 보냅니다.**
이때 `user_public_id`는 그 행동의 주체이고, `from_host`는 `true`, `client_session_id`는
호스트의 세션입니다.

**호스트가 대신 보내려면 호스트가 전원의 `userId`를 알아야 합니다.** 그 값은
`MatchParticipant.UserId`에서 읽습니다(S15P21D205-864). 각 클라이언트가 연결 토큰에
자기 계정의 `userId`를 실어 보내고, 호스트가 `PlayerAvatar.UserId`로 복제하며,
`PlayerRoster`가 `RoomParticipant.UserId`로 옮기고 경기 시작 때 `MatchParticipant`로 넘어갑니다.
호스트가 바뀌어도 `MatchSessionState.ParticipantUserIds`에 남아 있어 새 호스트도 같은 값을 봅니다.

`PlayerRegistry.IdOf`가 돌려주는 `"P3"` 같은 값은 방 안에서만 쓰는 좌석 키라 `user_public_id`에
넣으면 안 됩니다.

**로그인하지 않은 플레이어는 `MatchParticipant.UserId`가 `null`입니다.** 그대로 보내면 서버가
계정 없음으로 저장하고, 그 경기 안에서는 `params.seat`으로만 구분됩니다. **빈 문자열로 바꾸면
안 됩니다** — 서버의 `userPublicId`는 UUID 형식 검사를 받고 빈 문자열은 그 검사를 통과하지
못해 **배치 전체가 400** 이 됩니다(7절). 발행 시점에 형식을 확인해 아닌 값을 null 로 바꾸는
이유가 그것입니다.

### 각 클라이언트가 보낸다 — 그 클라이언트만 아는 것

UI 클릭, 설정 변경, FPS·핑, 이탈, 조작 학습 지표, **펀치 휘두름**.

호스트가 알 수 없는 정보입니다. `from_host`는 `false`입니다.

**v2 에는 이 경로가 없습니다.** 발행하는 것은 호스트뿐이고, 여기 적힌 일곱 이벤트는 아무도
보내지 않습니다(5.3). 이 절은 그것들을 되살릴 때의 책임 규칙으로 남아 있습니다 — 호스트가
대신 보내면 안 되는 것들의 목록입니다.

펀치 휘두름이 여기 있는 이유: [PlayerCombatant](../../Assets/_Game/Client/Combat/PlayerCombatant.cs)는
공격 반경 `OverlapSphere`에 사람이 잡혔을 때만 호스트에 `TryRequestHit`을 보냅니다. **빗나간 주먹은 호스트에
도달하지 않습니다.** 빗나감을 호스트로 올리는 RPC 를 새로 만들지 않습니다.
분석 때문에 네트워크 트래픽을 늘리는 것은 순서가 뒤바뀐 것입니다.

v1 은 그래서 명중률을 클라이언트의 `punch_swung`과 호스트의 `punch_hit`을 조인해 구할
계획이었습니다. **v2 는 둘 다 없이 구합니다** — 휘두름은 `NetworkPlayerMotor`의
`[Networked]` 카운터(`attack_sequence`)가 호스트에도 복제되어 있고, 명중은 서버가 인정한
누적 피격 수입니다(5.2). 빗나간 주먹이 호스트에 도달하지 않는다는 사실은 그대로지만,
휘두른 **횟수**는 도달합니다.

### `from_host`를 왜 저장하나

규칙이 나중에 어긋나도 중복 제거가 가능하도록 남깁니다. 경기 사실 이벤트에
`from_host = false`가 섞여 있으면 그건 버그입니다.

### 호스트가 바뀌면 — 호스트 마이그레이션

**호스트가 나가도 경기는 끊기지 않습니다.** `MatchMigrationCheckpoint`로 새 호스트가
이어받습니다. 하지만 **로그는 이어지지 않습니다.**

v2 는 표본을 뜨는 버퍼가 자기 `match_id`를 만듭니다. 그래서 이관이 일어나면:

- 옛 호스트의 구간이 `end_reason = "Interrupted"`, `partial = true` 로 닫힙니다. 그 시점까지
  확보한 것만 전송됩니다.
- 새 호스트가 **다른 `match_id`** 로 새 버퍼를 시작하고, 도중부터 기록했으므로 그쪽
  `match_start`도 `partial = true` 입니다.
- 두 구간을 한 경기로 자동으로 합치지 않습니다. 사람이 방 코드와 시각으로 잇습니다.

`host_migrated`를 찍어 한 `match_id`를 이어 쓰는 것이 v1 계획이었고, v2 는 그 이벤트를 보내지
않습니다. 이관된 경기를 찾는 방법은 6절에 있습니다.

두 구간 모두 `upload_complete = 0`이라 경기 단위 집계에서 통째로 빠집니다. 뷰가 그 열을
`match_start` 1건, `match_end` 1건, 행 수 = `expected_events`, 그리고 **`partial = false`**
넷의 곱으로 정의하기 때문입니다 — 건수가 다 맞아도 `partial` 하나로 걸립니다. 그것이
의도입니다: **반쪽 경기를 섞으면 "찾는 시간이 짧다" 같은 잘못된 결론이 나옵니다.** 대신 그런
경기가 몇 판이었는지는 1번 화면에서 셉니다.

---

## 5. 이벤트 목록 (19개)

서버 화이트리스트(`GameEventName`)에 19개가 있습니다. **그중 v2 가 보내는 것은 5개입니다.**
나머지 14개는 이름만 열어 둔 상태이고 5.3 절에 있습니다.

화이트리스트를 5개로 줄이지 않는 이유는, 이름을 지우면 되살릴 때 서버 배포가 필요해지고
그때 `params` 명세를 처음부터 다시 쓰게 되기 때문입니다. 받을 준비만 되어 있는 것은 비용이
들지 않습니다.

### 5.1 v2 가 발행하는 5종

전부 호스트(StateAuthority)가 보냅니다. **클라이언트 발행은 v2 에 없습니다.** 발행 지점은
[`MatchAnalyticsRecorder`](../../Assets/_Game/Bootstrap/MatchAnalyticsRecorder.cs) 하나이고,
`Hiding`·`Searching` 단계에서만 표본을 뜹니다.

| 이벤트 | `params` | 비고 |
| --- | --- | --- |
| `match_start` | `player_count, hide_sec, seek_sec, sprint_multiplier, stun_hits, destroy_limit, build_ver, partial` | **방 설정을 전부 박는다.** 이게 없으면 "30초일 때 vs 60초일 때"를 비교할 수 없다. `hide_sec`은 **한 사람당** 숨는 시간(기본 30)이고 Hiding 단계 길이는 `hide_sec × player_count`다. 맵과 방은 `params`가 아니라 공통 봉투의 `map_id`·`room_code`다. `partial`은 이 경기가 도중부터 기록됐다는 표시 |
| `phase_change` | `from, to` | 모든 단계 계산의 기준점. 이름은 `MatchPhase` 그대로이고 첫 건은 `from = "Waiting"` |
| `position_sample` | `seat, rotation_y, posture, grounded, attack_sequence, total_hits_received, total_stuns, item_id, item_known, item_destroyed, holder_seat, item_x, item_y, item_z, item_in_motion` | **1Hz × 인원수.** 전체 행의 대부분이 이것이고, v2 분석의 거의 전부가 여기서 나온다(5.2) |
| `player_result` | `seat, result, total_hits_received, total_stuns` | 좌표 = 종료 시점 위치. 누적값을 다시 담는 이유는 마지막 1초 사이의 변화를 놓치지 않기 위한 것 |
| `match_end` | `end_reason, duration_ms, partial, expected_events, dropped_samples` | 이게 없는 경기는 분석 제외. `end_reason`은 `MatchEndReason` 이름이고 중단이면 `Interrupted` |

**`params`의 키는 서버가 검증하지 않습니다.** `JsonNode`로 그대로 저장하므로 이 표와 코드가
어긋나도 400 이 나지 않고 **뷰가 조용히 NULL 을 냅니다.** 필드를 바꿀 때는 `match_analysis_*`
뷰를 함께 고쳐야 합니다.

**각 클라이언트가 자기 위치를 보내면 안 됩니다.** 호스트가 어차피 다 알고 있어서 중복이고,
클라이언트마다 시각이 달라 같은 순간의 스냅샷이 만들어지지 않습니다.

### 5.2 샘플에서 유도하는 사실

v1 이 개별 이벤트로 찍으려 했던 것 대부분이 1초 샘플의 **누적값 차분**으로 나옵니다. 유도는
쿼리마다 새로 쓰지 않고 뷰에 한 번 적어 둡니다. JSON 을 꺼내는 일을 여덟 곳에 복사해 두면
필드 이름이 바뀔 때 여덟 곳이 어긋납니다.

| 알고 싶은 것 | 유도 방법 | 어디에 |
| --- | --- | --- |
| 휘두른 횟수 | `attack_sequence`의 경기 내 최댓값 − 최솟값. 플레이어 오브젝트가 경기마다 새로 생기지 않아 0 부터 시작하지 않는다 | `match_analysis_combat.swings` |
| 명중 횟수 | `total_hits_received` 차분 | `match_analysis_combat.hits_received` |
| 기절 횟수 | `total_stuns` 차분 | `match_analysis_combat.stuns` |
| 숨긴 위치 | `item_known = 1 AND item_in_motion = 0` 인 샘플의 `item_x`·`item_y`·`item_z` | `match_analysis_positions` |
| 집기·탈취 | `item_holder_seat`이 바뀐 시점. `item_holder_seat <> player_seat`이면 발견·탈취 | `match_analysis_positions` |
| 파괴 | `item_destroyed`가 거짓→참으로 바뀐 시점 | `match_analysis_positions` |
| 경기 중 이탈 | 그 좌석의 샘플이 종료보다 일찍 끊긴 것 | 대시보드 8번 |
| 최종 경고 진입 | `phase_change(→Searching)` + `seek_sec` − 30초. 결정적인 값이라 기록하지 않는다 | (계산) |

**유도의 대가는 셋입니다.** 첫째, 시각이 ±1초이고 1초 사이에 일어나 끝난 일은 아예 보이지
않습니다. 둘째, **가해자를 모릅니다** — `total_hits_received`는 맞은 쪽의 누적이라 "누가
때렸나"가 없고 좌표 근접도로 추정할 수밖에 없습니다. 셋째, 호스트가 표본을 뜨는 두 단계
밖은 비어 있습니다.

누적값이므로 **매초 값을 SUM 하면 중복 집계됩니다.** 최종값은 `player_result`를 쓰거나 같은
경기·좌석의 MAX 를 봅니다.

### 5.3 부록 — 아직 발행하지 않는 14개

서버는 받지만 아무도 보내지 않습니다. **되살릴 때의 `params` 명세로 남겨 둔 것입니다.**
`analytics-dashboards.md`에서 이것들을 읽던 쿼리 다섯 개는 영구히 비어 있어서 지웠습니다.

호스트가 보낼 것 (7):

| 이벤트 | `params` | 5.2 로 대체되는가 |
| --- | --- | --- |
| `final_warning` | `ends_in_ms` | 계산으로 대체. Searching 마지막 30초 진입 |
| `host_migrated` | `previous_host_user_public_id` | **부분.** `match_start.partial`과 `upload_complete = 0`으로만 안다 |
| `item_hidden` | `item_id, owner_id` | 위치는 대체, **시각은 ±1초.** `TryRecordItemPlacement`가 신호 |
| `item_picked_up` | `item_id, owner_id, holder_id, hidden_ago_ms` | 대체 가능, 시각 ±1초. `TryHoldObject`가 신호 |
| `item_destroyed` | `item_id, owner_id, destroyer_id` | 시점은 대체, **파괴자를 모름.** `PlayerItemDestroyed`가 신호 |
| `punch_hit` | `attacker_id, target_id, hits_so_far` | 횟수는 대체, **가해자를 모름.** `RegisterHit`이 신호 |
| `player_stunned` | `attacker_id, combat_ms, punches_landed` | 횟수는 대체, `combat_ms`는 ±1초. `PlayerStunned`가 신호 |

발견·탈취·되찾음을 이벤트 셋으로 나누지 않고 `item_picked_up` 하나로 둔 이유: 코드에 그 셋을
구분하는 신호가 없습니다. 호스트 쪽 신호는 `TryHoldObject` 하나이고 구분은 `owner_id`와
`holder_id`의 관계에서 나옵니다.

각 클라이언트가 보낼 것 (7). **이쪽은 유도할 방법이 없습니다** — 호스트가 모르는 정보이고,
v2 에는 클라이언트 발행 경로 자체가 없습니다:

| 이벤트 | `params` | 없어서 못 하는 것 |
| --- | --- | --- |
| `client_session_start` | `build_ver, platform, resolution` | 앱 실행 수 대비 경기 수 |
| `scene_enter` | `scene` (`AppFlowState` 이름) | 화면 단위 퍼널 |
| `client_quit` | `scene, reason` (`NORMAL` / `FORCED`) | **질문 7 의 완전판.** 경기 전 이탈 |
| `first_interact` | `elapsed_since_phase_ms` | **질문 6.** F키 첫 상호작용까지 걸린 시간 |
| `punch_swung` | `had_target` | 빗나감. 호스트에 도달하지 않는다(4절) |
| `settings_changed` | `category, key` | **질문 6.** 값은 넣지 않는다 |
| `perf_sample` | `fps_avg, fps_p1, ping_ms` | fps·핑 전부 |

`settings_changed`에서 값을 빼는 이유는 두 가지입니다. 노이즈가 크고, 설정값 중에 닉네임
공개 범위처럼 개인 정보에 가까운 것이 섞여 있습니다.
**"감도를 자주 바꾼다"는 사실만으로 기본값이 나쁘다는 신호는 충분합니다.**

### 5.4 질문 ↔ 화면

쿼리는 [`analytics-dashboards.md`](analytics-dashboards.md)가 원본이고, 그 문서를
`deploy/metabase/provision_dashboards.py`가 읽어 Metabase 화면을 만듭니다.

| 질문 | 화면 | 답이 나오나 |
| --- | --- | --- |
| 1 숨는 시간 | 2 | ✅ 숨긴 시점을 `item_in_motion = 0`으로 잡는다 |
| 2 죽은 구역 | 3·4 | ✅ 좌표 히트맵. 맵 그림 위에 겹치는 것은 Metabase 로 안 되고 따로 만든다 |
| 3 은신처 품질 | 3 | ✅ |
| 4 찾는 시간 | 5 | ✅ `item_holder_seat` 변화로 |
| 5 기절 3회 | 6 | ✅ 명중률까지. 분모(`swings`)가 `attack_sequence`다 |
| 6 조작 학습 | — | ❌ **화면이 없다.** `first_interact`·`settings_changed`가 필요하다 |
| 7 이탈 지점 | 8 | ⚠️ **경기 시작 후만.** 로비·홈 이탈은 `client_quit`이 필요하다 |

---

## 6. 집계할 때 반드시 넣을 필터

경기 단위 집계는 전부 `upload_complete = 1`을 깔고 갑니다. 뷰가 "시작·종료가 모두 있고,
예정 건수와 실제 행 수가 같고, 중단 표시가 없음"을 그 한 열로 정리해 둡니다.

```sql
-- 믿을 수 있는 경기만
SELECT match_id FROM d205_analytics.match_analysis_summary
WHERE upload_complete = 1
```

마이그레이션이 실패해 끊긴 경기는 이벤트가 중간에 멎어 있습니다. 이걸 섞으면
"찾는 시간이 짧다" 같은 잘못된 결론이 나옵니다.

**호스트가 바뀐 경기는 `host_migrated`로 찾을 수 없습니다.** v2 는 그 이벤트를 보내지 않고,
호스트 이관 뒤의 구간은 **새 `match_id`의 부분 기록**으로 들어옵니다. 두 구간을 자동으로 한
경기로 합치지 않습니다. 그래서 경계를 의심할 대상은 이렇게 찾습니다.

```sql
-- 도중부터 기록된 경기. 앞 구간이 다른 match_id 에 있거나 아예 없다
SELECT match_id FROM game_event
WHERE event_name = 'match_start'
  AND params->>'$.partial' = 'true'
```

발행 규칙이 지켜지고 있는지 주기적으로 확인합니다.

```sql
-- 경기당 1건이어야 하는 이벤트가 여러 건이면 발행 책임 분리가 깨진 것
SELECT match_id, event_name, COUNT(*)
FROM game_event
WHERE event_name IN ('match_start', 'match_end')
GROUP BY match_id, event_name
HAVING COUNT(*) > 1
```

재전송으로 생기는 중복은 여기서 걱정하지 않습니다. `UNIQUE (client_session_id, client_seq,
occurred_at)`이 DB 에서 막습니다(7절).

---

## 7. 전송 규약

클라이언트는 이벤트를 **모아서** 보냅니다. `POST /api/v1/events`에 배열로 넣습니다.

**v2 는 경기가 끝난 뒤에 보냅니다.** 표본을 뜨는 동안에는 메모리에만 쌓고 HTTP 도 파일 I/O 도
하지 않습니다.

| 시점 | 동작 |
| --- | --- |
| 표본 1초마다 | 메모리 적재만 |
| 경기 종료·중단 | `persistentDataPath/match-analytics/<match_id>.jsonl` 로 쓰고 전송 시작 |
| 전송 중 | 50건씩 배치, 배치 사이 200ms |
| 앱 시작 | 남은 파일 재전송 |

**이벤트마다 HTTP를 치지 않습니다.** 프레임이 튑니다. 직렬화는 발행 시점에 하고 버퍼에는
객체가 아니라 문자열이 쌓입니다 — 50건을 한꺼번에 직렬화하면 그 프레임이 튑니다.

**경기 종료 후 전송의 대가가 질문 7 입니다.** v1 은 10초마다 flush 해서 이탈자의 마지막
10초치만 잃는 설계였습니다. v2 는 경기가 끝나야 보내므로 **경기 전에 떠난 사람의 기록이 아예
생기지 않습니다.** 5.4 의 질문 7 이 "경기 시작 후만"인 근본 이유가 이것이고, 8번 화면은
호스트가 전원을 관측한다는 사실을 빌려 경기 중 이탈만 겨우 봅니다.

전송에 실패하면 파일을 남겨 다음 경기 종료나 앱 시작 때 **같은 이벤트 ID로** 재전송합니다.
시연 중 네트워크가 한 번 튀어서 그날 데이터가 통째로 날아가는 것을 막습니다.
**플레이테스트 기회는 유한합니다.** 보관 상한은 경기 10개이고, 400·413 을 받은 파일은
`.rejected`로 격리해 다음 경기의 전송을 막지 않습니다.

### 재전송은 중복을 만든다

202 를 받지 못한 배치가 실제로는 저장됐을 수 있습니다. 서버가 넣고 나서 응답이 끊기면
클라이언트는 실패로 보고 스풀에서 다시 보냅니다. 그래서

- 클라이언트는 이벤트마다 `client_seq`를 붙입니다. 세션 안에서 0부터 하나씩 늘고, 재전송해도
  **같은 값**입니다. 스풀에 쓸 때 이미 붙어 있어야 합니다.
- 서버는 `UNIQUE (client_session_id, client_seq, occurred_at)`에 `INSERT IGNORE`로 넣습니다.
  두 번 온 이벤트는 조용히 버려집니다.
- 유니크 키에 `occurred_at`이 들어간 것은 파티션 규칙 때문입니다. MySQL 은 파티션 키가 모든
  유니크 키에 포함돼야 하고, `received_at`은 재전송마다 달라져서 쓸 수 없습니다.

### 계층 위치

**서버 쪽은 별도 서비스입니다**(2026-09-14, S15P21D205-980). `backend/analytics` 모듈이 `/api/v1/events` 를
받아 자기 DB(`d205-mysql-analytics`)에 쓰고, 계정 서비스(`backend/app`)는 이 경로를 모릅니다. nginx 가 이
주소 하나만 8081 로 보냅니다. 수집 서비스가 죽어 있으면 nginx 가 502 를 내고 클라이언트는 그 배치를
스풀에 두었다가 다시 보냅니다. 게임·계정 기능은 영향이 없습니다.

Unity 쪽 전송 구현은 `Backend` 계층에 둡니다. `Client`가 아닙니다. `UnityWebRequest`를
만지는 계층은 `Backend` 하나라는 것이 `_Game/README.md`의 규칙이고, 계정·친구·접속 상태가
그렇게 되어 있습니다.

**v2 에 `Core` 포트는 없습니다.** 발행 지점이 하나뿐이라 포트를 둘 이유가 없었습니다. 표본을
뜨는 `MatchAnalyticsRecorder`는 `Bootstrap`에, 버퍼와 전송(`MatchAnalyticsBuffer`,
`MatchAnalyticsUpload`)은 `Backend`에 있습니다. 발행 지점이 여럿으로 늘어나면 — 5.3 의
클라이언트 발행 7종이 그렇습니다 — 그때 `Core` 포트가 필요해집니다.

### 서버는 기다리지 않는다

서버는 받자마자 상한 큐에 넣고 `202`로 응답합니다. DB 쓰기를 기다리지 않습니다.
큐가 가득 차면 이벤트를 **버립니다.** 절대 블로킹하지 않습니다.

한 IP 가 분당 600요청을 넘기면 `429 RATE_LIMITED` 입니다. 상한이 600 인 이유는 플레이테스트가
한 교실에서 이뤄져 수십 명이 NAT 뒤의 공용 IP 하나로 보이기 때문입니다.

**v2 의 요청은 고르게 오지 않고 경기 종료 시점에 몰립니다.** 보내는 것은 호스트 하나뿐이지만
(다른 클라이언트는 아무것도 보내지 않습니다) 한 경기가 50건씩 쪼개져 나갑니다. 6인·8분이면
위치 표본이 약 2,900건이라 배치가 약 60개이고, 배치 사이 200ms 를 두므로 **12초 안에 요청
60개**입니다. 한 교실에서 다섯 경기가 동시에 끝나면 같은 IP 에서 300 요청이고, 여기에 앱
시작 때의 스풀 재전송이 겹칠 수 있습니다.

분당 600 은 그래도 남지만 v1 이 상정한 "분당 6" 과는 두 자릿수 다릅니다. 동시 경기가 열 판을
넘어가면 상한을 올려야 합니다. 429 는 400 과 달리 **재전송합니다** — 파일을 그대로 두고 배치당
최대 3회, 2초·4초·6초 간격으로 다시 시도합니다.

서버가 배치를 거부하는 경우(400)는 대표적으로 `event_name`이 목록에 없을 때, 배열이 상한을
넘을 때, `occurred_at`이 3절의 범위를 벗어날 때이고, 그 밖에 `params`가 JSON 객체가 아니거나
2048바이트를 넘을 때, 필수 필드 누락, UUID 형식 위반(`clientSessionId`·`matchId`·`userPublicId` —
빈 문자열도 위반) 등 요청 검증 실패도 400 `INVALID_REQUEST` 입니다. 클라이언트는 400 을 받은
배치를 재전송하지 않고 버립니다. 다시 보내도 같은 답이 옵니다.

---

## 8. 개인정보

- **닉네임, 채팅 내용, 음성은 로깅하지 않습니다.** `user_public_id`만 넣습니다
- 설정 이벤트에 설정 *값*을 넣지 않습니다
- 분석 계정은 `SELECT` 권한만 갖습니다

### 탈퇴하면 지운다

게임 DB 는 탈퇴 시 CASCADE 로 그 사람의 행을 전부 지우고, `room_invites` 주석은 그것을
"탈퇴는 흔적을 남기지 않는다"는 약속으로 적어 두었습니다. `game_event`는 FK 가 없어서
그냥 두면 `user_public_id`가 남습니다. **같은 약속을 여기에도 적용합니다.**

계정 서비스가 탈퇴를 커밋한 뒤 수집 서비스의 내부 API `DELETE /internal/users/{userId}/events` 를
공유 키와 함께 부르고, 수집 서비스가 `UPDATE game_event SET user_public_id = NULL WHERE user_public_id = ?`
를 실행합니다(S15P21D205-1002). 행은 남고 사람만 지워집니다. 경기 집계는 "누군가 여기 숨겼다"만
알면 되고 그게 누구였는지는 필요 없으므로, 행을 지우면 남은 다섯 명의 경기가 뒤틀리고
사람만 지우면 아무것도 뒤틀리지 않습니다.

`params` 안의 `owner_id`, `attacker_id` 같은 값도 같은 사람을 가리킵니다. 그것까지 훑어
바꾸는 것은 JSON 갱신이라 비싸고, 그 값들은 `match_start.players`로만 유저와 이어지므로
최상위 컬럼이 NULL 이 된 순간 이미 끊깁니다. 최상위 컬럼만 지웁니다.

**best-effort 입니다.** 수집 서비스가 죽어 있어도 탈퇴는 성공해야 합니다. 호출이 실패하면 계정
서비스가 그 userId 를 메모리에 두고 1분마다 다시 보냅니다(`AnalyticsErasureRelay`). 계정 서비스가 그
사이 재시작하면 그 목록은 사라지고, 그때는 로그를 보고 사람이 같은 UPDATE 를 실행합니다. 탈퇴를 막는
것보다 그 편이 약속에 가깝습니다. 사용자 입장에서 탈퇴는 이미 됐고, 남은 것은 우리가 치울 일입니다.

`ix_game_event_match` 도 `ix_game_event_name` 도 `user_public_id`로 시작하지 않아 이 UPDATE 는
풀 스캔입니다. 탈퇴는 드물고 테이블이 수백만 행이라도 수 초라 인덱스를 따로 두지 않습니다.
탈퇴가 잦아지거나 행이 수천만이 되면 그때 `(user_public_id)` 인덱스를 추가합니다.

---

## 9. 버전과 미확정 항목

`schema_ver`는 현재 `2`입니다. 필드를 추가할 때는 올리고, 집계 쿼리에서 버전을 구분합니다.
뷰는 `schema_ver = 2`만 읽습니다. **이벤트를 지우기보다 새 이름으로 추가하는 편이
안전합니다** — 5.3 의 14개를 화이트리스트에 남겨 둔 이유입니다.

### 정해진 것

- **`position_sample`은 1Hz로 켭니다.** 전체 행의 대부분이 이것이고, 질문 2·3·4 와 5.2 의
  유도가 전부 여기에 얹혀 있습니다. 경기당 상한은 20,000건이며 넘으면 샘플만 버리고
  단계·종료 기록은 남깁니다(`match_end.dropped_samples`)
- **개별 행동 이벤트는 보내지 않습니다.** 같은 사실을 누적값 차분으로 얻고, 잃는 것은
  1초 미만의 시각과 가해자입니다(5.2)

### 확정 대기

승리 조건이 절대평가(종료 시 자기 물건을 들고 있으면 성공)라 순위·점수 개념이 없습니다.
이 규칙이 바뀌면 `player_result`의 `result`가 바뀝니다. 공통 봉투는 이 변경과 무관합니다.

`match_end`의 `escaped_count`는 v2 에서 보내지 않습니다. 탈출 인원은 `player_result`의
`result`를 세어 구합니다.

### 열려 있는 결정

- **질문 6(조작 학습)에 답할지.** `first_interact`와 `settings_changed`가 필요하고, 둘 다
  클라이언트 발행이라 v2 에 없는 경로를 새로 만들어야 합니다. 화면도 없습니다
- **질문 7 을 완전하게 볼지.** `client_quit`이 필요합니다. 경기 전 이탈이 안 보이는 것이
  지금의 가장 큰 빈칸입니다
- **`perf_sample`(fps·핑)을 수집할지.** 성능 문제가 실제로 보고되면 그때 켭니다
