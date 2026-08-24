using System.Collections.Generic;
using Game.Bootstrap;
using Game.Server.Items;
using Game.Server.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class HighlightReplayPlayerTests
    {
        private readonly List<GameObject> gameObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var gameObject in gameObjects)
            {
                Object.DestroyImmediate(gameObject);
            }

            gameObjects.Clear();
        }

        [Test]
        public void Start_AppliesFirstRecordedPlayerAndObjectPoses()
        {
            var playerTarget = CreateGameObject("Player").transform;
            var objectTarget = CreateGameObject("Item").transform;
            var player = new HighlightReplayPlayer(
                new[] { playerTarget },
                new[] { new SceneWorldObjectReference("item", objectTarget) });

            Assert.That(player.Start(new[]
            {
                Clip(
                    new HighlightSegment(10d, 11d),
                    Frame(10d, Vector3.one, Vector3.right))
            }), Is.True);

            Assert.That(playerTarget.position, Is.EqualTo(Vector3.one));
            Assert.That(objectTarget.position, Is.EqualTo(Vector3.right));
        }

        [Test]
        public void Advance_InterpolatesBetweenRecordedFrames()
        {
            var target = CreateGameObject("Player").transform;
            var player = new HighlightReplayPlayer(
                new[] { target },
                new SceneWorldObjectReference[0]);
            player.Start(new[]
            {
                Clip(
                    new HighlightSegment(10d, 11d),
                    Frame(10d, Vector3.zero),
                    Frame(11d, Vector3.right * 10f))
            });

            Assert.That(player.Advance(0.5d), Is.True);

            Assert.That(target.position, Is.EqualTo(Vector3.right * 5f));
        }

        [Test]
        public void Advance_UsesPlaybackSpeedAndCompletesAllClips()
        {
            var target = CreateGameObject("Player").transform;
            var player = new HighlightReplayPlayer(
                new[] { target },
                new SceneWorldObjectReference[0]);
            player.Start(new[]
            {
                Clip(
                    new HighlightSegment(0d, 4d, 2d),
                    Frame(0d, Vector3.zero),
                    Frame(4d, Vector3.right * 4f)),
                Clip(
                    new HighlightSegment(10d, 11d),
                    Frame(10d, Vector3.up),
                    Frame(11d, Vector3.up * 2f))
            });

            Assert.That(player.Advance(1d), Is.True);
            Assert.That(target.position, Is.EqualTo(Vector3.right * 2f));
            Assert.That(player.Advance(1.5d), Is.True);
            Assert.That(target.position, Is.EqualTo(Vector3.up * 1.5f));
            Assert.That(player.Advance(0.5d), Is.False);
            Assert.That(player.IsPlaying, Is.False);
        }

        private HighlightReplayFrame Frame(
            double recordedAt,
            Vector3 playerPosition,
            Vector3? objectPosition = null)
        {
            var worldObjects = objectPosition.HasValue
                ? new[]
                {
                    new WorldObjectState(
                        "item",
                        new Pose(objectPosition.Value, Quaternion.identity))
                }
                : new WorldObjectState[0];
            return new HighlightReplayFrame(
                recordedAt,
                new[] { new Pose(playerPosition, Quaternion.identity) },
                worldObjects);
        }

        private static HighlightReplayClip Clip(
            HighlightSegment segment,
            params HighlightReplayFrame[] frames)
        {
            return new HighlightReplayClip(segment, frames);
        }

        private GameObject CreateGameObject(string name)
        {
            var gameObject = new GameObject(name);
            gameObjects.Add(gameObject);
            return gameObject;
        }
    }
}
