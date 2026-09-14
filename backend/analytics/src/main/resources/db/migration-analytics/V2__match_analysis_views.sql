-- End-of-match uploads arrive in batches. Only complete, non-interrupted segments
-- should be included in comparisons; HTTP 202 alone is not a storage receipt.
CREATE SQL SECURITY INVOKER VIEW match_analysis_summary AS
SELECT match_id,
       MAX(room_code) AS room_code,
       MAX(map_id) AS map_id,
       MIN(occurred_at) AS started_at_utc,
       MAX(received_at) AS last_received_at_utc,
       MAX(match_time_ms) / 1000.0 AS duration_seconds,
       MAX(CASE WHEN event_name = 'match_start' THEN CAST(params->>'$.player_count' AS UNSIGNED) END) AS player_count,
       MAX(CASE WHEN event_name = 'match_start' THEN CAST(params->>'$.hide_sec' AS UNSIGNED) END) AS hide_seconds_per_player,
       MAX(CASE WHEN event_name = 'match_start' THEN CAST(params->>'$.seek_sec' AS UNSIGNED) END) AS seek_seconds,
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

CREATE SQL SECURITY INVOKER VIEW match_analysis_positions AS
SELECT match_id, map_id, phase, match_time_ms / 1000.0 AS elapsed_seconds,
       user_public_id, CAST(params->>'$.seat' AS SIGNED) AS player_seat,
       pos_x, pos_y, pos_z, params->>'$.posture' AS posture,
       CAST(params->>'$.attack_sequence' AS UNSIGNED) AS attack_sequence,
       params->>'$.item_id' AS assigned_item_id,
       params->>'$.item_known' = 'true' AS item_known,
       params->>'$.item_destroyed' = 'true' AS item_destroyed,
       params->>'$.item_in_motion' = 'true' AS item_in_motion,
       CAST(params->>'$.holder_seat' AS SIGNED) AS item_holder_seat,
       CAST(params->>'$.item_x' AS DECIMAL(12,4)) AS item_last_x,
       CAST(params->>'$.item_y' AS DECIMAL(12,4)) AS item_last_y,
       CAST(params->>'$.item_z' AS DECIMAL(12,4)) AS item_last_z
FROM game_event
WHERE event_name = 'position_sample' AND from_host = 1 AND schema_ver = 2;
