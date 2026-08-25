using System;
using System.Collections.Generic;
using Game.Bootstrap;
using Game.Server.Items;
using Game.SOAP.Config;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class MatchRuntimeContextTests
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
        public void CapturePlayers_UpdatesReusablePositionAndPoseBuffers()
        {
            var players = new Transform[MatchRulesSO.PlayerCount];
            for (var index = 0; index < players.Length; index++)
            {
                players[index] = CreateGameObject(
                    $"Player {index}",
                    new Vector3(index, 0f, index * 2f)).transform;
            }

            Vector3[] positions = null;
            Pose[] poses = null;
            MatchRuntimeContext.CapturePlayers(players, ref positions, ref poses);
            var originalPositions = positions;
            players[0].position = Vector3.up;
            MatchRuntimeContext.CapturePlayers(players, ref positions, ref poses);

            Assert.That(positions, Is.SameAs(originalPositions));
            Assert.That(positions[0], Is.EqualTo(Vector3.up));
            Assert.That(poses[5].position, Is.EqualTo(players[5].position));
        }

        [Test]
        public void CaptureWorldObjects_UpdatesReusableStateBuffer()
        {
            var item = CreateGameObject("Item", Vector3.right);
            var references = new[]
            {
                new SceneWorldObjectReference("item", item.transform)
            };
            WorldObjectState[] states = null;

            MatchRuntimeContext.CaptureWorldObjects(references, ref states);
            var originalStates = states;
            item.transform.position = Vector3.forward;
            MatchRuntimeContext.CaptureWorldObjects(references, ref states);

            Assert.That(states, Is.SameAs(originalStates));
            Assert.That(states[0].ObjectId, Is.EqualTo("item"));
            Assert.That(states[0].Pose.position, Is.EqualTo(Vector3.forward));
        }

        [Test]
        public void CapturePlayers_RequiresExactlySixAssignedTransforms()
        {
            Vector3[] positions = null;
            Pose[] poses = null;

            Assert.That(
                () => MatchRuntimeContext.CapturePlayers(
                    Array.Empty<Transform>(),
                    ref positions,
                    ref poses),
                Throws.TypeOf<InvalidOperationException>());
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
