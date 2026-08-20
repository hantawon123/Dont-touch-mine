using Game.Client.Match;
using Game.Core.Match;
using Game.Server.Match;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class MatchPhasePresenterTests
    {
        [Test]
        public void Presenter_ForwardsCurrentAndChangedPhaseToView()
        {
            using var state = new MatchState();
            var view = new FakeMatchPhaseView();
            using var presenter = new MatchPhasePresenter(state, view);

            presenter.Start();
            Assert.That(view.Phase, Is.EqualTo(MatchPhase.Waiting));

            state.EnterPhase(MatchPhase.Hiding, 30d);
            Assert.That(view.Phase, Is.EqualTo(MatchPhase.Hiding));
        }

        private sealed class FakeMatchPhaseView : IMatchPhaseView
        {
            public MatchPhase Phase { get; private set; }

            public void SetPhase(MatchPhase phase)
            {
                Phase = phase;
            }
        }
    }
}
