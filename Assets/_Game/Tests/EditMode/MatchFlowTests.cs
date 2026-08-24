using Game.Core.Match;
using Game.Server.Match;
using Game.SOAP.Config;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class MatchFlowTests
    {
        private MatchRulesSO rules;
        private MatchState state;
        private MatchFlow flow;

        [SetUp]
        public void SetUp()
        {
            rules = ScriptableObject.CreateInstance<MatchRulesSO>();
            state = new MatchState();
            flow = new MatchFlow(rules, state);
        }

        [TearDown]
        public void TearDown()
        {
            state.Dispose();
            Object.DestroyImmediate(rules);
        }

        [Test]
        public void Start_EntersHidingAndSetsDeadline()
        {
            var started = flow.Start(10d);

            Assert.That(started, Is.True);
            Assert.That(state.CurrentPhase.CurrentValue, Is.EqualTo(MatchPhase.Hiding));
            Assert.That(state.PhaseEndsAt.CurrentValue, Is.EqualTo(190d));
        }

        [Test]
        public void Start_ReturnsFalseWhenMatchAlreadyStarted()
        {
            flow.Start(10d);

            Assert.That(flow.Start(20d), Is.False);
        }

        [TestCase(10d, 0, 30d)]
        [TestCase(39d, 0, 1d)]
        [TestCase(40d, 1, 30d)]
        [TestCase(160d, 5, 30d)]
        [TestCase(189d, 5, 1d)]
        [TestCase(190d, 5, 0d)]
        public void HidingTurn_UsesThirtySecondsPerPlayer(
            double now,
            int expectedTurnIndex,
            double expectedRemainingSeconds)
        {
            flow.Start(10d);

            Assert.That(flow.GetCurrentHidingTurnIndex(now), Is.EqualTo(expectedTurnIndex));
            Assert.That(
                flow.GetHidingTurnRemainingSeconds(now),
                Is.EqualTo(expectedRemainingSeconds));
        }

        [Test]
        public void HidingTurn_ReturnsInactiveOutsideHidingPhase()
        {
            Assert.That(flow.GetCurrentHidingTurnIndex(0d), Is.EqualTo(-1));
            Assert.That(flow.GetHidingTurnRemainingSeconds(0d), Is.Zero);

            flow.Start(10d);
            flow.AdvanceIfExpired(190d);

            Assert.That(flow.GetCurrentHidingTurnIndex(190d), Is.EqualTo(-1));
            Assert.That(flow.GetHidingTurnRemainingSeconds(190d), Is.Zero);
        }

        [Test]
        public void AdvanceIfExpired_AdvancesAtDeadline()
        {
            flow.Start(10d);

            Assert.That(flow.AdvanceIfExpired(189d), Is.False);
            Assert.That(flow.AdvanceIfExpired(190d), Is.True);
            Assert.That(state.CurrentPhase.CurrentValue, Is.EqualTo(MatchPhase.Searching));
            Assert.That(state.PhaseEndsAt.CurrentValue, Is.EqualTo(550d));
        }

        [Test]
        public void AdvanceIfExpired_CatchesUpAcrossMultiplePhases()
        {
            flow.Start(10d);

            flow.AdvanceIfExpired(600d);

            Assert.That(state.CurrentPhase.CurrentValue, Is.EqualTo(MatchPhase.Result));
            Assert.That(state.PhaseEndsAt.CurrentValue, Is.Zero);
        }

        [Test]
        public void GetRemainingSeconds_NeverReturnsNegativeValue()
        {
            flow.Start(10d);

            Assert.That(flow.GetRemainingSeconds(100d), Is.EqualTo(90d));
            Assert.That(flow.GetRemainingSeconds(1000d), Is.Zero);
        }

        [Test]
        public void IsFinalPeriod_ReturnsTrueOnlyForLastThirtySecondsOfSearching()
        {
            flow.Start(10d);
            flow.AdvanceIfExpired(190d);

            Assert.That(flow.IsFinalPeriod(519d), Is.False);
            Assert.That(flow.IsFinalPeriod(520d), Is.True);
            Assert.That(flow.IsFinalPeriod(549d), Is.True);
            Assert.That(flow.IsFinalPeriod(550d), Is.False);
        }

        [Test]
        public void CompleteHighlight_EndsHighlightBeforeMaximumDuration()
        {
            flow.Start(10d);
            flow.AdvanceIfExpired(550d);

            Assert.That(state.CurrentPhase.CurrentValue, Is.EqualTo(MatchPhase.Highlight));
            Assert.That(state.PhaseEndsAt.CurrentValue, Is.EqualTo(580d));
            Assert.That(flow.CompleteHighlight(), Is.True);
            Assert.That(state.CurrentPhase.CurrentValue, Is.EqualTo(MatchPhase.Result));
            Assert.That(state.PhaseEndsAt.CurrentValue, Is.Zero);
            Assert.That(flow.CompleteHighlight(), Is.False);
        }
    }
}
