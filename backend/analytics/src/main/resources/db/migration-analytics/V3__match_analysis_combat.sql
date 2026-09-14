-- 전투를 볼 수 있게 두 뷰를 넓힙니다 (S15P21D205-895).
--
-- V2 의 뷰로는 "몇 번 휘둘렀나"까지만 알 수 있습니다. attack_sequence 가 누적 공격
-- 시도라서 그렇습니다. "몇 번 맞았나, 몇 번 기절했나"가 없어 핵심 질문 5번(기절 펀치
-- 3회가 맞나)에 답할 수 없습니다.
--
-- 클라이언트는 S15P21D205-575 에서 그 둘을 매초 기록에 붙였습니다. 이름은
-- total_hits_received 와 total_stuns 이고 경기 시작부터의 누적값입니다. 새 빌드 이전의
-- 기록에는 그 키가 없고, 없는 키를 params->>'$.x' 로 꺼내면 오류가 아니라 NULL 입니다.
--
-- 설정값 stun_hits 를 요약 뷰에 더하는 것도 같은 이유입니다. 전투 화면의 비교 축이
-- "설정이 3회일 때 vs 5회일 때" 이고, 그 값은 match_start 의 params 에만 있습니다.

CREATE OR REPLACE SQL SECURITY INVOKER VIEW match_analysis_summary AS
SELECT match_id,
       MAX(room_code) AS room_code,
       MAX(map_id) AS map_id,
       MIN(occurred_at) AS started_at_utc,
       MAX(received_at) AS last_received_at_utc,
       MAX(match_time_ms) / 1000.0 AS duration_seconds,
       MAX(CASE WHEN event_name = 'match_start' THEN CAST(params->>'$.player_count' AS UNSIGNED) END) AS player_count,
       MAX(CASE WHEN event_name = 'match_start' THEN CAST(params->>'$.hide_sec' AS UNSIGNED) END) AS hide_seconds_per_player,
       MAX(CASE WHEN event_name = 'match_start' THEN CAST(params->>'$.seek_sec' AS UNSIGNED) END) AS seek_seconds,
       -- 기절까지 필요한 명중 수. 방 설정이고 경기마다 다를 수 있습니다.
       MAX(CASE WHEN event_name = 'match_start' THEN CAST(params->>'$.stun_hits' AS UNSIGNED) END) AS stun_hits,
       MAX(CASE WHEN event_name = 'match_end' THEN params->>'$.end_reason' END) AS end_reason,
       COUNT(*) AS received_events,
       SUM(event_name = 'position_sample') AS position_samples,
       MAX(CASE WHEN event_name = 'match_end' THEN CAST(params->>'$.expected_events' AS UNSIGNED) END) AS expected_events,
       MAX(CASE WHEN event_name = 'match_end' THEN CAST(params->>'$.dropped_samples' AS UNSIGNED) END) AS dropped_samples,
       (SUM(event_name = 'match_start') = 1 AND SUM(event_name = 'match_end') = 1
        AND COUNT(*) = MAX(CASE WHEN event_name = 'match_end' THEN CAST(params->>'$.expected_events' AS UNSIGNED) END)
        AND MAX(CASE WHEN event_name IN ('match_start', 'match_end') THEN params->>'$.partial' END) = 'false') AS upload_complete
FROM game_event
WHERE match_id IS NOT NULL AND from_host = 1 AND schema_ver = 2
GROUP BY match_id;

