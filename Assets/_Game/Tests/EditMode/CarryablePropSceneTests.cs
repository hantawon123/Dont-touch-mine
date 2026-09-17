using System;
using System.Collections.Generic;
using Game.Bootstrap;
using Game.Client.Interactions;
using Game.Core.Items;
using Game.SOAP.Config;
using System.Linq;
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
        public void Catalog_HasRealCarryablePrefabs()
        {
            var catalog = ItemCatalogSO.Load();
            foreach (var item in catalog.categories.Where(c => c.enabled).SelectMany(c => c.items.Where(i => i.enabled)))
            {
                Assert.That(item.prefab.GetComponent<CarryableItem>(), Is.Not.Null, item.id);
                Assert.That(item.prefab.GetComponent<Rigidbody>(), Is.Not.Null, item.id);
                Assert.That(item.prefab.GetComponent<Collider>(), Is.Not.Null, item.id);
            }
        }

        [Test]
        public void Capture_RejectsSceneMissingCatalogItemsBeforeRuntimeStarts()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Empty Playground");
            SceneManager.MoveGameObjectToScene(root, scene);

            try
            {
                var failure = Assert.Throws<InvalidOperationException>(
                    () => PlaygroundMatchScene.Capture(scene));

                Assert.That(failure.Message, Does.Contain("ShredderSpot"));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
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
                    ItemCatalog.AssignmentDefinitions.Count,
                    Is.EqualTo(carryableCount),
                    "모든 CarryableItem이 배정 물건 또는 일반 맵 물건으로 등록되어야 합니다.");
                Assert.That(
                    carryableCount,
                    Is.GreaterThan(64),
                    "64개 초과 동기화 경로를 실제 맵 구성으로 검증해야 합니다.");
                Assert.That(
                    captured.NetworkConfiguration.InitialWorldObjects.Count + MatchRulesSO.MaxPlayerCount,
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
                var replayIds = new HashSet<string>();
                Assert.That(PlaygroundMatchScene.TryCollectReplayObjectIds(captured.RuntimeContext, replayIds), Is.True);
                Assert.That(replayIds, Is.EquivalentTo(captured.RuntimeContext.ReplayObjects.Select(o => o.ObjectId)));
                var activeItem = carryableItems.First(i => replayIds.Contains(i.ObjectId));
                try
                {
                    activeItem.gameObject.SetActive(false);
                    replayIds.Clear();
                    PlaygroundMatchScene.TryCollectReplayObjectIds(captured.RuntimeContext, replayIds);
                    Assert.That(replayIds, Does.Not.Contain(activeItem.ObjectId));
                    Assert.That(replayIds, Is.EquivalentTo(captured.RuntimeContext.ReplayObjects.Select(o => o.ObjectId)));
                }
                finally { activeItem.gameObject.SetActive(true); }
                replayIds.Clear();
                PlaygroundMatchScene.TryCollectReplayObjectIds(captured.RuntimeContext, replayIds);
                Assert.That(replayIds, Does.Contain(activeItem.ObjectId));
                Assert.That(replayIds, Is.EquivalentTo(captured.RuntimeContext.ReplayObjects.Select(o => o.ObjectId)));

                Assert.That(
                    captured.RuntimeContext.ReplayObjects.Count,
                    Is.LessThanOrEqualTo(PlaygroundMatchScene.MaxReplayObjectCount),
                    "하이라이트 샘플 수는 라이브 동기화 용량과 별도로 제한해야 합니다.");

                var catalog = ItemCatalogSO.Load(applyDefinitions: false);
                foreach (var assigned in captured.NetworkConfiguration.ItemDefinitions)
                {
                    var instance = carryableItems.Single(i => i.ObjectId == assigned.ItemId);
                    var prefab = catalog.PrefabOf(assigned.ItemId);
                    Assert.That(prefab, Is.Not.Null, assigned.ItemId);
                    AssertTransformScalesMatch(prefab.transform, instance.transform, assigned.ItemId);
                    Assert.That(instance.gameObject.activeSelf, Is.False);
                    Assert.That(ContainsWorldObject(captured.NetworkConfiguration.InitialWorldObjects, assigned.ItemId), Is.False);
                }
                Assert.That(captured.NetworkConfiguration.ItemDefinitions.Count, Is.EqualTo(ItemCatalog.Definitions.Count));

                var rules = ScriptableObject.CreateInstance<MatchRulesSO>();
                try
                {
                    var config = captured.NetworkConfiguration;
                    foreach (var category in ItemCatalog.Categories.Concat(new[] { string.Empty }).ToArray())
                    {
                        Assert.That(Game.Core.Lobby.MatchRuleSettings.TryCreate(30, 5, 1f, 3, category, out var settings, out _), Is.True);
                        using var match = new Game.Server.Match.MatchRuntimeFactory(rules).CreateSession(
                            Enumerable.Range(0, 6).Select(i => "player_" + i).ToArray(), config.PlacementValidator,
                            config.SpawnPoints, config.ItemDefinitions, new System.Random(42), config.InitialWorldObjects,
                            matchRules: settings);
                        var allocated = match.Session.Assignments;
                        Assert.That(allocated.Select(a => a.Item.ItemId).Distinct().Count(), Is.EqualTo(6));
                        Assert.That(allocated.Select(a => a.Item.Category).Distinct().Count(), Is.EqualTo(1));
                        if (category.Length > 0) Assert.That(allocated.All(a => a.Item.Category == category), Is.True);
                        foreach (var assignment in allocated)
                        {
                            var item = carryableItems.Single(i => i.ObjectId == assignment.Item.ItemId);
                            item.gameObject.SetActive(true);
                            Assert.That(captured.RuntimeContext.ReplayObjects.Any(o => o.ObjectId == assignment.Item.ItemId), Is.True,
                                "Every assigned item must remain in replay even when the catalog exceeds replay capacity.");
                            item.gameObject.SetActive(false);
                        }
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(rules); }

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

        private static void AssertTransformScalesMatch(
            Transform expected,
            Transform actual,
            string itemId)
        {
            Assert.That(actual.localScale, Is.EqualTo(expected.localScale),
                $"{itemId}/{expected.name}: 프리팹 스케일이 런타임 복사본에 유지되어야 합니다.");
            Assert.That(actual.childCount, Is.EqualTo(expected.childCount),
                $"{itemId}/{expected.name}: 프리팹 Transform 계층이 런타임 복사본과 다릅니다.");

            for (var index = 0; index < expected.childCount; index++)
            {
                AssertTransformScalesMatch(
                    expected.GetChild(index),
                    actual.GetChild(index),
                    itemId);
            }
        }
    }
}
