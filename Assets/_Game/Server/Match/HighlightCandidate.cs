using System;
using System.Collections.Generic;

namespace Game.Server.Match
{
    public readonly struct HighlightSegment
    {
        public HighlightSegment(double startedAt, double endedAt, double playbackSpeed = 1d)
        {
            if (startedAt < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(startedAt));
            }

            if (endedAt < startedAt)
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
            : this(type, new[] { new HighlightSegment(startedAt, endedAt) }, targetId)
        {
        }

        public HighlightCandidate(
            HighlightType type,
            IReadOnlyList<HighlightSegment> segments,
            string targetId)
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

            Type = type;
            Segments = Array.AsReadOnly(copiedSegments);
            StartedAt = copiedSegments[0].StartedAt;
            EndedAt = copiedSegments[copiedSegments.Length - 1].EndedAt;
            PlaybackDurationSeconds = playbackDurationSeconds;
            TargetId = targetId.Trim();
        }

        public HighlightType Type { get; }
        public double StartedAt { get; }
        public double EndedAt { get; }
        public IReadOnlyList<HighlightSegment> Segments { get; }
        public double PlaybackDurationSeconds { get; }
        public string TargetId { get; }
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
                if (!candidatesByType.ContainsKey(candidate.Type))
                {
                    candidatesByType.Add(candidate.Type, candidate);
                }
            }

            var selected = new List<HighlightCandidate>(Game.SOAP.Config.MatchRulesSO.MaxHighlightCount);
            foreach (HighlightType type in Enum.GetValues(typeof(HighlightType)))
            {
                if (candidatesByType.TryGetValue(type, out var candidate))
                {
                    selected.Add(candidate);
                    if (selected.Count == Game.SOAP.Config.MatchRulesSO.MaxHighlightCount)
                    {
                        break;
                    }
                }
            }

            return selected.ToArray();
        }
    }
}