CREATE OR REPLACE SQL SECURITY INVOKER VIEW match_analysis_positions AS
SELECT match_id, map_id, phase, match_time_ms / 1000.0 AS elapsed_seconds,
       user_public_id, CAST(params->>'$.seat' AS SIGNED) AS player_seat,
       pos_x, pos_y, pos_z, params->>'$.posture' AS posture,
       CAST(params->>'$.attack_sequence' AS UNSIGNED) AS attack_sequence,
       -- 아래 둘은 경기 시작부터의 누적값입니다(S15P21D205-575). 매초 값을 SUM 하면
       -- 중복 집계되므로 쿼리는 MAX 를 씁니다. 과거 기록에는 필드가 없어 NULL 입니다.
       CAST(params->>'$.total_hits_received' AS UNSIGNED) AS total_hits_received,
       CAST(params->>'$.total_stuns' AS UNSIGNED) AS total_stuns,
       params->>'$.item_id' AS assigned_item_id,
       params->>'$.item_known' = 'true' AS item_known,
       params->>'$.item_destroyed' = 'true' AS item_destroyed,
       params->>'$.item_in_motion' = 'true' AS item_in_motion,
       -- 아무도 들고 있지 않으면 클라이언트가 -1 을 보냅니다. JsonUtility 는 int 에 null 을
       -- 쓸 수 없어서 MatchAnalyticsParams 의 기본값이 -1 입니다(holder_seat = -1).
       --
       -- 그 -1 을 그대로 내보내면 "소지자가 있고 그게 주인이 아니다" 라는 조건
       -- (item_holder_seat <> player_seat)이 경기 시작부터 참이 되어, 모든 물건이 처음부터
       -- 남에게 들린 것으로 집계됩니다. 실제로 그렇게 틀렸고(895 검증), 값이 비는 것이
       -- 아니라 그럴듯한 숫자가 나와서 눈으로는 잡히지 않았습니다.
       --
       -- 그래서 "없음" 을 NULL 로 바꿔 내보냅니다. 쿼리마다 -1 을 기억하게 두면 언젠가
       -- 한 곳이 잊습니다.
       CASE WHEN CAST(params->>'$.holder_seat' AS SIGNED) < 0 THEN NULL
            ELSE CAST(params->>'$.holder_seat' AS SIGNED) END AS item_holder_seat,
       CAST(params->>'$.item_x' AS DECIMAL(12,4)) AS item_last_x,
       CAST(params->>'$.item_y' AS DECIMAL(12,4)) AS item_last_y,
       CAST(params->>'$.item_z' AS DECIMAL(12,4)) AS item_last_z
FROM game_event
WHERE event_name = 'position_sample' AND from_host = 1 AND schema_ver = 2;

-- 사람·경기 단위 전투 합계. 세 화면이 아니라 한 곳에서 "최종값이 무엇인가"를 정합니다.
--
-- 위치 샘플의 MAX 만 쓰면 마지막 1초 사이의 피격을 놓칩니다. 그래서 클라이언트가
-- player_result 에도 종료 시점의 누적값을 담습니다(match-analytics.md). 둘 다 있으면
-- 종료 기록이 진실이고, 종료 기록이 없는 옛 데이터에서는 샘플의 MAX 로 물러섭니다.
--
-- 이 판단을 쿼리마다 반복하면 어느 화면은 종료 기록을 잊습니다. 뷰가 한 번 정합니다.
CREATE OR REPLACE SQL SECURITY INVOKER VIEW match_analysis_combat AS
SELECT match_id,
       CAST(params->>'$.seat' AS SIGNED) AS player_seat,
       -- 휘두른 횟수는 위치 샘플에만 있습니다. 이것도 누적이라 MAX 입니다.
       MAX(CASE WHEN event_name = 'position_sample'
                THEN CAST(params->>'$.attack_sequence' AS UNSIGNED) END) AS swings,
       COALESCE(
           MAX(CASE WHEN event_name = 'player_result'
                    THEN CAST(params->>'$.total_hits_received' AS UNSIGNED) END),
           MAX(CASE WHEN event_name = 'position_sample'
                    THEN CAST(params->>'$.total_hits_received' AS UNSIGNED) END)) AS hits_received,
       COALESCE(
           MAX(CASE WHEN event_name = 'player_result'
                    THEN CAST(params->>'$.total_stuns' AS UNSIGNED) END),
           MAX(CASE WHEN event_name = 'position_sample'
                    THEN CAST(params->>'$.total_stuns' AS UNSIGNED) END)) AS stuns
FROM game_event
WHERE match_id IS NOT NULL AND from_host = 1 AND schema_ver = 2
  AND event_name IN ('position_sample', 'player_result')
GROUP BY match_id, CAST(params->>'$.seat' AS SIGNED);
