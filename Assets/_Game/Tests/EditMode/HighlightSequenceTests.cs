using Game.Bootstrap;
using Game.Core.Match;
using Game.Core.Rooms;
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

        [TestCase(HighlightType.FirstBlood, "FIRST BLOOD")]
        [TestCase(HighlightType.TteTanMulgun, "HOT ITEM")]
        [TestCase(HighlightType.FinalMoment, "FINAL MOMENT")]
        [TestCase(HighlightType.LongestHidden, "LONGEST HIDDEN")]
        [TestCase(HighlightType.MostStunned, "MOST STUNNED")]
        public void HighlightTitle_UsesReadableEnglishLabel(
            HighlightType type,
            string expected)
        {
            Assert.That(
                NetworkHighlightPlaybackController.TitleOf(type),
                Is.EqualTo(expected));
        }

        [Test]
        public void HighlightTitle_AddsRecordedActorNickname()
        {
            var candidate = new HighlightCandidate(
                HighlightType.FirstBlood,
                new[] { new HighlightSegment(0d, 10d) },
                "item",
                eventAt: 5d,
                score: 80d,
                actorPlayerIndex: 1);

            Assert.That(NetworkHighlightPlaybackController.TitleOf(
                    candidate,
                    new[]
                    {
                        new MatchParticipant("p1", 0),
                        new MatchParticipant("p2", 1),
                    },
                    new[]
                    {
                        new RoomParticipant("p1", 0, true, "방장"),
                        new RoomParticipant("p2", 1, false, "민수"),
                    }),
                Is.EqualTo("FIRST BLOOD · 민수"));
        }

        private static HighlightCandidate Candidate(HighlightType type)
        {
            return new HighlightCandidate(type, 0d, 10d, type.ToString());
        }
    }
}
