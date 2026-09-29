using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Backend
{
    [Serializable]
    public sealed class MatchAnalyticsEvent
    {
        public long occurredAt, clientSeq, matchTimeMs;
        public string clientSessionId, roomCode, matchId, userPublicId, eventName, phase, mapId;
        public float posX, posY, posZ;
        public bool fromHost = true;
        public int schemaVer = 2;
        public MatchAnalyticsParams @params = new();

        public string ToJson() => JsonUtility.ToJson(this)
            .Replace("\"userPublicId\":\"\"", "\"userPublicId\":null")
            .Replace("\"roomCode\":\"\"", "\"roomCode\":null");
    }

    [Serializable]
    public sealed class MatchAnalyticsParams
    {
        public int seat = -1, player_count, hide_sec, seek_sec, stun_hits, destroy_limit;
        public float sprint_multiplier, rotation_y;
        public string from, to, end_reason, result, build_ver, posture, item_id;
        public int total_hits_received, total_stuns;
        public int attack_sequence, holder_seat = -1, dropped_samples;
        public bool grounded, item_known, item_destroyed, item_in_motion, partial;
        public float item_x, item_y, item_z;
        public long duration_ms;
        public int expected_events;
        public float sample_interval_sec = 1;
    }

    /// <summary>In-memory match segment. Sampling never performs HTTP or file I/O.</summary>
    public sealed class MatchAnalyticsBuffer
    {
        private const int MaxSamples = 20000;
        private readonly List<string> events = new();
        private readonly string sessionId = Guid.NewGuid().ToString();
        private long sequence;
        private double nextSampleAt;
        private readonly double startedAt;
        public string MatchId { get; } = Guid.NewGuid().ToString();
        public int DroppedSamples { get; private set; }
        public int Count => events.Count;

        public MatchAnalyticsBuffer(double startedAt)
        {
            this.startedAt = startedAt;
            nextSampleAt = startedAt;
        }

        public bool IsSampleDue(double now)
        {
            if (now < nextSampleAt) return false;
            nextSampleAt = now + 1d; // No catch-up burst after a stalled frame.
            return true;
        }

        public void Add(MatchAnalyticsEvent value, double now)
        {
            if (value.eventName == "position_sample" && events.Count >= MaxSamples)
            {
                DroppedSamples++;
                return;
            }
            value.matchId = MatchId;
            value.clientSessionId = sessionId;
            value.clientSeq = sequence++;
            value.occurredAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            value.matchTimeMs = Math.Max(0, (long)((now - startedAt) * 1000));
            if (value.eventName == "match_end") value.@params.duration_ms = value.matchTimeMs;
            events.Add(value.ToJson());
        }

        public string[] Snapshot() => events.ToArray();
    }
}
