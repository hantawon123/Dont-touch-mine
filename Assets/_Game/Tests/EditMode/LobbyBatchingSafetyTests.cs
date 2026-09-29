using Game.Client.Interactions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public class LobbyBatchingSafetyTests
    {
        [Test]
        public void BatchedLobbyGeometry_ExcludesMovableHierarchiesAndTransparentMaterials()
        {
            var root = PrefabUtility.LoadPrefabContents(
                "Assets/_Game/Content/Prefabs/LobbyBasementEnvironment.prefab");
            try
            {
                var batchedCount = 0;
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if ((GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) &
                         StaticEditorFlags.BatchingStatic) == 0) continue;
                    batchedCount++;
                    for (var node = renderer.transform; node != null; node = node.parent)
                    {
                        Assert.IsNull(node.GetComponent<CarryableItem>(), renderer.name);
                        Assert.IsNull(node.GetComponent<Rigidbody>(), renderer.name);
                        Assert.IsNull(node.GetComponent<Animator>(), renderer.name);
                        Assert.IsNull(node.GetComponent<Animation>(), renderer.name);
                        Assert.IsEmpty(node.GetComponents<MonoBehaviour>(), renderer.name);
                        if (node == root.transform) break;
                    }
                    foreach (var material in renderer.sharedMaterials)
                    {
                        Assert.IsNotNull(material, renderer.name);
                        Assert.LessOrEqual(material.renderQueue, 2500, renderer.name);
                    }
                }
                Assert.Greater(batchedCount, 0, "Fixed lobby background must be batched.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
