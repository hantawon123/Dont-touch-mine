using System;
using System.Collections.Generic;
using Game.Bootstrap;
using Game.Client.Interactions;
using Game.Core.Items;
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

        [Test]
        public void Playground_ContainsEveryMvpItemCatalogEntry()
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

                foreach (var definition in ItemCatalog.Definitions)
                {
                    Assert.That(
                        carryableItems.Exists(item =>
                            item.name.StartsWith(definition.ItemId, StringComparison.Ordinal)),
                        Is.True,
                        $"{definition.ItemId}: Playground에 대응하는 CarryableItem이 없습니다.");
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

        [Test]
        public void Playground_CapturesNetworkMatchConfiguration()
        {
            var scene = SceneManager.GetSceneByPath(PlaygroundScenePath);
            var openedForTest = !scene.isLoaded;

            if (openedForTest)
            {
                scene = EditorSceneManager.OpenScene(PlaygroundScenePath, OpenSceneMode.Additive);
            }

            try
            {
                PlaygroundMatchScene captured = null;
                Assert.DoesNotThrow(() => captured = PlaygroundMatchScene.Capture(scene));

                var carryableItems = CollectCarryableItems(scene);
                var carryableCount = carryableItems.Count;
                Assert.That(
                    captured.NetworkConfiguration.InitialWorldObjects.Count +
                    ItemCatalog.Definitions.Count,
                    Is.EqualTo(carryableCount),
                    "모든 CarryableItem이 배정 물건 또는 일반 맵 물건으로 등록되어야 합니다.");
                Assert.That(
                    carryableCount,
                    Is.GreaterThan(64),
                    "64개 초과 동기화 경로를 실제 맵 구성으로 검증해야 합니다.");
                Assert.That(
                    carryableCount,
                    Is.LessThanOrEqualTo(
                        Game.Network.Match.MatchSessionState.MaxReplicatedObjects),
                    "Playground의 모든 CarryableItem이 네트워크 상태 용량 안에 들어야 합니다.");
                foreach (var item in carryableItems)
                {
                    Assert.That(
                        item.ObjectId.Length,
                        Is.LessThanOrEqualTo(
                            Game.Network.Match.MatchSessionState.MaxObjectIdLength),
                        $"{item.name}: 물건 ID가 네트워크 제한보다 깁니다.");
                }
                Assert.That(
                    captured.RuntimeContext.ReplayObjects.Count,
                    Is.LessThanOrEqualTo(PlaygroundMatchScene.MaxReplayObjectCount),
                    "하이라이트 샘플 수는 라이브 동기화 용량과 별도로 제한해야 합니다.");

                foreach (var source in ItemCatalog.Definitions)
                {
                    Assert.That(
                        ContainsWorldObject(
                            captured.NetworkConfiguration.InitialWorldObjects,
                            source.ItemId),
                        Is.True,
                        $"{source.ItemId}: 맵 원본은 배정 후에도 일반 물건으로 남아야 합니다.");
                }

                foreach (var assigned in captured.NetworkConfiguration.ItemDefinitions)
                {
                    Assert.That(
                        assigned.ItemId,
                        Does.StartWith("Assigned_"),
                        "배정 물건은 맵 원본과 다른 런타임 ID를 써야 합니다.");
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

        private static bool ContainsWorldObject(
            IReadOnlyList<Game.Server.Items.WorldObjectState> states,
            string objectId)
        {
            foreach (var state in states)
            {
                if (string.Equals(state.ObjectId, objectId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
