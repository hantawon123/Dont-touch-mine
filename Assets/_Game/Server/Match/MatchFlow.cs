using System;
using Game.Core.Match;
using Game.SOAP.Config;

namespace Game.Server.Match
{
    public sealed class MatchFlow
    {
        private readonly MatchRulesSO rules;
        private readonly MatchState state;

        public MatchFlow(MatchRulesSO rules, MatchState state)
        {
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public bool Start(double now)
        {
            ValidateTime(now);

            if (state.CurrentPhase.CurrentValue != MatchPhase.Waiting)
            {
                return false;
            }

            EnterPhase(MatchPhase.Hiding, now);
            return true;
        }

        public bool AdvanceIfExpired(double now)
        {
            ValidateTime(now);
            var advanced = false;

            while (state.PhaseEndsAt.CurrentValue > 0d &&
                   now >= state.PhaseEndsAt.CurrentValue &&
                   MatchPhaseFlow.TryGetNext(state.CurrentPhase.CurrentValue, out var next))
            {
                var nextStartedAt = state.PhaseEndsAt.CurrentValue;
                EnterPhase(next, nextStartedAt);
                advanced = true;
            }

            return advanced;
        }

        public double GetRemainingSeconds(double now)
        {
            ValidateTime(now);
            return Math.Max(0d, state.PhaseEndsAt.CurrentValue - now);
        }

        public bool IsFinalPeriod(double now)
        {
            ValidateTime(now);
            var remainingSeconds = state.PhaseEndsAt.CurrentValue - now;
            return state.CurrentPhase.CurrentValue == MatchPhase.Searching &&
                   remainingSeconds > 0d &&
                   remainingSeconds <= rules.FinalWarningSeconds;
        }

        public bool CompleteHighlight()
        {
            if (state.CurrentPhase.CurrentValue != MatchPhase.Highlight)
            {
                return false;
            }

            state.EnterPhase(MatchPhase.Result, 0d);
            return true;
        }

        private void EnterPhase(MatchPhase phase, double startedAt)
        {
            var duration = rules.GetDurationSeconds(phase);
            state.EnterPhase(phase, duration > 0f ? startedAt + duration : 0d);
        }

        private static void ValidateTime(double time)
        {
            if (double.IsNaN(time) || double.IsInfinity(time) || time < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(time));
            }
        }
    }
}
