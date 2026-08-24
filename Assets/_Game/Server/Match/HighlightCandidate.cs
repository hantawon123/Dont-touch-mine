using System;
using System.Collections.Generic;

namespace Game.Server.Match
{
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
        {
            if (!Enum.IsDefined(typeof(HighlightType), type))
            {
                throw new ArgumentOutOfRangeException(nameof(type));
            }

            if (startedAt < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(startedAt));
            }

            if (endedAt < startedAt)
            {
                throw new ArgumentOutOfRangeException(nameof(endedAt));
            }

            if (string.IsNullOrWhiteSpace(targetId))
            {
                throw new ArgumentException("Target id is required.", nameof(targetId));
            }

            Type = type;
            StartedAt = startedAt;
            EndedAt = endedAt;
            TargetId = targetId.Trim();
        }

        public HighlightType Type { get; }
        public double StartedAt { get; }
        public double EndedAt { get; }
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
