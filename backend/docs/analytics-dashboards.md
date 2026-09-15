# 플레이 로그 대시보드 쿼리 (812·895)

Metabase 의 "플레이 로그"(`d205_analytics`)를 읽는 여덟 화면의 SQL 입니다. 각 화면이 답하는 것은
[`analytics-events.md`](analytics-events.md) 1절의 질문 번호를 따릅니다. 화면은 손으로 만들지 않고
[아래](#화면-만들기) 스크립트가 이 문서를 읽어 만듭니다.

**전부 v2 수집(매초 기록) 위에 서 있습니다.** 클라이언트가 실제로 보내는 것은 다섯 종류
(`match_start`, `phase_change`, `position_sample`, `player_result`, `match_end`)이고, 그 구조는
[`match-analytics.md`](match-analytics.md)에 있습니다. 쿼리는 원본 테이블이 아니라 거기서 만든 세 뷰
(`match_analysis_summary`, `match_analysis_positions`, `match_analysis_combat`)를 읽습니다. JSON 을
꺼내는 일을 여덟 곳에 복사해 두면 필드 이름이 바뀔 때 여덟 곳이 어긋납니다.

**v1 쿼리는 지웠습니다.** 이 문서에는 예전에 `item_hidden`·`item_picked_up`·`client_quit` 을 읽는
다섯 쿼리가 있었습니다. v2 는 그 이벤트를 보내지 않으므로 그 화면들은 영구히 비어 있었습니다.
안 오는 기록을 기다리는 쿼리를 남겨 두면 다음 사람이 "왜 비어 있나"를 처음부터 다시 조사합니다.
필요하면 git 히스토리에 있습니다(812).

**여기 없는 것** — 맵 그림 위에 겹치는 히트맵. Metabase 로 안 되고 데이터가 며칠 쌓인 뒤 따로
만듭니다. 그리고 로비·화면 단위 이탈 — 그건 `client_quit` 이 필요하고 v2 에는 없습니다.

**어디까지 확인했나** (2026-09-09). 운영과 같은 조합(로컬 MySQL 8.4 + Metabase v0.63.16.6)에 손으로
만든 v2 모양 경기 둘 — 완전히 수신된 4인 경기 하나, 예정 건수가 모자란 끊긴 경기 하나 — 을 넣고
여덟 화면의 값을 손계산과 대조했습니다. 실제 플레이 데이터로는 아직 확인하지 않았습니다.

---

## 공통: 완전히 수신된 경기만

경기 단위 집계는 전부 `upload_complete = 1` 을 깔고 갑니다.

```sql
SELECT match_id
FROM match_analysis_summary
WHERE upload_complete = 1
```

**HTTP 202 는 저장 영수증이 아닙니다.** 큐에 접수했다는 뜻이라, API 나 DB 가 흔들리면 그 뒤에서
행이 사라질 수 있습니다. `upload_complete` 는 시작·종료가 모두 있고, `match_end` 가 말한 예정
건수와 실제 행 수가 같고, 중단 표시가 없는 경우입니다. 이걸 안 깔면 절반만 도착한 경기가
"찾는 시간이 짧다" 같은 잘못된 결론을 만듭니다.

샘플 상한(경기당 약 20,000건)까지 따지려면 `dropped_samples = 0` 도 봅니다. 1번 화면이 그 둘을
같이 보여 주는 이유입니다.

**기간 제한이 없습니다.** 아래 쿼리는 쌓인 전체를 봅니다. 경기가 수십 판인 동안은 그게
맞습니다 - 표본이 적은데 기간을 자르면 볼 것이 없습니다. 경기가 수백 판을 넘어 화면이 느려지면
`good` 절에 `started_at_utc >= NOW() - INTERVAL 14 DAY` 같은 조건을 넣으세요. 그 순간 화면의
뜻이 "전체"에서 "최근"으로 바뀌므로, 넣을 때 이 문서에도 적어야 합니다.

호스트가 바뀐 경기는 **다른 경기 UUID 의 부분 구간**으로 들어옵니다. v2 는 그것들을 하나로 합치지
않으므로, 한 판이 두 행으로 보이는 것이 정상입니다.

**`/* @filter */` 표식.** 번호 절의 SQL 마다 경기를 고르는 `WHERE` 끝에 이 주석이 정확히 하나 있습니다.
관리 화면의 분석 조회 API(S15P21D205-976)가 이 문서를 그대로 읽어 그 자리에 기간·경기 조건
(`AND started_at_utc >= ? AND started_at_utc < ? AND match_id = ?`)을 끼워 넣고 실행합니다. 조건이 없으면
빈 문자열로 바뀌므로 Metabase 와 API 는 같은 SQL 을 돕니다. 표식은 `match_analysis_summary` 를 읽는
절에만 둘 수 있습니다 - 끼워 넣는 조건이 그 뷰의 컬럼이기 때문입니다. 1 번 절처럼 `WHERE` 가 없던
쿼리는 `WHERE 1 = 1` 뒤에 둡니다. 표식을 지우거나 둘 이상 두면 수집 서비스가 기동하지 않고 그 이유를
로그에 남기며, `AnalyticsQueryDocTest` 가 먼저 잡습니다.

---

## 1. 경기 목록과 수집 상태

값을 보기 전에 **데이터를 믿을 수 있는지** 보는 화면입니다. 대시보드 맨 위에 둡니다.

```sql
SELECT match_id                    AS `경기`,
       room_code                   AS `방`,
       map_id                      AS `맵`,
       started_at_utc              AS `시작(UTC)`,
       player_count                AS `인원`,
       ROUND(duration_seconds)     AS `진행(초)`,
       end_reason                  AS `종료 사유`,
       received_events             AS `도착`,
       expected_events             AS `예정`,
       upload_complete             AS `완전 수신`,
       dropped_samples             AS `생략 샘플`
FROM match_analysis_summary
WHERE 1 = 1 /* @filter */
ORDER BY started_at_utc DESC
LIMIT 100
```

**시각화**: 표. `완전 수신` 이 0 인 줄은 아래 화면들에서 빠진 경기입니다. `도착` 이 `예정` 보다
작으면 큐에서 유실된 것이고, 크면 같은 경기를 두 번 올린 것입니다. 플레이테스트 직후 이 화면에
0 이 많으면 값을 읽기 전에 수집을 먼저 고쳐야 합니다.

---

## 2. 숨는 시간 적정성 (질문 1)

Hiding 단계가 끝날 때까지 **물건을 제자리에 두지 못한 인원의 비율**을 방 설정(`hide_sec`)별로
봅니다. 비율이 높으면 30초가 짧은 것이고, 0 에 가까우면 줄여도 됩니다.

v1 은 `item_hidden` 이벤트를 셌습니다. v2 에는 그 이벤트가 없으므로 **물건이 처음 멈춘 순간**으로
대신합니다 — 서버가 위치를 알고(`item_known`), 들고 있거나 굴러가는 중이 아니면(`item_in_motion`
= false) 그 자리에 놓인 것입니다.

```sql
WITH good AS (
    SELECT match_id, hide_seconds_per_player
    FROM match_analysis_summary
    WHERE upload_complete = 1 /* @filter */
),
seats AS (
    -- 그 경기에 실제로 샘플이 있는 자리. player_count 로 나누지 않는 이유는 중간에
    -- 들어오거나 나간 사람이 있으면 그 값과 자리 수가 다르기 때문입니다.
    SELECT DISTINCT match_id, player_seat
    FROM match_analysis_positions
),
placed AS (
    SELECT match_id, player_seat
    FROM match_analysis_positions
    WHERE phase = 'Hiding'
      AND item_known = 1 AND item_in_motion = 0 AND item_destroyed = 0
    GROUP BY match_id, player_seat
)
SELECT g.hide_seconds_per_player                  AS `숨는 시간(초)`,
       COUNT(DISTINCT s.match_id)                 AS `경기 수`,
       ROUND(100 * AVG(p.player_seat IS NULL), 1) AS `못 숨긴 인원 %`
FROM seats s
JOIN good g USING (match_id)
LEFT JOIN placed p USING (match_id, player_seat)
GROUP BY g.hide_seconds_per_player
ORDER BY 1
```

**시각화**: 막대. x = 숨는 시간, y = 못 숨긴 인원 %. 경기 수가 5 미만인 막대는 믿지 마세요.

`hide_sec` 은 **한 사람당** 값이고 Hiding 단계 전체 길이는 `hide_sec × player_count` 입니다.
그래서 이 화면의 x 축은 단계 길이가 아니라 방 설정입니다.

---

## 3. 은신처 분포 (질문 2·3)

물건이 **멈춘 자리**를 2m 격자로 묶어 건수와, 그 자리에서 **남에게 들리기까지 걸린 시간**을
붙입니다. 건수가 많고 오래 버티는 칸이 "너무 좋은 자리", 아무도 안 쓰는 칸이 "죽은 구역"입니다.

발견 판정은 소지자입니다. `item_holder_seat` 가 주인(`player_seat`)이 아닌 값으로 바뀌면 남이
가져간 것이고, 주인 자신이 들고 있는 것은 되찾음이라 세지 않습니다.

**한 사람의 첫 자리만 봅니다.** v2 에는 배치 이벤트가 없어서 "다시 숨긴 자리"를 첫 자리와 구분할
근거가 얇습니다. 재배치를 쫓다가 들고 이동하던 중의 좌표를 은신처로 세는 것이 더 나쁩니다.

```sql
WITH good AS (
    SELECT match_id FROM match_analysis_summary WHERE upload_complete = 1 /* @filter */
),
placed AS (
    SELECT p.match_id, p.player_seat, MIN(p.elapsed_seconds) AS hidden_at_sec
    FROM match_analysis_positions p
    JOIN good USING (match_id)
    WHERE p.item_known = 1 AND p.item_in_motion = 0 AND p.item_destroyed = 0
    GROUP BY p.match_id, p.player_seat
),
spot AS (
    -- 그 순간의 물건 좌표. 사람 좌표가 아니라 item_last_* 입니다.
    --
    -- GROUP BY 로 한 자리에 한 줄을 보장합니다. 같은 초에 같은 자리의 샘플이 두 번
    -- 들어오면(중복 업로드) 이 조인이 두 줄을 내고 숨긴 횟수가 부풀기 때문입니다.
    -- 중복 업로드는 upload_complete 가 걸러 주지만, 세는 쿼리가 그 필터에만 기대지
    -- 않게 둡니다 - 이 화면에서 이미 같은 모양의 오류를 두 번 냈습니다.
    SELECT pl.match_id, pl.player_seat, pl.hidden_at_sec,
           MIN(p.map_id)                       AS map_id,
           MIN(FLOOR(p.item_last_x / 2) * 2)   AS gx,
           MIN(FLOOR(p.item_last_z / 2) * 2)   AS gz
    FROM placed pl
    JOIN match_analysis_positions p
      ON p.match_id = pl.match_id
     AND p.player_seat = pl.player_seat
     AND p.elapsed_seconds = pl.hidden_at_sec
    GROUP BY pl.match_id, pl.player_seat, pl.hidden_at_sec
),
taken AS (
    SELECT match_id, player_seat, MIN(elapsed_seconds) AS taken_at_sec
    FROM match_analysis_positions
    WHERE item_holder_seat IS NOT NULL AND item_holder_seat <> player_seat
    GROUP BY match_id, player_seat
)
SELECT s.map_id                                          AS `맵`,
       s.gx                                              AS `x(2m)`,
       s.gz                                              AS `z(2m)`,
       COUNT(*)                                          AS `숨긴 횟수`,
       COUNT(t.taken_at_sec)                             AS `발견된 횟수`,
       ROUND(AVG(t.taken_at_sec - s.hidden_at_sec))      AS `발견까지 평균(초)`,
       ROUND(100 * (1 - COUNT(t.taken_at_sec) / COUNT(*))) AS `끝까지 안 들킨 %`
FROM spot s
LEFT JOIN taken t USING (match_id, player_seat)
GROUP BY s.map_id, s.gx, s.gz
ORDER BY `숨긴 횟수` DESC
```

**시각화**: 표로 시작. Metabase 의 피벗(행 z, 열 x, 값 숨긴 횟수)이 맵 위 히트맵의 임시
대용입니다. `끝까지 안 들킨 %` 가 높으면서 `숨긴 횟수` 도 많은 칸을 먼저 보세요. MySQL 에
중앙값 함수가 없어 평균을 썼습니다.

---

## 4. 체류 구역 (질문 2)

사람이 **어디에 머무는가**입니다. 3번은 물건 자리, 이쪽은 사람 자리입니다. 둘을 나란히 놓으면
"물건은 있는데 아무도 안 가는 구역"과 "사람만 지나가는 구역"이 갈립니다.

```sql
WITH good AS (
    SELECT match_id FROM match_analysis_summary
    WHERE upload_complete = 1 AND dropped_samples = 0 /* @filter */
)
SELECT p.map_id                  AS `맵`,
       p.phase                   AS `단계`,
       FLOOR(p.pos_x / 2) * 2    AS `x(2m)`,
       FLOOR(p.pos_z / 2) * 2    AS `z(2m)`,
       COUNT(*)                  AS `체류 샘플`
FROM match_analysis_positions p
JOIN good USING (match_id)
GROUP BY p.map_id, p.phase, FLOOR(p.pos_x / 2) * 2, FLOOR(p.pos_z / 2) * 2
ORDER BY `체류 샘플` DESC
```

**시각화**: 표. 샘플 하나가 대략 1초이지만 **정확한 초가 아닙니다.** 프레임이 밀리면 샘플이
빠지고, 이탈하면 그 사람의 샘플이 끊깁니다. 그래서 이 화면은 절대 시간이 아니라 구역 사이의
비교로만 읽습니다. 여기서만 `dropped_samples = 0` 을 함께 거는 이유도 그것입니다 — 상한에서
잘린 경기는 구역 비교를 왜곡합니다.

---

## 5. 찾는 시간 적정성 (질문 4)

남의 물건이 **들린 시각의 분포**입니다. Searching 시작(그 경기의 `phase = 'Searching'` 첫 샘플)을
빼서 "찾기 시작 후 몇 초"로 바꿉니다. 분포가 뒤쪽에 몰려 있으면 5분이 짧고, 앞쪽에 몰려 있고
뒤가 비면 길다는 뜻입니다.

```sql
WITH good AS (
    SELECT match_id, seek_seconds
    FROM match_analysis_summary
    WHERE upload_complete = 1 /* @filter */
),
started AS (
    SELECT match_id, MIN(elapsed_seconds) AS seek_start_sec
    FROM match_analysis_positions
    WHERE phase = 'Searching'
    GROUP BY match_id
),
taken AS (
    SELECT match_id, player_seat, MIN(elapsed_seconds) AS taken_at_sec
    FROM match_analysis_positions
    WHERE item_holder_seat IS NOT NULL AND item_holder_seat <> player_seat
    GROUP BY match_id, player_seat
)
SELECT FLOOR((t.taken_at_sec - s.seek_start_sec) / 30) * 30 AS `찾기 시작 후(초)`,
       COUNT(*)                                            AS `발견 건수`,
       MIN(g.seek_seconds)                                 AS `설정된 찾는 시간(초)`
FROM taken t
JOIN good g USING (match_id)
JOIN started s USING (match_id)
WHERE t.taken_at_sec >= s.seek_start_sec
GROUP BY 1
ORDER BY 1
```

**시각화**: 막대(히스토그램). 30초 구간. 방 설정이 여러 종류면 `설정된 찾는 시간` 으로 필터를
하나 두세요. 해상도가 1초라 같은 초에 두 개가 들리면 순서는 알 수 없습니다.

---

## 6. 전투 적정성 (질문 5)

기절까지 필요한 명중 수(`stun_hits`, 기본 3)가 맞는지 봅니다. 사람·경기 단위 합계는
`match_analysis_combat` 뷰가 이미 정리해 둡니다. 그 뷰가 감추는 것이 둘입니다.

- **휘두름은 `MAX - MIN`, 피격·기절은 `MAX`.** 셋 다 누적값인데 리셋 시점이 다릅니다. 피격·기절은
  경기마다 새로 만드는 배열이라 0 에서 시작하지만, `attack_sequence` 는 플레이어 오브젝트의
  `[Networked]` 카운터라 **같은 방에서 두 번째 경기를 하면 지난 경기 값에서 이어집니다.**
- **마지막 1초의 피격은 종료 기록에서 옵니다.** 위치 샘플의 마지막과 경기 종료 사이의 피격은
  샘플에 안 남으므로 `player_result` 의 최종값을 먼저 씁니다.

```sql
WITH good AS (
    SELECT match_id, stun_hits
    FROM match_analysis_summary
    WHERE upload_complete = 1 /* @filter */
)
SELECT g.stun_hits                                                     AS `설정된 기절 펀치`,
       COUNT(*)                                                        AS `사람-경기 수`,
       ROUND(AVG(c.swings), 1)                                         AS `평균 휘두름`,
       ROUND(AVG(c.hits_received), 1)                                  AS `평균 피격`,
       ROUND(AVG(c.stuns), 2)                                          AS `평균 기절`,
       ROUND(100 * SUM(c.hits_received) / NULLIF(SUM(c.swings), 0), 1) AS `명중률 %`
FROM match_analysis_combat c
JOIN good g USING (match_id)
GROUP BY g.stun_hits
ORDER BY 1
```

**시각화**: 막대. x = 설정된 기절 펀치, y = 평균 휘두름·평균 피격·평균 기절.

읽는 법과 한계가 이 화면에서 가장 중요합니다.

- **명중률은 경기 전체의 비율입니다.** 누가 누구를 때렸는지는 수집하지 않으므로 개인별 명중률을
  낼 수 없습니다. 분자는 모두의 피격 합, 분모는 모두의 휘두름 합입니다.
- **휘두름 수는 그 경기의 증가분입니다.** 뷰가 `MAX - MIN` 으로 세는 이유는 위에 있습니다.
  그래서 경기 시작 1초 안의 공격은 빠집니다(숨기기 시작 직후라 실질적으로 없습니다).
- **분모와 분자가 다른 것을 셉니다.** `휘두름` 은 공격 시도이고 `피격` 은 서버가 인정한 명중입니다.
  무적·기절 중 거절된 공격과 빗나간 공격은 피격에 안 들어갑니다. 그래서 명중률이 100% 를 넘는
  일은 없지만, 로비 펀치처럼 집계에서 빠지는 것이 있어 절대값보다 설정별 비교로 읽어야 합니다.
- **숨기기 중의 명중은 피격에 들어가고 기절은 늘지 않습니다**(`match-analytics.md`). 그래서
  `평균 피격` 을 `stun_hits` 로 나눈 값이 `평균 기절` 보다 큰 것이 정상입니다.
- `평균 기절` 이 0 에 가까우면 3회가 너무 많은 것이고, `평균 휘두름` 대비 `평균 기절` 이 크면
  전투가 너무 빨리 끝나는 것입니다.
- 새 빌드(S15P21D205-575) 이전에 쌓인 경기에는 피격·기절 필드가 없어 그 줄만 비어 있습니다.

---

## 7. 물건 생애

물건 하나가 **어떻게 끝났는가**입니다. 3번이 자리를 보는 화면이면 이쪽은 결말을 보는 화면입니다.

```sql
WITH good AS (
    SELECT match_id FROM match_analysis_summary WHERE upload_complete = 1 /* @filter */
),
item AS (
    SELECT p.match_id, p.player_seat,
           MAX(p.item_destroyed) AS destroyed,
           MAX(p.item_holder_seat IS NOT NULL AND p.item_holder_seat <> p.player_seat) AS taken,
           MAX(p.item_known = 1 AND p.item_in_motion = 0) AS ever_placed
    FROM match_analysis_positions p
    JOIN good USING (match_id)
    GROUP BY p.match_id, p.player_seat
)
SELECT CASE
           WHEN destroyed = 1   THEN '부서짐'
           WHEN taken = 1       THEN '남이 가져감'
           WHEN ever_placed = 1 THEN '끝까지 제자리'
           ELSE '숨기지 못함'
       END                                              AS `결말`,
       COUNT(*)                                         AS `물건 수`,
       ROUND(100 * COUNT(*) / SUM(COUNT(*)) OVER (), 1) AS `비율 %`
FROM item
GROUP BY 1
ORDER BY `물건 수` DESC
```

**시각화**: 막대. `끝까지 제자리` 가 절반을 넘으면 찾는 쪽이 너무 어렵거나 시간이 짧은 것이고,
`숨기지 못함` 이 눈에 띄면 2번 화면(숨는 시간)을 같이 보세요.

`부서짐` 을 먼저 판정하는 이유는 파괴가 마지막 사건이기 때문입니다. 남이 가져가서 부순 물건은
"남이 가져감"이 아니라 "부서짐"으로 셉니다 — 주인 입장에서 결말은 파괴입니다.

---

## 8. 경기 중 이탈 (질문 7 부분)

호스트가 매초 **전원**의 위치를 보내므로, 어떤 자리의 기록이 종료보다 일찍 끊기면 그 사람이
빠진 것입니다. `client_quit` 없이 이탈을 보는 유일한 창입니다.

```sql
WITH good AS (
    SELECT match_id, duration_seconds
    FROM match_analysis_summary
    WHERE upload_complete = 1 /* @filter */
),
last_seen AS (
    SELECT p.match_id, p.player_seat, MAX(p.elapsed_seconds) AS last_sec
    FROM match_analysis_positions p
    JOIN good USING (match_id)
    GROUP BY p.match_id, p.player_seat
),
gone AS (
    SELECT l.match_id, l.player_seat, l.last_sec, g.duration_seconds,
           (SELECT q.phase
              FROM match_analysis_positions q
             WHERE q.match_id = l.match_id
               AND q.player_seat = l.player_seat
               AND q.elapsed_seconds = l.last_sec
             LIMIT 1) AS last_phase
    FROM last_seen l
    JOIN good g USING (match_id)
    -- 5초 여유. 마지막 샘플과 종료 사이에는 늘 한두 초가 비므로, 여유 없이 세면
    -- 모든 사람이 이탈로 잡힙니다.
    WHERE l.last_sec < g.duration_seconds - 5
)
SELECT last_phase                              AS `마지막 단계`,
       COUNT(*)                                AS `이탈 인원`,
       ROUND(AVG(duration_seconds - last_sec)) AS `남은 시간 평균(초)`
FROM gone
GROUP BY last_phase
ORDER BY `이탈 인원` DESC
```

**시각화**: 막대. `Searching` 에 몰리면 찾는 단계가 지루한 것이고, `Hiding` 에 몰리면 시작부터
붙잡지 못한 것입니다. `남은 시간 평균` 이 크면 일찍 나간 것입니다.

**이 화면이 못 보는 것.** 로비에서 나간 사람, 매치가 안 잡혀 떠난 사람, 게임을 켜자마자 끈
사람은 여기 없습니다. 경기가 시작된 뒤의 이탈만 보입니다. 화면·단계별 이탈의 완전판은
`client_quit` 을 수집해야 합니다.

호스트가 나간 경우는 다르게 나타납니다. 그 경기는 중단 기록으로 끊기고 `upload_complete` 가 0 이
되어 이 화면에서 통째로 빠집니다. 1번 화면에서 세어야 합니다.

---

## 히트맵으로 보기

3·4번 화면은 표입니다. 표는 "숫자가 큰 칸"만 보여 주고 **어디인지는 못 보여 줍니다.**
같은 CSV 를 그림으로 바꾸는 도구가 `Tools/analytics/heatmap.py` 입니다. 로컬에서 돌립니다.

```
pip install matplotlib
python Tools/analytics/heatmap.py 체류구역.csv --split-by 단계 --log -o dwell.png
```

CSV 는 Metabase 화면 오른쪽 아래 내려받기로 받고, 한글 헤더를 그대로 읽으므로 손볼 것이
없습니다. 옵션·읽는 법·막혔을 때는 [`analytics-heatmap.md`](analytics-heatmap.md) 에 있습니다.

**맵 그림 위에 겹치는 것은 아직 못 합니다.** 맵을 위에서 찍은 그림과 그 그림이 덮는 월드
좌표가 있어야 합니다. 받을 자리(`--map`·`--extent`)는 뚫어 두었습니다.

---

## 화면 만들기

`deploy/metabase/provision_dashboards.py` 가 **이 문서를 읽어** 질문 여덟 개와 그것을 묶은
대시보드("플레이 로그 기본 대시보드")를 만듭니다. SQL 은 이 문서가 원본이고 스크립트는 시각화
종류와 대시보드 배치만 압니다. 손으로 붙여 넣으면 문서와 화면이 조용히 갈라집니다.

서버에서 돌립니다. Metabase 는 8443(Basic Auth) 뒤에 있지만 스크립트는 컨테이너 옆
`127.0.0.1:3000` 으로 붙어서 Basic Auth 와 인증서를 지나지 않습니다.

```
scp backend/docs/analytics-dashboards.md backend/deploy/metabase/provision_dashboards.py d205:/tmp/
ssh -t d205 "MB_USER=<Metabase 관리자 이메일> python3 /tmp/provision_dashboards.py --doc /tmp/analytics-dashboards.md"
```

비밀번호는 물어봅니다(`MB_PASS` 로 줄 수도 있습니다). 무엇을 만들지만 보려면 `--dry-run` 입니다.
표준 라이브러리만 쓰므로 서버에 설치할 것은 없습니다.

여러 번 돌려도 안전합니다. 질문·대시보드를 이름으로 찾아 있으면 갱신합니다. 뒤집어 말하면
**Metabase 화면에서 손으로 고친 SQL·시각화·배치는 다음 실행에서 문서의 값으로 되돌아갑니다.**
화면에서 고친 것이 마음에 들면 이 문서에 옮겨 적고 다시 돌리세요.

- **뷰가 먼저 있어야 합니다.** `match_analysis_summary`·`match_analysis_positions` 는 분석 스키마의
  Flyway 마이그레이션(V2·V3)이 만듭니다. 백엔드를 배포한 뒤 Metabase 의 관리자 → 데이터베이스에서
  **스키마 동기화**를 한 번 눌러야 새 뷰와 컬럼이 보입니다.
- 질문과 대시보드는 **"우리의 분석"** 에 놓입니다. 그건 우리가 만든 컬렉션이 아니라 Metabase 의
  루트 컬렉션이고, 한국어 화면에서 이름이 그렇게 나옵니다. API 에서는 `collection_id` 가 빈 값입니다.
- `'플레이 로그' 데이터베이스가 Metabase 에 없습니다` 로 멈추면 `deploy/README.md` 의
  "Metabase 첫 설정" 을 아직 하지 않은 것입니다. 스크립트는 데이터베이스 연결을 만들지 않습니다.
  거기에는 `d205_reader` 비밀번호가 들어가고, 그건 저장소에 없습니다.
- 데이터가 없으면 화면은 빈 채로 만들어집니다. 경기가 한 판도 안 올라온 상태에서는 그게 정상입니다.
- 무거운 쿼리는 게임과 같은 MySQL 을 때립니다. 대시보드 자동 새로 고침은 켜지 않습니다.
