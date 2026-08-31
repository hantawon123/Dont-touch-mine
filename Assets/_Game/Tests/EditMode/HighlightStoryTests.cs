using System.Linq;
using Game.Core.Items;
using Game.Core.Match;
using Game.Server.Match;
using Game.Server.Items;
using Game.SOAP.Config;
using NUnit.Framework;
using UnityEngine;
using Game.Network.Session;

namespace Game.Tests.EditMode
{
    public sealed class HighlightStoryTests
    {
        [Test]
        public void ReplayTransfer_RoundTripsRecordedActions()
        {
            var segment = new HighlightSegment(10, 11);
            var frame = new HighlightReplayFrame(10, new[] { Pose.identity, Pose.identity },
                new WorldObjectState[0], new byte[] { 1, 2 });
            var data = new HighlightReplayData(new HighlightCandidate(HighlightType.MostStunned,
                new[] { segment }, "1"), new[] { new HighlightReplayClip(segment, new[] { frame }) });
            var bytes = HighlightReplaySerializer.Serialize(new[] { data });
            Assert.That(HighlightReplaySerializer.TryDeserialize(bytes, out var decoded), Is.True);
            Assert.That(decoded[0].Clips[0].Frames[0].PlayerActions, Is.EqualTo(new byte[] { 1, 2 }));
            bytes[4] = 1;
            Assert.That(HighlightReplaySerializer.TryDeserialize(bytes, out _), Is.False);
        }

        [Test]
        public void ReplayFrame_RejectsUnknownAction()
        {
            Assert.That(() => new HighlightReplayFrame(0, new[] { Pose.identity },
                new WorldObjectState[0], new byte[] { 3 }), Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }

        [Test]
        public void ReplaySegment_RejectsNonFiniteTimeline()
        {
            Assert.That(() => new HighlightSegment(double.NaN, 1), Throws.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => new HighlightSegment(0, double.PositiveInfinity), Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }

        [Test]
        public void Fade_DoesNotConsumeBody_AndHoldsLastFrame()
        {
            Assert.That(HighlightPresentationTiming.BodyTime(0.5, 10), Is.Zero);
            Assert.That(HighlightPresentationTiming.Opacity(0.55, 10), Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(HighlightPresentationTiming.BodyTime(10.7, 10), Is.EqualTo(10).Within(0.001));
            Assert.That(HighlightPresentationTiming.Opacity(11, 10), Is.Zero);
            Assert.That(HighlightPresentationTiming.Opacity(11.25, 10), Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(HighlightPresentationTiming.Opacity(11.6, 10), Is.EqualTo(1f));
        }

        [Test]
        public void FirstBlood_KeepsThreeSecondTail_AndIsNotRepeatedAsFinalMoment()
        {
            WithRecorder(recorder =>
            {
                recorder.RecordItemDestroyed(1, "a", 110);
                var candidates = recorder.CaptureCandidates(110);
                var first = candidates.Single(c => c.Type == HighlightType.FirstBlood);
                Assert.That(first.StartedAt, Is.EqualTo(103));
                Assert.That(first.EndedAt, Is.EqualTo(113));
                Assert.That(candidates.Any(c => c.Type == HighlightType.FinalMoment), Is.False);
            });
        }

        [Test]
        public void Journey_RequiresDifferentHolders_NotRepeatedInteractions()
        {
            WithRecorder(recorder =>
            {
                recorder.RecordItemPickup(0, "a", 102);
                recorder.RecordItemInteraction(1, "a", 104);
                recorder.RecordItemPickup(0, "a", 106);
                Assert.That(recorder.CaptureCandidates(120).Any(c => c.Type == HighlightType.TteTanMulgun), Is.False);
                recorder.RecordItemPickup(1, "a", 110);
                Assert.That(recorder.CaptureCandidates(120).Any(c => c.Type == HighlightType.TteTanMulgun), Is.True);
            });
        }

        [Test]
        public void Timeout_FinalMomentShowsSurvivingItem_NotAnArbitraryDrop()
        {
            WithRecorder(recorder =>
            {
                recorder.RecordItemInteraction(0, "a", 119);
                var final = recorder.CaptureCandidates(120, MatchEndReason.TimeExpired)
                    .Single(c => c.Type == HighlightType.FinalMoment);
                Assert.That(final.StartedAt, Is.EqualTo(113));
                Assert.That(final.EndedAt, Is.EqualTo(123));
            });
        }

        [Test]
        public void Hidden_UsesNormalSpeedAtEncounter_AndFastForEmptyTime()
        {
            WithRecorder(recorder =>
            {
                var frames = new[] { Frame(100, 20), Frame(120, 2), Frame(122, 20), Frame(140, 20) };
                var hidden = recorder.CaptureCandidates(140, null, frames)
                    .Single(c => c.Type == HighlightType.LongestHidden);
                Assert.That(hidden.StartedAt, Is.EqualTo(100));
                Assert.That(hidden.EndedAt, Is.EqualTo(140));
                Assert.That(hidden.PlaybackDurationSeconds, Is.LessThanOrEqualTo(10.001));
                Assert.That(hidden.Segments.Any(s => s.StartedAt <= 120 && s.EndedAt >= 120 && s.PlaybackSpeed == 1), Is.True);
                Assert.That(hidden.Segments.Any(s => s.PlaybackSpeed > 1), Is.True);
            });
        }

        [Test]
        public void StunMontage_DoesNotRepeatOverlappingTime()
        {
            WithRecorder(recorder =>
            {
                recorder.RecordPlayerStunned(1, 105);
                recorder.RecordPlayerStunned(1, 106);
                recorder.RecordPlayerStunned(1, 107);
                var stun = recorder.CaptureCandidates(120).Single(c => c.Type == HighlightType.MostStunned);
                for (var i = 1; i < stun.Segments.Count; i++)
                    Assert.That(stun.Segments[i].StartedAt, Is.GreaterThanOrEqualTo(stun.Segments[i - 1].EndedAt));
            });
        }

        private static HighlightReplayFrame Frame(double at, float distance) => new(at,
            new[] { new Pose(Vector3.one * 30, Quaternion.identity), new Pose(Vector3.right * distance, Quaternion.identity) },
            new[] { new WorldObjectState("a", Pose.identity) });

        private static void WithRecorder(System.Action<HighlightEventRecorder> test)
        {
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();
            try
            {
                var recorder = new HighlightEventRecorder(rules, new[]
                {
                    new PlayerItemAssignment(0, new ItemDefinition("a", "category")),
                    new PlayerItemAssignment(1, new ItemDefinition("b", "category"))
                });
                recorder.StartRecording(100);
                recorder.StartSearching(100);
                test(recorder);
            }
            finally { Object.DestroyImmediate(rules); }
        }
    }
}
