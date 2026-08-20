using Game.Core.Match;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class MatchPhaseFlowTests
    {
        [TestCase(MatchPhase.Waiting, MatchPhase.Hiding)]
        [TestCase(MatchPhase.Hiding, MatchPhase.Searching)]
        [TestCase(MatchPhase.Searching, MatchPhase.Result)]
        public void TryGetNext_ReturnsDefinedNextPhase(MatchPhase current, MatchPhase expected)
        {
            var hasNext = MatchPhaseFlow.TryGetNext(current, out var next);

            Assert.That(hasNext, Is.True);
            Assert.That(next, Is.EqualTo(expected));
        }

        [Test]
        public void TryGetNext_ReturnsFalseForResult()
        {
            var hasNext = MatchPhaseFlow.TryGetNext(MatchPhase.Result, out var next);

            Assert.That(hasNext, Is.False);
            Assert.That(next, Is.EqualTo(MatchPhase.Result));
        }
    }
}
