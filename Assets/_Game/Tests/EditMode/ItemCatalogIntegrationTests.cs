using System;
using System.Linq;
using Game.Client.Lobby;
using Game.Core.Items;
using Game.SOAP.Config;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class ItemCatalogIntegrationTests
    {
        [Test]
        public void EveryPlayableCategory_AssignsDistinctRealItems_ForEveryPlayerCount()
        {
            var catalog = ItemCatalogSO.Load();
            foreach (var category in catalog.categories.Where(c => c.enabled))
            for (var count = 1; count <= MatchRulesSO.MaxPlayerCount; count++)
            for (var seed = 0; seed < 50; seed++)
            {
                var assigned = ItemAssignmentSystem.Assign(ItemCatalog.Definitions, count, new System.Random(seed), category.id);
                Assert.That(assigned.All(a => a.Item.Category == category.id), Is.True);
                Assert.That(assigned.Select(a => a.Item.ItemId).Distinct().Count(), Is.EqualTo(count));
                Assert.That(assigned.All(a => category.items.Any(i => i.enabled && i.id == a.Item.ItemId && i.prefab != null)), Is.True);
            }
        }

        [Test]
        public void PrefabOf_ReturnsTheCatalogPrefab()
        {
            var catalog = ItemCatalogSO.Load();
            var item = catalog.categories.First(c => c.enabled).items.First(i => i.enabled);
            Assert.That(catalog.PrefabOf(item.id), Is.SameAs(item.prefab));
            Assert.That(catalog.PrefabOf("missing_item"), Is.Null);
            Assert.That(catalog.PrefabOf("  "), Is.Null);
        }

        [Test]
        public void LabelOf_ReturnsCatalogLabelAndFallsBackToId()
        {
            var catalog = ItemCatalogSO.Load();
            var category = catalog.categories.First(c => c.enabled);
            Assert.That(
                Game.Client.Lobby.PlaySettingsCategoryCatalog.LabelOf(category.id),
                Is.EqualTo(category.label));
            Assert.That(
                Game.Client.Lobby.PlaySettingsCategoryCatalog.LabelOf(string.Empty),
                Is.EqualTo("랜덤"));
            Assert.That(
                Game.Client.Lobby.PlaySettingsCategoryCatalog.LabelOf("missing_category"),
                Is.EqualTo("missing_category"));
        }

        [Test]
        public void Random_ChoosesOneCategory_AndCanReachEveryEnabledCategory()
        {
            ItemCatalogSO.Load();
            var seen = new System.Collections.Generic.HashSet<string>();
            for (var seed = 0; seed < 300; seed++)
            {
                var assigned = ItemAssignmentSystem.Assign(6, new System.Random(seed));
                Assert.That(assigned.Select(a => a.Item.Category).Distinct().Count(), Is.EqualTo(1));
                Assert.That(assigned.Select(a => a.Item.ItemId).Distinct().Count(), Is.EqualTo(6));
                seen.Add(assigned[0].Item.Category);
            }
            Assert.That(seen, Is.EquivalentTo(ItemCatalog.Categories));
            Assert.That(seen, Does.Not.Contain("reserve"));
        }

        [Test]
        public void AddingCategoryAndItems_UpdatesPickerAndAssignments_WithoutCodeChanges()
        {
            var catalog = ItemCatalogSO.Load();
            var category = new ItemCatalogSO.Category { id = "test_extra", label = "추가 분류" };
            for (var i = 0; i < 6; i++) category.items.Add(new ItemCatalogSO.Item
                { id = "test_item_" + i, displayName = "새 물건 " + i, prefab = catalog.categories[0].items[i].prefab });
            try
            {
                catalog.categories.Add(category);
                Assert.That(PlaySettingsCategoryCatalog.All.Any(o => o.Id == category.id && o.Label == category.label), Is.True);
                var assigned = ItemAssignmentSystem.Assign(ItemCatalog.Definitions, 6, new System.Random(42), category.id);
                Assert.That(assigned.Select(a => a.Item.ItemId), Is.EquivalentTo(category.items.Select(i => i.id)));
                category.enabled = false;
                Assert.That(PlaySettingsCategoryCatalog.Contains(category.id), Is.False);
                Assert.That(ItemCatalog.Categories, Does.Not.Contain(category.id));
            }
            finally { catalog.categories.Remove(category); catalog.Apply(); }
        }

        [Test]
        public void InvalidCatalog_RejectsDuplicateIdsMissingPrefabsAndInsufficientCategory()
        {
            var source = ItemCatalogSO.Load();
            var copy = UnityEngine.Object.Instantiate(source);
            try
            {
                var category = copy.categories.First(c => c.enabled);
                var item = category.items[0];
                var id = item.id;
                item.id = category.items[1].id;
                Assert.Throws<InvalidOperationException>(() => copy.Validate());
                item.id = id;
                var prefab = item.prefab; item.prefab = null;
                Assert.Throws<InvalidOperationException>(() => copy.Validate());
                item.prefab = prefab;
                category.items.RemoveRange(5, category.items.Count - 5);
                Assert.Throws<InvalidOperationException>(() => copy.Validate());
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); source.Apply(); }
        }
    }
}
