using R3;

namespace Game.Core.Match
{
    public interface IMatchState
    {
        ReadOnlyReactiveProperty<MatchPhase> CurrentPhase { get; }
        ReadOnlyReactiveProperty<double> PhaseEndsAt { get; }
    }
}
