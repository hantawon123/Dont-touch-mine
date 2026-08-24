using Game.Core.Match;
using Game.SOAP.Config;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class MatchRulesTests
    {
        [TestCase(MatchPhase.Waiting, 0f)]
        [TestCase(MatchPhase.Hiding, 180f)]
        [TestCase(MatchPhase.Searching, 360f)]
        [TestCase(MatchPhase.Highlight, 30f)]
        [TestCase(MatchPhase.Result, 0f)]
        public void GetDurationSeconds_ReturnsDurationForPhase(MatchPhase phase, float expected)
        {
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();

            try
            {
                Assert.That(rules.GetDurationSeconds(phase), Is.EqualTo(expected));
            }
            finally
            {
                Object.DestroyImmediate(rules);
            }
        }

        [Test]
        public void HidingDuration_IsSixThirtySecondTurns()
        {
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();

            try
            {
                Assert.That(MatchRulesSO.PlayerCount, Is.EqualTo(6));
                Assert.That(rules.HidingTurnDurationSeconds, Is.EqualTo(30f));
                Assert.That(rules.HidingDurationSeconds, Is.EqualTo(180f));
            }
            finally
            {
                Object.DestroyImmediate(rules);
            }
        }

        [Test]
        public void PlayerInteractionRules_HaveMvpDefaults()
        {
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();

            try
            {
                Assert.That(rules.DestructionUsesPerPlayer, Is.EqualTo(5));
                Assert.That(rules.HitsRequiredToStun, Is.EqualTo(3));
                Assert.That(rules.StunDurationSeconds, Is.EqualTo(2f));
                Assert.That(rules.InvulnerabilityDurationSeconds, Is.Zero);
                Assert.That(MatchRulesSO.MaxHighlightCount, Is.EqualTo(3));
                Assert.That(rules.HighlightClipDurationSeconds, Is.EqualTo(10f));
            }
            finally
            {
                Object.DestroyImmediate(rules);
            }
        }
    }
}
