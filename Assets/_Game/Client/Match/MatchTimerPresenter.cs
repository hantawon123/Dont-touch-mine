using System;
using Game.Core.Match;
using VContainer.Unity;

namespace Game.Client.Match
{
    public interface IMatchTimerView
    {
        void SetRemainingSeconds(double remainingSeconds);
    }

    public sealed class MatchTimerPresenter : ITickable
    {
        private readonly IMatchState state;
        private readonly IMatchClock clock;
        private readonly IMatchTimerView view;

        public MatchTimerPresenter(
            IMatchState state,
            IMatchClock clock,
            IMatchTimerView view)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
        }

        public void Tick()
        {
            view.SetRemainingSeconds(
                Math.Max(0d, state.PhaseEndsAt.CurrentValue - clock.ServerTime));
        }
    }
}
