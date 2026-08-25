using Game.Core.Match;
using Game.Server.Match;
using Game.SOAP.Config;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class MatchSynchronizationTests
    {
        private const int PlayerCount = 6;

        [Test]
        public void SixPlayers_KeepTheSamePhaseAndDeadline()
        {
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();
            var states = CreateStates();

            try
            {
                var flow = new MatchFlow(rules, states[0]);

                flow.Start(10d);
                Synchronize(states);
                AssertSynchronized(states, MatchPhase.Hiding, 190d);

                flow.AdvanceIfExpired(190d);
                Synchronize(states);
                AssertSynchronized(states, MatchPhase.Searching, 550d);

                flow.AdvanceIfExpired(550d);
                Synchronize(states);
                AssertSynchronized(states, MatchPhase.Highlight, 580d);

                flow.AdvanceIfExpired(580d);
                Synchronize(states);
                AssertSynchronized(states, MatchPhase.Result, 0d);
            }
            finally
            {
                foreach (var state in states)
                {
                    state.Dispose();
                }

                Object.DestroyImmediate(rules);
            }
        }

        private static MatchState[] CreateStates()
        {
            var states = new MatchState[PlayerCount];
            for (var i = 0; i < states.Length; i++)
            {
                states[i] = new MatchState();
            }

            return states;
        }

        private static void Synchronize(MatchState[] states)
        {
            var snapshot = states[0].CaptureSnapshot();
            for (var i = 1; i < states.Length; i++)
            {
                states[i].ApplySnapshot(snapshot);
            }
        }

        private static void AssertSynchronized(
            MatchState[] states,
            MatchPhase expectedPhase,
            double expectedDeadline)
        {
            foreach (var state in states)
            {
                Assert.That(state.CurrentPhase.CurrentValue, Is.EqualTo(expectedPhase));
                Assert.That(state.PhaseEndsAt.CurrentValue, Is.EqualTo(expectedDeadline));
            }
        }
    }
}
