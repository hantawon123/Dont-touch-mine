using System;
using System.Linq;
using System.Reflection;
using Game.Core.Items;
using Game.Editor;
using Game.SOAP.Config;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode
{
    public sealed class ItemCatalogMetadataTests
    {
        [Test]
        public void EditorStartupStillRejectsMissingAuthoredPrefab()
        {
            var source = ItemCatalogSO.Load();
            var item = source.categories[0].items[0];
            var prefab = item.prefab;
            try
            {
                item.prefab = null;
                var initialize = typeof(ItemCatalogSO).GetMethod("Initialize", BindingFlags.NonPublic | BindingFlags.Static);
                var error = Assert.Throws<TargetInvocationException>(() => initialize.Invoke(null, null));
                Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
            }
            finally { item.prefab = prefab; source.Apply(); }
        }

        [Test]
        public void GeneratedMetadataMatchesDefinitionsWithoutPrefabDependencies()
        {
            var source = ItemCatalogSO.Load();
            var expected = ItemCatalog.Definitions.ToArray();
            var metadata = ItemCatalogMetadataBuildPreparation.Prepare();
            try
            {
                Assert.That(metadata.categories.Select(c => (c.id, c.label, c.enabled)),
                    Is.EqualTo(source.categories.Select(c => (c.id, c.label, c.enabled))));
                Assert.That(metadata.categories.SelectMany(c => c.items).Select(i => (i.id, i.displayName, i.enabled)),
                    Is.EqualTo(source.categories.SelectMany(c => c.items).Select(i => (i.id, i.displayName, i.enabled))));
                Assert.That(metadata.categories.SelectMany(c => c.items).All(i => i.prefab == null), Is.True);
                Assert.That(AssetDatabase.GetDependencies(ItemCatalogMetadataBuildPreparation.Path)
                    .Any(p => p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)), Is.False);
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                typeof(ItemCatalogSO).GetMethod("Validate", flags, null, new[] {typeof(bool)}, null)
                    .Invoke(metadata, new object[] {false});
                typeof(ItemCatalogSO).GetMethod("ConfigureDefinitions", flags).Invoke(metadata, null);
                Assert.That(ItemCatalog.Definitions, Is.EqualTo(expected));
                Assert.Throws<InvalidOperationException>(() => metadata.Validate());
                // A stale generated file is overwritten on every build preparation.
                metadata.categories[0].label = "stale";
                metadata.categories[0].items[0].prefab = source.categories[0].items[0].prefab;
                Assert.That(ItemCatalogMetadataBuildPreparation.Prepare(), Is.SameAs(metadata));
                Assert.That(metadata.categories[0].label, Is.EqualTo(source.categories[0].label));
                Assert.That(metadata.categories[0].items[0].prefab, Is.Null);
            }
            finally { source.Apply(); }
        }
    }
}
