using System;
using System.Collections.Generic;
using Game.SOAP.Config;

namespace Game.Server.Match
{
    public sealed class HighlightSequence
    {
        private readonly string[] highlightIds;
        private int currentIndex;

        public HighlightSequence(
            IReadOnlyList<string> candidateIds,
            MatchRulesSO rules)
        {
            if (candidateIds == null)
            {
                throw new ArgumentNullException(nameof(candidateIds));
            }

            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            var selectedIds = new List<string>(MatchRulesSO.MaxHighlightCount);
            var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var candidateId in candidateIds)
            {
                if (string.IsNullOrWhiteSpace(candidateId))
                {
                    continue;
                }

                var normalizedId = candidateId.Trim();
                if (!uniqueIds.Add(normalizedId))
                {
                    continue;
                }

                selectedIds.Add(normalizedId);
                if (selectedIds.Count == MatchRulesSO.MaxHighlightCount)
                {
                    break;
                }
            }

            highlightIds = selectedIds.ToArray();
            TotalDurationSeconds = highlightIds.Length * rules.HighlightClipDurationSeconds;
        }

        public int Count => highlightIds.Length;
        public int CurrentIndex => currentIndex;
        public bool IsComplete => currentIndex >= highlightIds.Length;
        public float TotalDurationSeconds { get; }

        public bool TryGetCurrent(out string highlightId)
        {
            if (IsComplete)
            {
                highlightId = null;
                return false;
            }

            highlightId = highlightIds[currentIndex];
            return true;
        }

        public bool CompleteCurrent()
        {
            if (IsComplete)
            {
                return false;
            }

            currentIndex++;
            return true;
        }
    }
}
