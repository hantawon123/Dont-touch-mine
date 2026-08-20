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

        [Test]
        public void AdvanceIfExpired_AdvancesAtDeadline()
        {
            flow.Start(10d);

            Assert.That(flow.AdvanceIfExpired(189d), Is.False);
            Assert.That(flow.AdvanceIfExpired(190d), Is.True);
            Assert.That(state.CurrentPhase.CurrentValue, Is.EqualTo(MatchPhase.Searching));
            Assert.That(state.PhaseEndsAt.CurrentValue, Is.EqualTo(490d));
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
    }
}
