using System.Collections.Generic;
using Game.Bootstrap;
using Game.Server.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class HighlightCameraDirectorTests
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
        public void Focus_UsesCloseDistanceForEventHighlights()
        {
            var camera = Create("Camera", Vector3.zero);
            var fallback = Create("Fallback", Vector3.left);
            var item = Create("Item", Vector3.zero);
            var director = Director(
                camera.transform,
                fallback.transform,
                new Transform[0],
                new[] { new SceneWorldObjectReference("item", item.transform) });

            Assert.That(director.Focus(Candidate(HighlightType.FirstBlood, "item")), Is.True);

            Assert.That(camera.transform.position.z, Is.EqualTo(-8f));
        }

        [Test]
        public void Focus_UsesWideDistanceForJourneyHighlights()
        {
            var camera = Create("Camera", Vector3.zero);
            var fallback = Create("Fallback", Vector3.left);
            var item = Create("Item", Vector3.zero);
            var director = Director(
                camera.transform,
                fallback.transform,
                new Transform[0],
                new[] { new SceneWorldObjectReference("item", item.transform) });

            director.Focus(Candidate(HighlightType.LongestHidden, "item"));

            Assert.That(camera.transform.position.z, Is.EqualTo(-12f));
        }

        [Test]
        public void Focus_ResolvesPlayerIndexAndFollowsMovement()
        {
            var camera = Create("Camera", Vector3.zero);
            var fallback = Create("Fallback", Vector3.left);
            var player = Create("Player", Vector3.zero);
            var director = Director(
                camera.transform,
                fallback.transform,
                new[] { player.transform },
                new SceneWorldObjectReference[0]);

            Assert.That(director.Focus(Candidate(HighlightType.MostStunned, "0")), Is.True);
            player.transform.position = Vector3.right * 10f;
            director.Tick(1f);

            Assert.That(camera.transform.position.x, Is.GreaterThan(9f));
        }

        [Test]
        public void Focus_UsesFallbackWhenTargetDoesNotExist()
        {
            var camera = Create("Camera", Vector3.zero);
            var fallback = Create("Fallback", new Vector3(1f, 2f, 3f));
            fallback.transform.rotation = Quaternion.Euler(10f, 20f, 30f);
            var director = Director(
                camera.transform,
                fallback.transform,
                new Transform[0],
                new SceneWorldObjectReference[0]);

            Assert.That(director.Focus(Candidate(HighlightType.FinalMoment, "missing")), Is.False);
            Assert.That(camera.transform.position, Is.EqualTo(fallback.transform.position));
            Assert.That(
                Quaternion.Angle(camera.transform.rotation, fallback.transform.rotation),
                Is.LessThan(0.001f));
        }

        [Test]
        public void Focus_HidesAndRestoresRendererBlockingTheTarget()
        {
            var camera = Create("Camera", Vector3.zero);
            var fallback = Create("Fallback", Vector3.left);
            var item = Create("Item", Vector3.zero);
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = "Wall";
            blocker.transform.position = new Vector3(0f, 2.75f, -4f);
            blocker.transform.localScale = new Vector3(4f, 4f, 0.5f);
            gameObjects.Add(blocker);
            Physics.SyncTransforms();

            var director = Director(
                camera.transform,
                fallback.transform,
                new Transform[0],
                new[] { new SceneWorldObjectReference("item", item.transform) });

            director.Focus(Candidate(HighlightType.FirstBlood, "item"));

            Assert.That(blocker.GetComponent<Renderer>().forceRenderingOff, Is.True);
            director.ClearOccluders();
            Assert.That(blocker.GetComponent<Renderer>().forceRenderingOff, Is.False);
        }

        private static HighlightCameraDirector Director(
            Transform camera,
            Transform fallback,
            IReadOnlyList<Transform> players,
            IReadOnlyList<SceneWorldObjectReference> objects)
        {
            return new HighlightCameraDirector(camera, fallback, players, objects);
        }

        private static HighlightCandidate Candidate(HighlightType type, string targetId)
        {
            return new HighlightCandidate(type, 0d, 10d, targetId);
        }

        private GameObject Create(string name, Vector3 position)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.position = position;
            gameObjects.Add(gameObject);
            return gameObject;
        }
    }
}
