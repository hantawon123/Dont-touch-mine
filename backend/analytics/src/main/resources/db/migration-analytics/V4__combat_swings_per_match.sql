-- 휘두른 횟수를 경기 단위로 셉니다 (S15P21D205-895 자체 검토에서 찾은 오류).
--
-- V3 의 match_analysis_combat 은 swings 를 MAX(attack_sequence) 로 셌습니다. 피격·기절
-- 누적이 경기마다 0 에서 시작하니 공격 시퀀스도 그럴 것이라고 본 것인데, 그 셋은 출처가
-- 다릅니다.
--
--   - total_hits_received·total_stuns 는 MatchSessionCoordinator 가 경기마다 새로 만드는
--     배열입니다(MatchRuntimeFactory.CreateSession 이 경기당 한 번 부릅니다). 0 에서 시작합니다.
--   - attack_sequence 는 NetworkPlayerMotor 의 [Networked] 카운터입니다. 플레이어 오브젝트는
--     방에 들어올 때 스폰되고 나갈 때 디스폰되므로, 같은 방에서 두 번째 경기를 하면 지난
--     경기의 값에서 이어집니다. 리셋하는 곳이 없습니다.
--
-- 그래서 두 번째 경기의 swings 가 "지난 경기까지의 총합" 이 됐습니다. 픽스처로 확인한
-- 값은 실제 10 번에 대해 17 이었습니다. 값이 비는 것이 아니라 그럴듯하게 큰 숫자가
-- 나오는 오류라, 실제 데이터에서는 "많이 휘두르네" 로 읽고 지나갔을 것입니다.
--
-- MAX - MIN 으로 세면 두 경우가 모두 맞습니다. 카운터가 0 에서 시작하면 MIN 이 0 이라
-- 답이 같고, 이어지면 그 경기의 증가분만 남습니다. 대가는 첫 샘플(경기 시작 후 1초 안)
-- 이전의 공격이 빠지는 것인데, 숨기기 단계 시작 직후라 실질적으로 없습니다.
--
-- V3 를 고치지 않고 새 파일을 두는 이유: 이미 머지된 마이그레이션이라 이미 적용한 DB 가
-- 있으면 Flyway 가 체크섬 불일치로 기동을 막습니다.
CREATE OR REPLACE SQL SECURITY INVOKER VIEW match_analysis_combat AS
SELECT match_id,
       CAST(params->>'$.seat' AS SIGNED) AS player_seat,
       MAX(CASE WHEN event_name = 'position_sample'
                THEN CAST(params->>'$.attack_sequence' AS UNSIGNED) END)
       - MIN(CASE WHEN event_name = 'position_sample'
                  THEN CAST(params->>'$.attack_sequence' AS UNSIGNED) END) AS swings,
       -- 아래 둘은 경기마다 0 에서 시작하므로 MAX 가 곧 합계입니다. 마지막 1초 사이의
       -- 피격은 위치 샘플에 안 남으므로 종료 기록(player_result)의 값을 먼저 씁니다.
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
