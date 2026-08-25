using System;
using Game.Core.Match;
using R3;
using VContainer.Unity;

namespace Game.Client.Match
{
    public sealed class MatchPhasePresenter : IStartable, IDisposable
    {
        private readonly IMatchState state;
        private readonly IMatchPhaseView view;
        private IDisposable subscription;

        public MatchPhasePresenter(IMatchState state, IMatchPhaseView view)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
        }

        public void Start()
        {
            subscription = state.CurrentPhase.Subscribe(view.SetPhase);
        }

        public void Dispose()
        {
            subscription?.Dispose();
        }
    }
}
