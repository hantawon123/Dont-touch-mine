using System;
using System.Collections.Generic;

namespace Game.Server.Match
{
    public readonly struct HighlightSegment
    {
        public HighlightSegment(double startedAt, double endedAt, double playbackSpeed = 1d)
        {
            if (!double.IsFinite(startedAt) || startedAt < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(startedAt));
            }

            if (!double.IsFinite(endedAt) || endedAt < startedAt)
            {
                throw new ArgumentOutOfRangeException(nameof(endedAt));
            }

            if (playbackSpeed <= 0d || double.IsNaN(playbackSpeed) || double.IsInfinity(playbackSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(playbackSpeed));
            }

            StartedAt = startedAt;
            EndedAt = endedAt;
            PlaybackSpeed = playbackSpeed;
        }

        public double StartedAt { get; }
        public double EndedAt { get; }
        public double PlaybackSpeed { get; }
        public double PlaybackDurationSeconds => (EndedAt - StartedAt) / PlaybackSpeed;
    }

    public enum HighlightType
    {
        FirstBlood,
        TteTanMulgun,
        FinalMoment,
        LongestHidden,
        MostStunned
    }

    public readonly struct HighlightCandidate
    {
        public HighlightCandidate(
            HighlightType type,
            double startedAt,
            double endedAt,
            string targetId)
            : this(
                type,
                new[] { new HighlightSegment(startedAt, endedAt) },
                targetId,
                eventAt: endedAt,
                score: DefaultScore(type))
        {
        }

        public HighlightCandidate(
            HighlightType type,
            IReadOnlyList<HighlightSegment> segments,
            string targetId)
            : this(
                type,
                segments,
                targetId,
                eventAt: segments != null && segments.Count > 0
                    ? segments[segments.Count - 1].EndedAt
                    : 0d,
                score: DefaultScore(type))
        {
        }

        public HighlightCandidate(
            HighlightType type,
            IReadOnlyList<HighlightSegment> segments,
            string targetId,
            double eventAt,
            double score,
            int actorPlayerIndex = -1,
            int secondaryPlayerIndex = -1)
        {
            if (!Enum.IsDefined(typeof(HighlightType), type))
            {
                throw new ArgumentOutOfRangeException(nameof(type));
            }

            if (segments == null)
            {
                throw new ArgumentNullException(nameof(segments));
            }

            if (segments.Count == 0)
            {
                throw new ArgumentException("At least one segment is required.", nameof(segments));
            }

            if (string.IsNullOrWhiteSpace(targetId))
            {
                throw new ArgumentException("Target id is required.", nameof(targetId));
            }

            if (!double.IsFinite(eventAt) || eventAt < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(eventAt));
            }

            if (!double.IsFinite(score) || score < 0d || score > 100d)
            {
                throw new ArgumentOutOfRangeException(nameof(score));
            }

            if (actorPlayerIndex < -1 || actorPlayerIndex >= Game.SOAP.Config.MatchRulesSO.MaxPlayerCount)
            {
                throw new ArgumentOutOfRangeException(nameof(actorPlayerIndex));
            }

            if (secondaryPlayerIndex < -1 ||
                secondaryPlayerIndex >= Game.SOAP.Config.MatchRulesSO.MaxPlayerCount)
            {
                throw new ArgumentOutOfRangeException(nameof(secondaryPlayerIndex));
            }

            var copiedSegments = new HighlightSegment[segments.Count];
            double playbackDurationSeconds = 0d;
            for (var index = 0; index < segments.Count; index++)
            {
                if (segments[index].PlaybackSpeed <= 0d)
                {
                    throw new ArgumentException("Every segment must be valid.", nameof(segments));
                }

                copiedSegments[index] = segments[index];
                playbackDurationSeconds += segments[index].PlaybackDurationSeconds;
            }

            if (eventAt < copiedSegments[0].StartedAt ||
                eventAt > copiedSegments[copiedSegments.Length - 1].EndedAt)
            {
                throw new ArgumentOutOfRangeException(nameof(eventAt));
            }

            Type = type;
            Segments = Array.AsReadOnly(copiedSegments);
            StartedAt = copiedSegments[0].StartedAt;
            EndedAt = copiedSegments[copiedSegments.Length - 1].EndedAt;
            PlaybackDurationSeconds = playbackDurationSeconds;
            TargetId = targetId.Trim();
            EventAt = eventAt;
            Score = score;
            ActorPlayerIndex = actorPlayerIndex;
            SecondaryPlayerIndex = secondaryPlayerIndex;
        }

        public HighlightType Type { get; }
        public double StartedAt { get; }
        public double EndedAt { get; }
        public IReadOnlyList<HighlightSegment> Segments { get; }
        public double PlaybackDurationSeconds { get; }
        public string TargetId { get; }
        public double EventAt { get; }
        public double Score { get; }
        public int ActorPlayerIndex { get; }
        public int SecondaryPlayerIndex { get; }

        private static double DefaultScore(HighlightType type) =>
            Enum.IsDefined(typeof(HighlightType), type)
                ? Enum.GetValues(typeof(HighlightType)).Length - (int)type
                : 0d;
    }

    public static class HighlightCandidateSelector
    {
        public static HighlightCandidate[] Select(IReadOnlyList<HighlightCandidate> candidates)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            var candidatesByType = new Dictionary<HighlightType, HighlightCandidate>();
            foreach (var candidate in candidates)
            {
                if (!candidatesByType.TryGetValue(candidate.Type, out var selected) ||
                    candidate.Score > selected.Score ||
                    candidate.Score == selected.Score && candidate.EventAt > selected.EventAt)
                {
                    candidatesByType[candidate.Type] = candidate;
                }
            }

            var ranked = new List<HighlightCandidate>(candidatesByType.Values);
            ranked.Sort((left, right) =>
            {
                var scoreOrder = right.Score.CompareTo(left.Score);
                return scoreOrder != 0
                    ? scoreOrder
                    : left.Type.CompareTo(right.Type);
            });

            if (ranked.Count > Game.SOAP.Config.MatchRulesSO.MaxHighlightCount)
            {
                ranked.RemoveRange(
                    Game.SOAP.Config.MatchRulesSO.MaxHighlightCount,
                    ranked.Count - Game.SOAP.Config.MatchRulesSO.MaxHighlightCount);
            }

            return ranked.ToArray();
        }
    }
}
