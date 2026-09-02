using System;
using System.Collections.Generic;
using Game.Core.Items;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class ItemAssignmentSystemTests
    {
        private static readonly ItemDefinition[] Definitions =
        {
            new("bear", "toy"),
            new("ball", "toy"),
            new("block", "toy"),
            new("doll", "toy"),
            new("car", "toy"),
            new("kite", "toy"),
            new("apple", "food"),
            new("bread", "food")
        };

        [Test]
        public void Assign_GivesSixPlayersUniqueItems()
        {
            var assignments = ItemAssignmentSystem.Assign(
                Definitions,
                6,
                new Random(1234));
            var assignedItemIds = new HashSet<string>();

            Assert.That(assignments, Has.Length.EqualTo(6));
            for (var playerIndex = 0; playerIndex < assignments.Length; playerIndex++)
            {
                Assert.That(assignments[playerIndex].PlayerIndex, Is.EqualTo(playerIndex));
                Assert.That(assignedItemIds.Add(assignments[playerIndex].Item.ItemId), Is.True);
                Assert.That(assignments[playerIndex].Item.Category, Is.EqualTo("toy"));
            }
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void Assign_FromMvpCatalogMatchesPlayerCount(int playerCount)
        {
            var assignments = ItemAssignmentSystem.Assign(
                playerCount,
                new Random(1200 + playerCount));
            var assignedItemIds = new HashSet<string>();

            Assert.That(assignments, Has.Length.EqualTo(playerCount));
            foreach (var assignment in assignments)
            {
                Assert.That(assignedItemIds.Add(assignment.Item.ItemId), Is.True);
                Assert.That(ItemCatalog.AssignmentDefinitions, Does.Contain(assignment.Item));
            }
        }

        [Test]
        public void Assign_WithSameSeedReturnsSameAssignments()
        {
            var first = ItemAssignmentSystem.Assign(Definitions, 6, new Random(42));
            var second = ItemAssignmentSystem.Assign(Definitions, 6, new Random(42));

            for (var playerIndex = 0; playerIndex < first.Length; playerIndex++)
            {
                Assert.That(
                    first[playerIndex].Item.ItemId,
                    Is.EqualTo(second[playerIndex].Item.ItemId));
            }
        }

        [Test]
        public void Assign_UsesConfiguredCategory()
        {
            var assignments = ItemAssignmentSystem.Assign(
                Definitions,
                2,
                new Random(42),
                "food");

            Assert.That(assignments, Has.Length.EqualTo(2));
            Assert.That(assignments[0].Item.Category, Is.EqualTo("food"));
            Assert.That(assignments[1].Item.Category, Is.EqualTo("food"));
        }

        [Test]
        public void AssignedCatalogItemKeepsSourceDisplayName()
        {
            var assigned = ItemCatalog.AssignedDefinition(0);

            Assert.That(assigned.ItemId, Is.EqualTo("Assigned_0"));
            Assert.That(
                ItemCatalog.DisplayNameOf(assigned.ItemId),
                Is.EqualTo(ItemCatalog.Definitions[0].DisplayName));
        }

        [Test]
        public void Catalog_ExposesItemsGroupedByActualCategory()
        {
            Assert.That(
                ItemCatalog.Categories,
                Is.EqualTo(new[] { "food", "tableware", "decoration", "kitchen" }));

            var food = ItemCatalog.DefinitionsInCategory("food");

            Assert.That(food, Has.Count.EqualTo(3));
            foreach (var item in food)
            {
                Assert.That(item.Category, Is.EqualTo("food"));
            }

            Assert.That(food[0].ItemId, Is.EqualTo("Soda_01"));
        }

        [Test]
        public void Catalog_ProvidesSixAssignmentCopiesPerCategory()
        {
            foreach (var category in ItemCatalog.Categories)
            {
                var count = 0;
                foreach (var assignment in ItemCatalog.AssignmentDefinitions)
                {
                    if (assignment.Category == category)
                    {
                        count++;
                    }
                }

                Assert.That(count, Is.EqualTo(6), category);
            }
        }

        [Test]
        public void Assign_RejectsInsufficientItems()
        {
            var definitions = new[]
            {
                new ItemDefinition("bear", "toy")
            };

            Assert.Throws<InvalidOperationException>(
                () => ItemAssignmentSystem.Assign(definitions, 6, new Random(1)));
        }

        [Test]
        public void Assign_RejectsDuplicateItemIds()
        {
            var definitions = new[]
            {
                new ItemDefinition("bear", "toy"),
                new ItemDefinition("bear", "decoration"),
                new ItemDefinition("apple", "food"),
                new ItemDefinition("bread", "food"),
                new ItemDefinition("hammer", "tool"),
                new ItemDefinition("wrench", "tool")
            };

            Assert.Throws<ArgumentException>(
                () => ItemAssignmentSystem.Assign(definitions, 6, new Random(1)));
        }
    }
}
