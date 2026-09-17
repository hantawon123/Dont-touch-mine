# 경기 분석 수집 v2

호스트가 숨기기·찾기 중 플레이어 상태를 **약 1초 간격**으로 메모리에 모읍니다.
프레임이 늦어져도 밀린 샘플을 한꺼번에 생성하지 않습니다. 0.1초 하이라이트 기록은 별개입니다.
현재 경기 데이터는 결과 수신 후 `POST /api/v1/events`로 50건씩 순서대로 전송합니다.
배치 사이에는 200ms를 두며 클라이언트별 중복 수집은 하지 않습니다.
4명·10분이면 위치 기록 약 2,400건과 시작·전환·결과·종료 기록이 생깁니다.

## 수집 항목

| 이벤트 | 내용 |
| --- | --- |
| match_start | 경기 UUID, 맵, 방 코드, 인원, 게임 규칙, 빌드 버전 |
| phase_change | 이전·다음 페이즈와 경기 경과 시간 |
| position_sample | 플레이어 좌표·회전·자세·접지·누적 공격 시퀀스, 배정 물건 ID·파괴 여부·소지자·마지막 서버 좌표 |
| player_result | 참가 자리 번호와 Winner/Loser |
| match_end | 종료 이유, 경과 시간, 전송 예정 건수, 상한으로 생략된 샘플 수, 중단 여부 |

`schemaVer=2`입니다. **이 수집에는 닉네임·채팅·음성이 들어가지 않습니다.** 사용자 UUID가 없으면
`userPublicId=null`이며 같은 경기 안에서는 `seat`로 구분합니다.

채팅은 2026-09-17부터 **별개의 경로로** 따로 담습니다(S15P21D205-1027). 여기와 헷갈리면 안 되므로
차이를 적어 둡니다. 분석(`d205_analytics`)은 익명 집계용이고 지금도 채팅을 담지 않습니다. 채팅은
신고 조사 목적으로만 계정 서비스 DB의 `chat_logs`에 담기며 3일 뒤 지워집니다. 자세한 것은
[채팅 정화와 기록](chat-moderation.md)에 있습니다. 음성은 여전히 수집하지 않습니다.
공격 명중, 줍기·놓기의 정확한 시각은 이번 수집에 없습니다. 1초 사이 일어난 행동은 놓칠 수 있습니다.
물건 좌표는 서버 스냅샷의 마지막 좌표입니다. 들고 있거나 이동 중이면 `item_in_motion=true`이므로
정적인 숨김 위치 분석에서는 `item_known=true AND item_in_motion=false`만 사용합니다.

## 저장 및 누락 판별

- 원본: 기존 `d205_analytics.game_event`. V1 테이블과 API는 그대로 사용합니다.
- 조회: V2 Flyway 마이그레이션이 `match_analysis_summary`, `match_analysis_positions` 뷰를 만듭니다.
- 요약의 `upload_complete=1`은 시작·종료가 모두 있고 예정 건수와 실제 행 수가 같으며 중단 표시가 없는 경우입니다.
  샘플 상한까지 검증하려면 `dropped_samples=0`도 확인합니다.
- 호스트 이탈·권한 상실 시 확보한 구간만 중단 기록으로 전송합니다. 호스트 이관 후에는 새 경기 UUID의
  부분 구간으로 기록하며, 호스트 사이 구간을 자동으로 하나의 완전한 경기로 합치지 않습니다.
- 위치 기록은 경기당 최대 약 20,000건입니다. 페이즈·종료 기록은 상한 이후에도 남깁니다.
- 종료 시 `persistentDataPath/match-analytics/*.jsonl`에 기록한 후 전송합니다. 네트워크 오류·429·서버 오류는
  배치당 최대 3회 시도하며, 남은 파일은 다음 경기 종료 또는 앱 시작 때 같은 이벤트 ID로 재시도합니다.
  400·413 파일은 `.rejected`로 보관하여 후속 경기 전송을 막지 않습니다. 보관 상한은 10개 경기입니다.
- HTTP 202는 **큐 접수**입니다. API/DB 장애로 큐에서 유실되면 자동 복구하지 못할 수 있으므로 뷰에서
  실제 도착 건수를 확인해야 합니다. 접수 완료 파일은 제거합니다.
- 브라우저 강제 종료 전의 미완료 경기는 전송을 보장하지 않습니다. WebGL 파일의 브라우저 재시작 후
  영속성도 검증하지 않았으므로 파일 보관을 완전한 유실 방지 장치로 보지 않습니다.

## SQL 로 직접 보기

정해진 질문은 관리 화면 분석 탭에 있습니다(`analytics-dashboards.md`). 그 밖의 것을 한 번 보려면 EC2 에서
읽기 계정으로 분석 DB 에 붙어 아래 SQL 을 돌립니다(`deploy/README.md` 의 "즉석 SQL 은 읽기 계정으로").
기존 v1 대시보드의 세부 행동 지표는 이 5종 이벤트만으로 계산할 수 없습니다.

