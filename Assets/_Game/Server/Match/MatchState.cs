using System;
using Game.Core.Match;
using R3;

namespace Game.Server.Match
{
    public sealed class MatchState : IDisposable
    {
        private readonly ReactiveProperty<MatchPhase> currentPhase = new(MatchPhase.Waiting);
        private readonly ReactiveProperty<double> phaseEndsAt = new(0d);

        public ReadOnlyReactiveProperty<MatchPhase> CurrentPhase => currentPhase;
        public ReadOnlyReactiveProperty<double> PhaseEndsAt => phaseEndsAt;

        internal void EnterPhase(MatchPhase phase, double endsAt)
        {
            if (double.IsNaN(endsAt) || double.IsInfinity(endsAt) || endsAt < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(endsAt));
            }

            phaseEndsAt.Value = endsAt;
            currentPhase.Value = phase;
        }

        public void Dispose()
        {
            currentPhase.Dispose();
            phaseEndsAt.Dispose();
        }
    }
}
