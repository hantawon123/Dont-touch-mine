using System;
using System.Collections.Generic;
using Game.SOAP.Config;

namespace Game.Server.Match
{
    public sealed class HighlightSequence
    {
        private readonly HighlightCandidate[] highlights;
        private int currentIndex;

        public HighlightSequence(
            IReadOnlyList<HighlightCandidate> candidates,
            MatchRulesSO rules, bool preserveOrder = false)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            if(preserveOrder)
            {
                if(candidates.Count > MatchRulesSO.MaxHighlightCount) throw new ArgumentException("Too many selected highlights.");
                highlights=new HighlightCandidate[candidates.Count];
                for(int i=0;i<candidates.Count;i++) highlights[i]=candidates[i];
            }
            else highlights = HighlightCandidateSelector.Select(candidates);
            var totalDurationSeconds = 0f;
            foreach (var highlight in highlights)
            {
                totalDurationSeconds += (float)highlight.PlaybackDurationSeconds;
            }

            TotalDurationSeconds = totalDurationSeconds;
        }

        public int Count => highlights.Length;
        public int CurrentIndex => currentIndex;
        public bool IsComplete => currentIndex >= highlights.Length;
        public float TotalDurationSeconds { get; }
        public double ScheduledDurationSeconds =>
            TotalDurationSeconds + highlights.Length *
            Game.Core.Match.HighlightPresentationTiming.OverheadSeconds;

        public bool TryGetCurrent(out HighlightCandidate highlight)
        {
            if (IsComplete)
            {
                highlight = default;
                return false;
            }

            highlight = highlights[currentIndex];
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

        public HighlightCandidate[] Capture()
        {
            var snapshot = new HighlightCandidate[highlights.Length];
            Array.Copy(highlights, snapshot, highlights.Length);
            return snapshot;
        }
    }
}
