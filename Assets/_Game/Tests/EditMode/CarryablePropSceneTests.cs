using System.Collections.Generic;
using Game.Client.Interactions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Architecture.Tests
{
    public sealed class CarryablePropSceneTests
    {
        private const string PlaygroundScenePath = "Assets/_Game/Content/Scenes/Playground.unity";
        private const int MinimumCarryablePropCount = 190;

        [Test]
        public void Playground_CarryablePropsAreReadyForInteraction()
        {
            var scene = SceneManager.GetSceneByPath(PlaygroundScenePath);
            var openedForTest = !scene.isLoaded;

            if (openedForTest)
            {
                scene = EditorSceneManager.OpenScene(PlaygroundScenePath, OpenSceneMode.Additive);
            }

            try
            {
                var carryableItems = CollectCarryableItems(scene);
                Assert.That(carryableItems, Has.Count.GreaterThanOrEqualTo(MinimumCarryablePropCount));

                foreach (var item in carryableItems)
                {
                    Assert.That(item.enabled, Is.True, $"{item.name}: CarryableItem이 비활성화되어 있습니다.");
                    Assert.That(item.DisplayName, Is.Not.Null.And.Not.Empty, $"{item.name}: 표시 이름이 없습니다.");

                    var body = item.GetComponent<Rigidbody>();
                    Assert.That(body, Is.Not.Null, $"{item.name}: Rigidbody가 없습니다.");
                    Assert.That(HasEnabledSolidCollider(item), Is.True,
                        $"{item.name}: 활성화된 비트리거 Collider가 없습니다.");

                    foreach (var meshCollider in item.GetComponentsInChildren<MeshCollider>(includeInactive: true))
                    {
                        Assert.That(meshCollider.convex, Is.True,
                            $"{item.name}: 동적 Rigidbody와 함께 사용할 MeshCollider는 Convex여야 합니다.");
                    }
                }
            }
            finally
            {
                if (openedForTest)
                {
                    EditorSceneManager.CloseScene(scene, removeScene: true);
                }
            }
        }

        private static List<CarryableItem> CollectCarryableItems(Scene scene)
        {
            var items = new List<CarryableItem>();

            foreach (var rootObject in scene.GetRootGameObjects())
            {
                items.AddRange(rootObject.GetComponentsInChildren<CarryableItem>(includeInactive: true));
            }

            return items;
        }

        private static bool HasEnabledSolidCollider(CarryableItem item)
        {
            foreach (var itemCollider in item.GetComponentsInChildren<Collider>(includeInactive: true))
            {
                if (itemCollider.enabled && !itemCollider.isTrigger)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
