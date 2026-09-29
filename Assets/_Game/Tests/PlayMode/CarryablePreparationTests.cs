using System.Reflection;
using Game.Client.Interactions;
using Game.Core.Items;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.PlayMode
{
    public class CarryablePreparationTests
    {
        [Test]
        public void RepeatedNamesKeepPrefixOrderAndUniqueFallbackIds()
        {
            var saved = ItemCatalog.Definitions;
            var a = new GameObject("prefix_long"); a.SetActive(false);
            var b = new GameObject("prefix_long"); b.SetActive(false);
            var c = new GameObject("unlisted_repeat"); c.SetActive(false);
            var d = new GameObject("unlisted_repeat"); d.SetActive(false);
            try
            {
                ItemCatalog.Configure(new[] { new ItemDefinition("prefix", "test", "first"), new ItemDefinition("prefix_long", "test", "second") });
                Assert.That(a.AddComponent<CarryableItem>().ObjectId, Is.EqualTo("prefix"));
                ItemCatalog.Configure(new[] { new ItemDefinition("prefix_long", "test", "new") });
                Assert.That(b.AddComponent<CarryableItem>().ObjectId, Is.EqualTo("prefix_long"));
                Assert.That(c.AddComponent<CarryableItem>().ObjectId, Is.Not.EqualTo(d.AddComponent<CarryableItem>().ObjectId));
            }
            finally
            {
                ItemCatalog.Configure(saved);
                Object.DestroyImmediate(a); Object.DestroyImmediate(b); Object.DestroyImmediate(c); Object.DestroyImmediate(d);
            }
        }

        [TestCase(1f)]
        [TestCase(2f)]
        public void PreparedBoundsAreUsedOnlyAtThePreparedScale(float scale)
        {
            var root = new GameObject("prepared item"); root.SetActive(false);
            root.transform.localScale = Vector3.one * scale;
            root.AddComponent<BoxCollider>();
            var item = root.AddComponent<CarryableItem>();
            var type = typeof(CarryableItem);
            void Set(string name, object value) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(item, value);
            try
            {
                Set("preparedForBuild", true); Set("preparedCenter", Vector3.zero);
                Set("preparedExtents", Vector3.one * .5f); Set("preparedScale", Vector3.one);
                item.UseObjectId("prepared_id");
                root.SetActive(true);
                Assert.That(item.ObjectId, Is.EqualTo("prepared_id"));
                Assert.That(item.PlacementCenterOffset, Is.EqualTo(Vector3.zero));
                Assert.That(item.PlacementHalfExtents, Is.EqualTo(Vector3.one * scale * .5f));
                Assert.That(item.enabled, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
