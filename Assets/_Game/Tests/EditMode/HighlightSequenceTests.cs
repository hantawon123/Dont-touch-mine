using Game.Server.Match;
using Game.SOAP.Config;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class HighlightSequenceTests
    {
        private MatchRulesSO rules;

        [SetUp]
        public void SetUp()
        {
            rules = ScriptableObject.CreateInstance<MatchRulesSO>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(rules);
        }

        [Test]
        public void Constructor_SelectsAtMostThreeUniqueHighlights()
        {
            var sequence = new HighlightSequence(
                new[]
                {
                    Candidate(HighlightType.MostStunned),
                    Candidate(HighlightType.FirstBlood),
                    Candidate(HighlightType.LongestHidden),
                    Candidate(HighlightType.FinalMoment)
                },
                rules);

            Assert.That(sequence.Count, Is.EqualTo(3));
            Assert.That(sequence.TotalDurationSeconds, Is.EqualTo(30f));
            Assert.That(sequence.TryGetCurrent(out var current), Is.True);
            Assert.That(current.Type, Is.EqualTo(HighlightType.FirstBlood));
        }

        [Test]
        public void CompleteCurrent_EndsAfterAvailableHighlights()
        {
            var sequence = new HighlightSequence(
                new[]
                {
                    Candidate(HighlightType.FirstBlood),
                    Candidate(HighlightType.FinalMoment)
                },
                rules);

            Assert.That(sequence.TotalDurationSeconds, Is.EqualTo(20f));
            Assert.That(sequence.CompleteCurrent(), Is.True);
            Assert.That(sequence.CurrentIndex, Is.EqualTo(1));
            Assert.That(sequence.TryGetCurrent(out var current), Is.True);
            Assert.That(current.Type, Is.EqualTo(HighlightType.FinalMoment));
            Assert.That(sequence.CompleteCurrent(), Is.True);
            Assert.That(sequence.IsComplete, Is.True);
            Assert.That(sequence.CompleteCurrent(), Is.False);
        }

        [Test]
        public void EmptyCandidates_CompleteImmediately()
        {
            var sequence = new HighlightSequence(new HighlightCandidate[0], rules);

            Assert.That(sequence.TotalDurationSeconds, Is.Zero);
            Assert.That(sequence.IsComplete, Is.True);
            Assert.That(sequence.TryGetCurrent(out _), Is.False);
        }

        private static HighlightCandidate Candidate(HighlightType type)
        {
            return new HighlightCandidate(type, 0d, 10d, type.ToString());
        }
    }
}
