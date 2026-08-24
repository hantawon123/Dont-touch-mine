using System.Linq;
using Game.Core.Items;
using Game.Server.Match;
using Game.SOAP.Config;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class HighlightEventRecorderTests
    {
        private MatchRulesSO rules;
        private HighlightEventRecorder recorder;

        [SetUp]
        public void SetUp()
        {
            rules = ScriptableObject.CreateInstance<MatchRulesSO>();
            recorder = new HighlightEventRecorder(rules, new[]
            {
                Assignment(0, "item-a"),
                Assignment(1, "item-b"),
                Assignment(2, "item-c"),
                Assignment(3, "item-d")
            });
            recorder.StartSearching(100d);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(rules);
        }

        [Test]
        public void CaptureCandidates_RecordsFirstDestroyedItemOnly()
        {
            recorder.RecordItemDestroyed(1, "item-a", 110d);
            recorder.RecordItemDestroyed(2, "item-b", 120d);

            var candidate = Candidate(HighlightType.FirstBlood, 120d);

            Assert.That(candidate.TargetId, Is.EqualTo("item-a"));
            Assert.That(candidate.StartedAt, Is.EqualTo(103d));
            Assert.That(candidate.EndedAt, Is.EqualTo(113d));
        }

        [Test]
        public void CaptureCandidates_SelectsMostInteractedItemByDefinedTieBreakers()
        {
            recorder.RecordItemInteraction(0, "item-a", 101d);
            recorder.RecordItemInteraction(1, "item-a", 102d);
            recorder.RecordItemInteraction(0, "item-b", 103d);
            recorder.RecordItemInteraction(1, "item-b", 104d);
            recorder.RecordItemInteraction(1, "item-b", 105d);

            var candidate = Candidate(HighlightType.TteTanMulgun, 110d);

            Assert.That(candidate.TargetId, Is.EqualTo("item-b"));
            Assert.That(candidate.Segments, Has.Count.EqualTo(3));
            Assert.That(candidate.PlaybackDurationSeconds, Is.EqualTo(6d));
        }

        [Test]
        public void CaptureCandidates_ExcludesOldFinalMoment()
        {
            recorder.RecordPlayerStunned(2, 105d);

            var types = recorder.CaptureCandidates(120d).Select(candidate => candidate.Type);

            Assert.That(types.Any(type => type == HighlightType.FinalMoment), Is.False);
        }

        [Test]
        public void CaptureCandidates_SelectsLongestUndiscoveredItem()
        {
            recorder.RecordItemInteraction(1, "item-a", 110d);
            recorder.RecordItemInteraction(2, "item-b", 120d);
            recorder.RecordItemInteraction(3, "item-c", 130d);

            var candidate = Candidate(HighlightType.LongestHidden, 140d);

            Assert.That(candidate.TargetId, Is.EqualTo("item-d"));
            Assert.That(candidate.EndedAt, Is.EqualTo(140d));
            Assert.That(candidate.PlaybackDurationSeconds, Is.EqualTo(10d));
        }

        [Test]
        public void CaptureCandidates_SelectsMostStunnedUsingLatestStunAsTieBreaker()
        {
            recorder.RecordPlayerStunned(1, 105d);
            recorder.RecordPlayerStunned(2, 106d);

            var candidate = Candidate(HighlightType.MostStunned, 110d);

            Assert.That(candidate.TargetId, Is.EqualTo("2"));
            Assert.That(candidate.Segments, Has.Count.EqualTo(1));
            Assert.That(candidate.StartedAt, Is.EqualTo(104d));
            Assert.That(candidate.EndedAt, Is.EqualTo(108d));
        }

        [Test]
        public void CaptureCandidates_UsesUpToThreeStunSegmentsWithinTenSeconds()
        {
            recorder.RecordPlayerStunned(1, 104d);
            recorder.RecordPlayerStunned(1, 108d);
            recorder.RecordPlayerStunned(1, 112d);
            recorder.RecordPlayerStunned(1, 116d);

            var candidate = Candidate(HighlightType.MostStunned, 120d);

            Assert.That(candidate.Segments, Has.Count.EqualTo(3));
            Assert.That(candidate.PlaybackDurationSeconds, Is.EqualTo(10d).Within(0.001d));
        }

        private HighlightCandidate Candidate(HighlightType type, double endedAt)
        {
            return recorder.CaptureCandidates(endedAt).Single(candidate => candidate.Type == type);
        }

        private static PlayerItemAssignment Assignment(int playerIndex, string itemId)
        {
            return new PlayerItemAssignment(
                playerIndex,
                new ItemDefinition(itemId, "category"));
        }
    }
}
