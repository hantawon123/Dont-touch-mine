using Game.Core.Match;

namespace Game.Server.Match
{
    internal readonly struct MatchStateSnapshot
    {
        public MatchStateSnapshot(MatchPhase phase, double phaseEndsAt)
        {
            Phase = phase;
            PhaseEndsAt = phaseEndsAt;
        }

        public MatchPhase Phase { get; }
        public double PhaseEndsAt { get; }
    }
}
