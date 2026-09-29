using System;
using System.Collections.Generic;
using System.Linq;
using Game.Bootstrap;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class MatchSceneConfigurationTests
    {
        private readonly List<GameObject> gameObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var gameObject in gameObjects)
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }

            gameObjects.Clear();
        }

        [Test]
        public void Capture_ReturnsSpawnPosesAndInitialWorldObjectStates()
        {
            var spawnPoints = CreateSpawnPoints();
            var shelf = CreateGameObject("Shelf", new Vector3(7f, 1f, 3f));

            var spawnPoses = MatchSceneConfiguration.CaptureSpawnPoses(spawnPoints);
            var worldObjects = MatchSceneConfiguration.CaptureWorldObjectStates(
                new[] { new SceneWorldObjectReference("shelf", shelf.transform) });

            Assert.That(spawnPoses, Has.Length.EqualTo(6));
            Assert.That(spawnPoses[5].position, Is.EqualTo(spawnPoints[5].position));
            Assert.That(worldObjects, Has.Length.EqualTo(1));
            Assert.That(worldObjects[0].ObjectId, Is.EqualTo("shelf"));
            Assert.That(worldObjects[0].Pose.position, Is.EqualTo(shelf.transform.position));
        }

        [Test]
        public void Capture_RejectsDuplicateSpawnPositionsAndWorldObjectIds()
        {
            var spawnPoints = CreateSpawnPoints();
            spawnPoints[5].position = spawnPoints[0].position;
            var first = CreateGameObject("First", Vector3.zero);
            var second = CreateGameObject("Second", Vector3.one);

            Assert.That(
                () => MatchSceneConfiguration.CaptureSpawnPoses(spawnPoints),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(
                () => MatchSceneConfiguration.CaptureWorldObjectStates(
                    new[]
                    {
                        new SceneWorldObjectReference("shelf", first.transform),
                        new SceneWorldObjectReference("shelf", second.transform)
                    }),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void CreateShuffledOrder_IsPermutation_AndApplyOrderReorders()
        {
            var order = MatchSceneConfiguration.CreateShuffledOrder(10, new System.Random(1234));

            Assert.That(order, Has.Length.EqualTo(10));
            Assert.That(order.Distinct().Count(), Is.EqualTo(10));
            Assert.That(order.Min(), Is.EqualTo(0));
            Assert.That(order.Max(), Is.EqualTo(9));

            var poses = Enumerable.Range(0, 10)
                .Select(i => new Pose(new Vector3(i, 0f, 0f), Quaternion.identity))
                .ToArray();
            var applied = MatchSceneConfiguration.ApplyOrder(poses, order);
            for (var index = 0; index < poses.Length; index++)
            {
                Assert.That(applied[index].position.x, Is.EqualTo(order[index]));
            }
        }

        [Test]
        public void CaptureSpawnPoses_ShuffleOn_UsesOneStableRandomOrderPerInstance()
        {
            var spawnPoints = CreateSpawnPoints();
            var host = CreateGameObject("SpawnPoints", Vector3.zero);
            var configuration = host.AddComponent<MatchSceneConfiguration>();
            using (var serialized = new UnityEditor.SerializedObject(configuration))
            {
                var points = serialized.FindProperty("spawnPoints");
                points.arraySize = spawnPoints.Length;
                for (var index = 0; index < spawnPoints.Length; index++)
                {
                    points.GetArrayElementAtIndex(index).objectReferenceValue = spawnPoints[index];
                }

                serialized.FindProperty("shuffleSpawnPoints").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            var first = configuration.CaptureSpawnPoses();
            var second = configuration.CaptureSpawnPoses();
            var unshuffled = MatchSceneConfiguration.CaptureSpawnPoses(spawnPoints);

            Assert.That(first.Select(p => p.position), Is.EqualTo(second.Select(p => p.position)),
                "같은 인스턴스는 두 번 물어도 같은 순서를 돌려줘야 한다.");
            Assert.That(first.Select(p => p.position), Is.EquivalentTo(unshuffled.Select(p => p.position)),
                "섞인 결과는 원래 지점들의 순열이어야 한다.");
        }

        [Test]
        public void CaptureSpawnPoses_ShuffleOff_KeepsAuthoredOrder()
        {
            var spawnPoints = CreateSpawnPoints();
            var host = CreateGameObject("SpawnPoints", Vector3.zero);
            var configuration = host.AddComponent<MatchSceneConfiguration>();
            using (var serialized = new UnityEditor.SerializedObject(configuration))
            {
                var points = serialized.FindProperty("spawnPoints");
                points.arraySize = spawnPoints.Length;
                for (var index = 0; index < spawnPoints.Length; index++)
                {
                    points.GetArrayElementAtIndex(index).objectReferenceValue = spawnPoints[index];
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            var poses = configuration.CaptureSpawnPoses();
            for (var index = 0; index < spawnPoints.Length; index++)
            {
                Assert.That(poses[index].position, Is.EqualTo(spawnPoints[index].position));
            }
        }

        [Test]
        public void CapturePlacementVolumes_PreservesConfiguredBounds()
        {
            var volumes = MatchSceneConfiguration.CapturePlacementVolumes(new[]
            {
                new ScenePlacementVolumeReference(
                    "item",
                    Vector3.up,
                    new Vector3(0.5f, 1f, 0.25f)),
            });

            Assert.That(volumes, Has.Length.EqualTo(1));
            Assert.That(volumes[0].ObjectId, Is.EqualTo("item"));
            Assert.That(volumes[0].CenterOffset, Is.EqualTo(Vector3.up));
            Assert.That(volumes[0].HalfExtents, Is.EqualTo(new Vector3(0.5f, 1f, 0.25f)));
        }

        private Transform[] CreateSpawnPoints()
        {
            var spawnPoints = new Transform[6];
            for (var index = 0; index < spawnPoints.Length; index++)
            {
                spawnPoints[index] = CreateGameObject(
                    $"Spawn {index}",
                    new Vector3(index * 2f, 0f, index)).transform;
            }

            return spawnPoints;
        }

        private GameObject CreateGameObject(string name, Vector3 position)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.position = position;
            gameObjects.Add(gameObject);
            return gameObject;
        }
    }
}
