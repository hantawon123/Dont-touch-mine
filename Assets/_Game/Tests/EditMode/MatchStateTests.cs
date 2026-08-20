using System;
using Game.Core.Match;
using Game.Server.Match;
using NUnit.Framework;
using R3;

namespace Game.Tests.EditMode
{
    public sealed class MatchStateTests
    {
        [Test]
        public void EnterPhase_UpdatesDeadlineBeforePublishingPhase()
        {
            using var state = new MatchState();
            var deadlineSeenBySubscriber = -1d;
            using var subscription = state.CurrentPhase.Subscribe(phase =>
            {
                if (phase == MatchPhase.Hiding)
                {
                    deadlineSeenBySubscriber = state.PhaseEndsAt.CurrentValue;
                }
            });

            state.EnterPhase(MatchPhase.Hiding, 30d);

            Assert.That(state.CurrentPhase.CurrentValue, Is.EqualTo(MatchPhase.Hiding));
            Assert.That(deadlineSeenBySubscriber, Is.EqualTo(30d));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(-1d)]
        public void EnterPhase_RejectsInvalidDeadline(double deadline)
        {
            using var state = new MatchState();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => state.EnterPhase(MatchPhase.Hiding, deadline));
        }
    }
}
