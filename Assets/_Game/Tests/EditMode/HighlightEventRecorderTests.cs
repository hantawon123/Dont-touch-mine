using System.Linq;
using Game.Core.Items;
using Game.Server.Items;
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
            Assert.That(candidate.ActorPlayerIndex, Is.EqualTo(1));
            Assert.That(candidate.EventAt, Is.EqualTo(110d));
            Assert.That(candidate.Score, Is.EqualTo(60d));
            Assert.That(candidate.StartedAt, Is.EqualTo(103d));
            Assert.That(candidate.EndedAt, Is.EqualTo(113d));
        }

        [Test]
        public void CaptureCandidates_SelectsMostInteractedItemByDefinedTieBreakers()
        {
            recorder.RecordItemPickup(0, "item-a", 101d);
            recorder.RecordItemPickup(1, "item-a", 102d);
            recorder.RecordItemPickup(0, "item-b", 103d);
            recorder.RecordItemPickup(1, "item-b", 104d);
            recorder.RecordItemPickup(1, "item-b", 105d);

            var candidate = Candidate(HighlightType.TteTanMulgun, 110d);

            Assert.That(candidate.TargetId, Is.EqualTo("item-b"));
            Assert.That(candidate.ActorPlayerIndex, Is.EqualTo(1));
            Assert.That(candidate.SecondaryPlayerIndex, Is.EqualTo(-1));
            Assert.That(candidate.Segments, Has.Count.EqualTo(2));
            Assert.That(candidate.PlaybackDurationSeconds, Is.EqualTo(4d));
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
            Assert.That(candidate.Score, Is.EqualTo(70d));
            Assert.That(candidate.PlaybackDurationSeconds, Is.EqualTo(6d));
            Assert.That(candidate.Segments.All(segment => segment.PlaybackSpeed == 1d), Is.True);
        }

        [Test]
        public void CaptureCandidates_SelectsLongestHiddenOnlyFromSurvivingItems()
        {
            recorder.RecordItemDestroyed(0, "item-a", 140d);
            recorder.RecordItemInteraction(0, "item-b", 110d);
            recorder.RecordItemInteraction(0, "item-c", 120d);
            recorder.RecordItemInteraction(0, "item-d", 130d);

            var candidate = Candidate(HighlightType.LongestHidden, 140d);

            Assert.That(candidate.TargetId, Is.EqualTo("item-d"));
        }

        [Test]
        public void CaptureCandidates_SelectsMostStunnedUsingLatestStunAsTieBreaker()
        {
            recorder.RecordPlayerStunned(0, 1, 105d);
            recorder.RecordPlayerStunned(3, 2, 106d);

            var candidate = Candidate(HighlightType.MostStunned, 110d);

            Assert.That(candidate.TargetId, Is.EqualTo("2"));
            Assert.That(candidate.ActorPlayerIndex, Is.EqualTo(3));
            Assert.That(candidate.SecondaryPlayerIndex, Is.EqualTo(-1));
            Assert.That(candidate.EventAt, Is.EqualTo(106d));
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

        [Test]
        public void RecordPlayerStunned_RejectsIndexOutsideActivePlayers()
        {
            Assert.That(
                () => recorder.RecordPlayerStunned(4, 101d),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }

        [Test]
        public void CaptureCandidates_RanksMeaningfulLateEventAboveRoutineFirstBlood()
        {
            recorder.RecordItemDestroyed(1, "item-a", 101d);
            recorder.RecordPlayerStunned(2, 3, 119d);

            var selected = HighlightCandidateSelector.Select(recorder.CaptureCandidates(120d));

            Assert.That(selected[0].Type, Is.EqualTo(HighlightType.FinalMoment));
            Assert.That(selected[0].ActorPlayerIndex, Is.EqualTo(2));
        }

        [Test]
        public void CaptureCandidates_KeepsFirstBloodAheadOfSimultaneousFinalDestruction()
        {
            var onlyDestroyedItems = new HighlightEventRecorder(rules, new[]
            {
                Assignment(0, "item-a"),
                Assignment(1, "item-b"),
            });
            onlyDestroyedItems.StartSearching(100d);
            onlyDestroyedItems.RecordItemDestroyed(1, "item-a", 110d);
            onlyDestroyedItems.RecordItemDestroyed(0, "item-b", 110d);

            var selected = HighlightCandidateSelector.Select(
                onlyDestroyedItems.CaptureCandidates(110d));

            Assert.That(selected[0].Type, Is.EqualTo(HighlightType.FirstBlood));
            Assert.That(selected.Any(candidate => candidate.Type == HighlightType.FinalMoment), Is.True);
        }

        [Test]
        public void CaptureCandidates_DoesNotCreateLongestHiddenForDestroyedSoloItem()
        {
            var solo = new HighlightEventRecorder(rules, new[]
            {
                Assignment(0, "solo-item")
            });
            solo.StartRecording(100d);
            solo.RecordItemPickup(0, "solo-item", 100d);
            solo.StartSearching(130d);
            solo.RecordItemDestroyed(0, "solo-item", 160d);

            var candidates = solo.CaptureCandidates(
                160d,
                MatchEndReason.AllPlayerItemsDestroyed,
                new[]
                {
                    new HighlightReplayFrame(
                        130d,
                        new[] { Pose.identity },
                        new[] { new WorldObjectState("solo-item", Pose.identity) })
                });

            var hotItem = candidates.Single(candidate =>
                candidate.Type == HighlightType.TteTanMulgun);
            Assert.That(candidates.Any(candidate =>
                candidate.Type == HighlightType.FirstBlood), Is.True);
            Assert.That(hotItem.ActorPlayerIndex, Is.Zero);
            Assert.That(hotItem.Score, Is.GreaterThan(0d));
            Assert.That(hotItem.EventAt, Is.LessThanOrEqualTo(hotItem.EndedAt));
            Assert.That(candidates.Any(candidate =>
                candidate.Type == HighlightType.LongestHidden), Is.False);
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
