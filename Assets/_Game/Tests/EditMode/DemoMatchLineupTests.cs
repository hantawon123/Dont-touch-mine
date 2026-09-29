using System.Linq;
using Game.Core.Items;
using Game.Core.Match;
using Game.SOAP.Config;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class DemoMatchLineupTests
    {
        [Test]
        public void ReverseJoinOrder_LastJoinerHidesFirstAndKeepsAccountPairs()
        {
            var ids = new[] { "first", "second", "third" };
            var users = new[] { "first-account", "", "third-account" };

            DemoMatchLineup.ReverseJoinOrder(ids, users);

            Assert.That(ids, Is.EqualTo(new[] { "third", "second", "first" }));
            Assert.That(users, Is.EqualTo(new[] { "third-account", "", "first-account" }));
        }

        [Test]
        public void TryAssign_SixPlayers_GivesEachJoinOrderItsItem()
        {
            Assert.That(DemoMatchLineup.TryAssign(FixedDefinitions(), 6, out var assignments), Is.True);

            Assert.That(assignments, Has.Length.EqualTo(6));
            for (var playerIndex = 0; playerIndex < 6; playerIndex++)
            {
                Assert.That(assignments[playerIndex].PlayerIndex, Is.EqualTo(playerIndex));
                Assert.That(
                    assignments[playerIndex].Item.ItemId,
                    Is.EqualTo(DemoMatchLineup.ItemIdsByJoinOrder[5 - playerIndex]));
            }
        }

        [Test]
        public void TryAssign_FewerPlayers_KeepsItemsByJoinOrder()
        {
            Assert.That(DemoMatchLineup.TryAssign(FixedDefinitions(), 3, out var assignments), Is.True);

            // 세 명이면 세 번째로 들어온 사람이 먼저 숨기고, 1~3번째 물건만 쓰인다.
            Assert.That(
                assignments.Select(a => a.Item.ItemId),
                Is.EqualTo(new[]
                {
                    DemoMatchLineup.ItemIdsByJoinOrder[2],
                    DemoMatchLineup.ItemIdsByJoinOrder[1],
                    DemoMatchLineup.ItemIdsByJoinOrder[0]
                }));
        }

        [Test]
        public void TryAssign_MissingItem_ReturnsFalse()
        {
            var definitions = FixedDefinitions().Skip(1).ToArray();

            Assert.That(DemoMatchLineup.TryAssign(definitions, 6, out var assignments), Is.False);
            Assert.That(assignments, Is.Null);
        }

        [Test]
        public void FixedItems_AreDistinctEnabledHalloweenCatalogItems()
        {
            ItemCatalogSO.Load();
            var halloween = ItemCatalog.DefinitionsInCategory("halloween").Select(d => d.ItemId).ToArray();

            Assert.That(DemoMatchLineup.ItemIdsByJoinOrder, Is.Unique);
            Assert.That(halloween, Is.SupersetOf(DemoMatchLineup.ItemIdsByJoinOrder));
            Assert.That(
                DemoMatchLineup.TryAssign(ItemCatalog.AssignmentDefinitions, 6, out _),
                Is.True);
        }

        private static ItemDefinition[] FixedDefinitions() =>
            DemoMatchLineup.ItemIdsByJoinOrder
                .Select(id => new ItemDefinition(id, "halloween"))
                .Append(new ItemDefinition("unrelated", "halloween"))
                .ToArray();
    }
}
