using System.Linq;
using Game.Core.Match;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class EndingStageLayoutTests
    {
        private static MatchParticipant[] Six() => Enumerable.Range(0, 6)
            .Select(i => new MatchParticipant($"P{i}", i)).ToArray();

        [Test]
        public void WinnersTakeEscapeSlots_LosersTakeArrestSlots_InPlayerIndexOrder()
        {
            var placements = EndingStageLayout.Assign(Six(), new[] { 4, 1 }, 6, 6);

            var escaped = placements.Where(p => p.Escaped).OrderBy(p => p.Slot).ToArray();
            var arrested = placements.Where(p => !p.Escaped).OrderBy(p => p.Slot).ToArray();
            Assert.That(escaped.Select(p => p.PlayerId), Is.EqualTo(new[] { "P1", "P4" }));
            Assert.That(escaped.Select(p => p.Slot), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(arrested.Select(p => p.PlayerId), Is.EqualTo(new[] { "P0", "P2", "P3", "P5" }));
            Assert.That(arrested.Select(p => p.Slot), Is.EqualTo(new[] { 0, 1, 2, 3 }));
        }

        [Test]
        public void NoWinners_EveryoneIsArrested()
        {
            var placements = EndingStageLayout.Assign(Six(), null, 6, 6);
            Assert.That(placements.All(p => !p.Escaped));
            Assert.That(placements.Select(p => p.Slot), Is.EquivalentTo(Enumerable.Range(0, 6)));
        }

        [Test]
        public void EveryoneWins_AllEscape()
        {
            var placements = EndingStageLayout.Assign(Six(), new[] { 0, 1, 2, 3, 4, 5 }, 6, 6);
            Assert.That(placements.All(p => p.Escaped));
        }

        [Test]
        public void MoreParticipantsThanSlots_ReuseLastSlot()
        {
            var placements = EndingStageLayout.Assign(Six(), null, 6, 2);
            Assert.That(placements.Max(p => p.Slot), Is.EqualTo(1));
            Assert.That(placements.Count(p => p.Slot == 1), Is.EqualTo(5));
        }

        [Test]
        public void UnsortedParticipants_AreOrderedByPlayerIndex()
        {
            var shuffled = new[] { new MatchParticipant("B", 3), new MatchParticipant("A", 0), new MatchParticipant("C", 5) };
            var placements = EndingStageLayout.Assign(shuffled, null, 6, 6);
            Assert.That(placements.Select(p => p.PlayerId), Is.EqualTo(new[] { "A", "B", "C" }));
            Assert.That(placements.Select(p => p.Slot), Is.EqualTo(new[] { 0, 1, 2 }));
        }
    }
}
