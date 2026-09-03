using Game.Server.Match;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class HighlightCandidateSelectorTests
    {
        [Test]
        public void Select_UsesFixedPriorityAndLimitsResultToThree()
        {
            var selected = HighlightCandidateSelector.Select(new[]
            {
                Candidate(HighlightType.MostStunned),
                Candidate(HighlightType.FinalMoment),
                Candidate(HighlightType.LongestHidden),
                Candidate(HighlightType.FirstBlood),
                Candidate(HighlightType.TteTanMulgun)
            });

            Assert.That(selected, Has.Length.EqualTo(3));
            Assert.That(selected[0].Type, Is.EqualTo(HighlightType.FirstBlood));
            Assert.That(selected[1].Type, Is.EqualTo(HighlightType.TteTanMulgun));
            Assert.That(selected[2].Type, Is.EqualTo(HighlightType.FinalMoment));
        }

        [Test]
        public void Select_FillsMissingPrimaryHighlightsWithFallbacks()
        {
            var selected = HighlightCandidateSelector.Select(new[]
            {
                Candidate(HighlightType.MostStunned),
                Candidate(HighlightType.FinalMoment),
                Candidate(HighlightType.LongestHidden)
            });

            Assert.That(selected, Has.Length.EqualTo(3));
            Assert.That(selected[0].Type, Is.EqualTo(HighlightType.FinalMoment));
            Assert.That(selected[1].Type, Is.EqualTo(HighlightType.LongestHidden));
            Assert.That(selected[2].Type, Is.EqualTo(HighlightType.MostStunned));
        }

        [Test]
        public void Select_KeepsOnlyOneCandidatePerType()
        {
            var selected = HighlightCandidateSelector.Select(new[]
            {
                Candidate(HighlightType.FirstBlood, "first"),
                Candidate(HighlightType.FirstBlood, "duplicate")
            });

            Assert.That(selected, Has.Length.EqualTo(1));
            Assert.That(selected[0].TargetId, Is.EqualTo("first"));
        }

        [Test]
        public void Select_RanksScoredCandidatesAheadOfFixedCategoryOrder()
        {
            var selected = HighlightCandidateSelector.Select(new[]
            {
                Candidate(HighlightType.FirstBlood, "first", 60d),
                Candidate(HighlightType.LongestHidden, "hidden", 95d),
                Candidate(HighlightType.MostStunned, "stunned", 75d),
                Candidate(HighlightType.FinalMoment, "final", 90d),
            });

            Assert.That(selected, Has.Length.EqualTo(3));
            Assert.That(selected[0].TargetId, Is.EqualTo("hidden"));
            Assert.That(selected[1].TargetId, Is.EqualTo("final"));
            Assert.That(selected[2].TargetId, Is.EqualTo("stunned"));
        }

        [Test]
        public void Select_ExcludesCandidatesWithNoHighlightScore()
        {
            var selected = HighlightCandidateSelector.Select(new[]
            {
                Candidate(HighlightType.LongestHidden, "empty", 0d),
                Candidate(HighlightType.FirstBlood, "meaningful", 60d),
            });

            Assert.That(selected, Has.Length.EqualTo(1));
            Assert.That(selected[0].TargetId, Is.EqualTo("meaningful"));
        }

        [Test]
        public void Select_KeepsOnlyTheSegmentClosestToEachHighlightEvent()
        {
            var selected = HighlightCandidateSelector.Select(new[]
            {
                new HighlightCandidate(
                    HighlightType.LongestHidden,
                    new[]
                    {
                        new HighlightSegment(10d, 12d),
                        new HighlightSegment(20d, 22d),
                        new HighlightSegment(30d, 32d),
                    },
                    "hidden",
                    eventAt: 21d,
                    score: 90d),
            });

            Assert.That(selected, Has.Length.EqualTo(1));
            Assert.That(selected[0].Segments, Has.Count.EqualTo(1));
            Assert.That(selected[0].Segments[0].StartedAt, Is.EqualTo(20d));
            Assert.That(selected[0].Segments[0].EndedAt, Is.EqualTo(22d));
        }

        private static HighlightCandidate Candidate(
            HighlightType type,
            string targetId = "target",
            double? score = null)
        {
            return score.HasValue
                ? new HighlightCandidate(
                    type,
                    new[] { new HighlightSegment(10d, 20d) },
                    targetId,
                    18d,
                    score.Value)
                : new HighlightCandidate(type, 10d, 20d, targetId);
        }
    }
}
