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

        private static HighlightCandidate Candidate(
            HighlightType type,
            string targetId = "target")
        {
            return new HighlightCandidate(type, 10d, 20d, targetId);
        }
    }
}
