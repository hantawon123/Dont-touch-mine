using System;

namespace Game.Core.Match
{
    public readonly struct MatchStateSnapshot
    {
        public MatchStateSnapshot(MatchPhase phase, double phaseEndsAt)
        {
            if (!Enum.IsDefined(typeof(MatchPhase), phase))
            {
                throw new ArgumentOutOfRangeException(nameof(phase));
            }

            if (double.IsNaN(phaseEndsAt) ||
                double.IsInfinity(phaseEndsAt) ||
                phaseEndsAt < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(phaseEndsAt));
            }

            Phase = phase;
            PhaseEndsAt = phaseEndsAt;
        }

        public MatchPhase Phase { get; }
        public double PhaseEndsAt { get; }
    }
}
