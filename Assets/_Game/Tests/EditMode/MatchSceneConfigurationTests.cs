using System;
using System.Collections.Generic;
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
