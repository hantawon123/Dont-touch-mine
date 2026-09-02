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
            new("apple", "food"),
            new("bread", "food"),
            new("hammer", "tool"),
            new("wrench", "tool"),
            new("cup", "kitchen"),
            new("plate", "kitchen")
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
                Assert.That(assignments[playerIndex].Item.Category, Is.Not.Empty);
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
                Assert.That(ItemCatalog.Definitions, Does.Contain(assignment.Item));
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
        public void SelectionRules_ListOnlyAvailableItemsInAssignedCategory()
        {
            var available = ItemSelectionRules.AvailableItems(
                Definitions,
                "toy",
                new[] { "bear" });

            Assert.That(available, Has.Count.EqualTo(1));
            Assert.That(available[0].ItemId, Is.EqualTo("ball"));
        }

        [Test]
        public void SelectionRules_ResolveValidSelection()
        {
            Assert.That(
                ItemSelectionRules.TryResolveSelection(
                    Definitions,
                    "toy",
                    "ball",
                    Array.Empty<string>(),
                    out var selected,
                    out var failure),
                Is.True);
            Assert.That(selected.ItemId, Is.EqualTo("ball"));
            Assert.That(failure, Is.EqualTo(ItemSelectionFailure.None));
        }

        [TestCase(null, "ball", null, ItemSelectionFailure.MissingCategory)]
        [TestCase("toy", "missing", null, ItemSelectionFailure.UnknownItem)]
        [TestCase("food", "ball", null, ItemSelectionFailure.WrongCategory)]
        [TestCase("toy", "ball", "ball", ItemSelectionFailure.AlreadySelected)]
        public void SelectionRules_RejectInvalidSelection(
            string category,
            string itemId,
            string selectedItemId,
            ItemSelectionFailure expected)
        {
            var selectedItemIds = selectedItemId == null
                ? Array.Empty<string>()
                : new[] { selectedItemId };

            Assert.That(
                ItemSelectionRules.TryResolveSelection(
                    Definitions,
                    category,
                    itemId,
                    selectedItemIds,
                    out _,
                    out var failure),
                Is.False);
            Assert.That(failure, Is.EqualTo(expected));
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
