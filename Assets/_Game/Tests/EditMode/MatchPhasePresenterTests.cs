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

        [Test]
        public void TimerPresenter_UsesServerTimeAndNeverShowsNegativeTime()
        {
            using var state = new MatchState();
            var clock = new FakeMatchClock { ServerTime = 10d };
            var view = new FakeMatchTimerView();
            var presenter = new MatchTimerPresenter(state, clock, view);
            state.EnterPhase(MatchPhase.Hiding, 100d);

            presenter.Tick();
            Assert.That(view.RemainingSeconds, Is.EqualTo(90d));

            clock.ServerTime = 101d;
            presenter.Tick();
            Assert.That(view.RemainingSeconds, Is.Zero);
        }

        private sealed class FakeMatchPhaseView : IMatchPhaseView
        {
            public MatchPhase Phase { get; private set; }

            public void SetPhase(MatchPhase phase)
            {
                Phase = phase;
            }
        }

        private sealed class FakeMatchClock : IMatchClock
        {
            public double ServerTime { get; set; }
        }

        private sealed class FakeMatchTimerView : IMatchTimerView
        {
            public double RemainingSeconds { get; private set; }

            public void SetRemainingSeconds(double remainingSeconds)
            {
                RemainingSeconds = remainingSeconds;
            }
        }
    }
}
