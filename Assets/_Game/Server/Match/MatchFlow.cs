using System;
using Game.Core.Match;
using Game.SOAP.Config;
using VContainer;

namespace Game.Server.Match
{
    public sealed class MatchFlow
    {
        private readonly MatchRulesSO rules;
        private readonly MatchState state;
        private readonly int playerCount;
        private double? highlightPresentationDuration;

        public void SetHighlightPresentationDuration(double duration)
        {
            ValidateTime(duration);
            highlightPresentationDuration = duration;
        }

        [Inject]
        public MatchFlow(MatchRulesSO rules, MatchState state)
            : this(rules, state, MatchRulesSO.MaxPlayerCount)
        {
        }

        public MatchFlow(MatchRulesSO rules, MatchState state, int playerCount)
        {
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            MatchRulesSO.ValidatePlayerCount(playerCount);
            this.playerCount = playerCount;
        }

        public int PlayerCount => playerCount;
        public float HidingDurationSeconds => rules.GetHidingDurationSeconds(playerCount);

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

        /// <remarks>
        /// The rule itself lives in <see cref="HidingTurns"/> because the screens
        /// report the same turn from the same replicated values, and a rule kept
        /// in two places is a rule that eventually disagrees with itself.
        /// </remarks>
        public int GetCurrentHidingTurnIndex(double now)
        {
            ValidateTime(now);
            return HidingTurns.IndexAt(
                state.CurrentPhase.CurrentValue,
                state.PhaseEndsAt.CurrentValue,
                now,
                playerCount,
                rules.HidingTurnDurationSeconds);
        }

        public double GetHidingTurnRemainingSeconds(double now)
        {
            // Checked here now that the turn index is no longer asked for first.
            ValidateTime(now);
            return HidingTurns.RemainingSecondsAt(
                state.CurrentPhase.CurrentValue,
                state.PhaseEndsAt.CurrentValue,
                now,
                playerCount,
                rules.HidingTurnDurationSeconds);
        }

        internal bool SkipCurrentHidingTurn(double now)
        {
            var remaining = GetHidingTurnRemainingSeconds(now);
            if (remaining <= 0d) return false;
            // Keep the original player indices. Moving the shared deadline also
            // advances the turn derived by clients from HidingTurns.
            state.EnterPhase(MatchPhase.Hiding, Math.Max(now, state.PhaseEndsAt.CurrentValue - remaining));
            return true;
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

        public bool CompleteSearchingEarly(double now)
        {
            ValidateTime(now);
            if (state.CurrentPhase.CurrentValue != MatchPhase.Searching ||
                now >= state.PhaseEndsAt.CurrentValue)
            {
                return false;
            }

            EnterPhase(MatchPhase.Highlight, now);
            return true;
        }

        public bool CompleteMatchEarly()
        {
            var phase = state.CurrentPhase.CurrentValue;
            if (phase != MatchPhase.Hiding && phase != MatchPhase.Searching)
            {
                return false;
            }

            state.EnterPhase(MatchPhase.Result, 0d);
            return true;
        }

        private void EnterPhase(MatchPhase phase, double startedAt)
        {
            var duration = rules.GetDurationSeconds(phase, playerCount);
            if (phase == MatchPhase.Highlight)
                duration += (float)(HighlightPresentationTiming.PostRollSeconds +
                    HighlightPresentationTiming.DeliveryGraceSeconds +
                    MatchRulesSO.MaxHighlightCount * HighlightPresentationTiming.OverheadSeconds);
            var actualDuration = phase == MatchPhase.Highlight && highlightPresentationDuration.HasValue
                ? highlightPresentationDuration.Value : duration;
            state.EnterPhase(phase, actualDuration > 0d ? startedAt + actualDuration : 0d);
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
