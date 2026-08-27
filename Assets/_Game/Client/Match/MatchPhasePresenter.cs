using System;
using Game.Core.Match;
using R3;
using VContainer.Unity;

namespace Game.Client.Match
{
    /// <summary>
    /// Reports the phase, and nothing more.
    /// </summary>
    /// <remarks>
    /// Deliberately does not name the player a hiding turn is waiting on. That
    /// needs the line-up and the match rules, which this scope does not carry,
    /// and the screen players actually see is driven by
    /// <c>NetworkMatchHudPresenter</c> — which does name them. Reviving this
    /// path for a real scene means giving it the same treatment.
    /// </remarks>
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
            subscription = state.CurrentPhase.Subscribe(OnPhaseChanged);
        }

        public void Dispose()
        {
            subscription?.Dispose();
        }

        private void OnPhaseChanged(MatchPhase phase)
        {
            view.SetPhase(phase, string.Empty);
        }
    }
}
