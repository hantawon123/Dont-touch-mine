using Game.Core.Items;
using Game.Server.Match;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class MatchOutcomeSystemTests
    {
        [Test]
        public void GetWinners_ReturnsEveryPlayerHoldingOwnItem()
        {
            var system = new MatchOutcomeSystem(CreateAssignments());

            system.TryHoldItem(0, "item-0");
            system.TryHoldItem(1, "item-2");
            system.TryHoldItem(5, "item-5");

            Assert.That(system.GetWinnerPlayerIndices(), Is.EqualTo(new[] { 0, 5 }));
        }

        [Test]
        public void DestroyItem_RemovesHolderAndDetectsAllItemsDestroyed()
        {
            var system = new MatchOutcomeSystem(CreateAssignments());
            system.TryHoldItem(0, "item-0");

            for (var playerIndex = 0; playerIndex < 6; playerIndex++)
            {
                Assert.That(system.DestroyItem($"item-{playerIndex}"), Is.True);
            }

            Assert.That(system.DestroyedItemCount, Is.EqualTo(6));
            Assert.That(system.AllPlayerItemsDestroyed, Is.True);
            Assert.That(system.GetWinnerPlayerIndices(), Is.Empty);
            Assert.That(system.DestroyItem("item-0"), Is.False);
        }

        [Test]
        public void TryHoldItem_PreventsTwoPlayersHoldingSameItem()
        {
            var system = new MatchOutcomeSystem(CreateAssignments());

            Assert.That(system.TryHoldItem(0, "item-1"), Is.True);
            Assert.That(system.TryHoldItem(2, "item-1"), Is.False);
        }

        private static PlayerItemAssignment[] CreateAssignments()
        {
            var assignments = new PlayerItemAssignment[6];
            for (var playerIndex = 0; playerIndex < assignments.Length; playerIndex++)
            {
                assignments[playerIndex] = new PlayerItemAssignment(
                    playerIndex,
                    new ItemDefinition($"item-{playerIndex}", "test"));
            }

            return assignments;
        }
    }
}
