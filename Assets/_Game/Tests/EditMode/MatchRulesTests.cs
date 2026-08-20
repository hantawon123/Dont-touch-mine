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
        [TestCase(MatchPhase.Searching, 300f)]
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
    }
}
