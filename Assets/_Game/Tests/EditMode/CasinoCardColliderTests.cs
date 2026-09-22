using Game.Client.Interactions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class CasinoCardColliderTests
    {
        private static readonly string[] CardPrefabs =
        {
            "Assets/_Game/Content/ItemCollection/Prefabs/casino/casino_139e62c73a.prefab",
            "Assets/_Game/Content/ItemCollection/Prefabs/casino/casino_8e0aea0741.prefab",
            "Assets/_Game/Content/ItemCollection/Prefabs/casino/casino_3e3d1601c4.prefab",
            "Assets/_Game/Content/ItemCollection/Prefabs/casino/casino_b3d60e48ce.prefab",
        };

        [Test]
        public void Cards_HaveBoxColliderThickEnoughToRestOnFloor()
        {
            foreach (var path in CardPrefabs)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                Assert.That(prefab.GetComponent<CarryableItem>(), Is.Not.Null, path);

                var box = prefab.GetComponent<BoxCollider>();
                Assert.That(box, Is.Not.Null, path);
                Assert.That(box.enabled, Is.True, path);
                Assert.That(box.size.x, Is.GreaterThanOrEqualTo(0.02f), path);
                Assert.That(box.size.y, Is.GreaterThanOrEqualTo(0.02f), path);
                Assert.That(box.size.z, Is.GreaterThanOrEqualTo(0.02f), path);
                Assert.That(box.center.y - box.size.y * 0.5f, Is.GreaterThanOrEqualTo(-0.002f), path);
            }
        }
    }
}