경기 목록과 수집 상태:

```sql
SELECT match_id AS 경기ID, room_code AS 방, map_id AS 맵,
       started_at_utc AS 시작UTC, player_count AS 인원,
       duration_seconds AS 진행초, end_reason AS 종료사유,
       received_events AS 도착건수, expected_events AS 예정건수,
       upload_complete AS 완전수신, dropped_samples AS 생략샘플
FROM d205_analytics.match_analysis_summary
ORDER BY started_at_utc DESC
LIMIT 100;
```

특정 경기 이동·물건 타임라인(`<match_id>` 자리에 경기 UUID 를 문자열로 넣습니다):

```sql
SELECT elapsed_seconds AS 경과초, phase AS 페이즈, player_seat AS 참가자리,
       pos_x AS X, pos_y AS Y, pos_z AS Z, posture AS 자세,
       assigned_item_id AS 배정물건, item_holder_seat AS 소지자자리,
       item_destroyed AS 파괴, item_in_motion AS 이동중,
       item_last_x AS 물건X, item_last_y AS 물건Y, item_last_z AS 물건Z
FROM d205_analytics.match_analysis_positions
WHERE match_id = '<match_id>'
ORDER BY elapsed_seconds, player_seat;
```

맵·페이즈별 2m 구역 체류 샘플 수(완전히 수신한 경기만):

```sql
SELECT p.map_id, p.phase, FLOOR(p.pos_x / 2) * 2 AS 구역X,
       FLOOR(p.pos_z / 2) * 2 AS 구역Z, COUNT(*) AS 체류샘플
FROM d205_analytics.match_analysis_positions p
JOIN d205_analytics.match_analysis_summary s ON s.match_id = p.match_id
WHERE s.upload_complete = 1 AND s.dropped_samples = 0
GROUP BY p.map_id, p.phase, FLOOR(p.pos_x / 2) * 2, FLOOR(p.pos_z / 2) * 2;
```

샘플 수는 대략적인 체류 지표이며 프레임 지연·이탈 때문에 정확한 초 단위 시간과 일치하지 않습니다.

## 검증 및 반영

- Unity EditMode: 1초 간격·지연 후 몰아쓰기 방지, JSON 형식, 50건 배치, 실패 보관·동일 ID 재시도,
  잘못된 배치 격리 후 다음 경기 전송을 확인합니다.
- MySQL 8.4 임시 스키마에서 V1/V2 실행과 누락 배치·완전 수신·중단 구분, 물건 좌표 변환을 검증했습니다.
  운영 원본 테이블에는 테스트 데이터를 넣지 않았습니다.
- 실제 수집에는 새 Unity 빌드 배포가 필요합니다. 뷰에는 백엔드 배포가 필요합니다.
  실제 멀티플레이 경기 종료 → HTTP 202 → DB 건수 일치 → 분석 탭 조회까지의 운영 검증은 별도입니다.

## 누적 피격·기절 횟수

`position_sample.params.total_hits_received`는 이번 경기에서 서버가 인정한 누적 피격 횟수,
`total_stuns`는 누적 기절 발생 횟수입니다. 기절 시 초기화되는 현재 피격 스택과 다릅니다.
숨기기 대기 중 인정된 펀치도 피격에 포함하지만 기절은 늘지 않습니다. 무적·기절 중 거절된 공격,
빗나간 공격과 로비 펀치는 집계하지 않습니다. 새 경기에서 0부터 시작합니다.
마지막 1초 사이 발생한 횟수를 놓치지 않도록 `player_result`에도 종료 시 누적값을 담습니다.
기존 `game_event.params` JSON으로 저장하므로 API/테이블 마이그레이션은 필요하지 않습니다.
과거 기록에는 필드가 없으며 NULL은 미수집을 의미합니다.
V3 마이그레이션(S15P21D205-895)이 위치 뷰에 `total_hits_received`·`total_stuns` 열을 열었고,
사람·경기 단위 합계는 `match_analysis_combat` 뷰가 종료 기록을 우선해 정리합니다. 아래 원본 조회는
초 단위 추이를 볼 때만 씁니다.

```sql
SELECT match_id, match_time_ms, params->>'$.seat' AS player_seat,
       CAST(params->>'$.total_hits_received' AS UNSIGNED) AS total_hits_received,
       CAST(params->>'$.total_stuns' AS UNSIGNED) AS total_stuns
FROM game_event
WHERE event_name = 'position_sample' AND from_host = 1 AND schema_ver = 2
ORDER BY match_id, match_time_ms, player_seat;
```

누적값이므로 매초 값을 SUM하면 중복 집계됩니다. 최종값은 `player_result`를 사용하거나
완료된 동일 경기·플레이어의 MAX를 조회합니다.
