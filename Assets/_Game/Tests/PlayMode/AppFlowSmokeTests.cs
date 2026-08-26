using System.Collections;
using System.Collections.Generic;
using Game.Bootstrap;
using Game.Core.Flow;
using Game.Core.Items;
using Game.Core.Lobby;
using Game.Core.Match;
using Game.Core.Rooms;
using Game.Server.Items;
using Game.Server.Match;
using Game.Server.Players;
using Game.SOAP.Config;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public sealed class AppFlowSmokeTests
    {
        [UnityTest]
        public IEnumerator HomeToRematch_CompletesFullRuntimeFlow()
        {
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();
            var sceneObjects = new List<GameObject>();

            try
            {
                var playerIds = new[] { "host", "guest" };
                var positions = new[] { Vector3.zero, Vector3.right };
                var appFlow = new AppFlowSystem();
                var context = new TestRuntimeContext
                {
                    ServerTime = 10d,
                    PlayerPositions = positions,
                    PlayerPoses = CreatePlayerPoses(positions),
                    ReplayObjects = new WorldObjectState[0]
                };
                var factory = new MatchRuntimeFactory(rules);
                using var match = factory.Create(
                    CreateLobby(playerIds.Length),
                    context,
                    appFlow,
                    playerIds,
                    new AcceptAllPlacements(),
                    CreateSpawnPoints(),
                    CreateItems(),
                    new System.Random(1234));
                var playerTargets = CreatePlayerTargets(playerIds.Length, sceneObjects);
                var camera = CreateSceneObject("Highlight Camera", sceneObjects);
                var fallback = CreateSceneObject("Highlight Fallback", sceneObjects);
                var highlight = new HighlightRuntimeFactory().Create(
                    match,
                    playerTargets,
                    new SceneWorldObjectReference[0],
                    camera.transform,
                    fallback.transform);

                Assert.That(appFlow.CurrentState, Is.EqualTo(AppFlowState.Home));
                Assert.That(appFlow.TryTransitionTo(AppFlowState.RoomBrowser), Is.True);
                Assert.That(appFlow.TryTransitionTo(AppFlowState.Lobby), Is.True);
                Assert.That(
                    match.LobbyStart.TryStart("host"),
                    Is.EqualTo(RoomStartResult.Started));
                Assert.That(appFlow.CurrentState, Is.EqualTo(AppFlowState.InGame));
                Assert.That(match.Session.CurrentPhase, Is.EqualTo(MatchPhase.Hiding));
                Assert.That(match.Session.GetCurrentHidingTurnIndex(10d), Is.Zero);

                positions[0] = new Vector3(3f, 0f, 0f);
                context.ServerTime = 40d;
                context.PlayerPoses = CreatePlayerPoses(positions);
                match.Runtime.Tick();
                Assert.That(match.Session.GetCurrentHidingTurnIndex(40d), Is.EqualTo(1));
                Assert.That(match.Session.TryGetItemPlacement(0, out var firstPlacement), Is.True);
                Assert.That(firstPlacement.WasAutoPlaced, Is.True);
                Assert.That(firstPlacement.Pose.position, Is.EqualTo(positions[0]));

                positions[1] = new Vector3(4f, 0f, 0f);
                context.ServerTime = 70d;
                context.PlayerPoses = CreatePlayerPoses(positions);
                match.Runtime.Tick();
                Assert.That(match.Session.CurrentPhase, Is.EqualTo(MatchPhase.Searching));
                Assert.That(match.Session.AllItemsPlaced, Is.True);
                Assert.That(appFlow.CurrentState, Is.EqualTo(AppFlowState.InGame));

                yield return null;

                context.ServerTime = 420d;
                match.Runtime.Tick();
                Assert.That(match.Session.IsFinalPeriod(420d), Is.True);

                context.ServerTime = 430d;
                match.Runtime.Tick();
                Assert.That(appFlow.CurrentState, Is.EqualTo(AppFlowState.Highlight));
                Assert.That(
                    match.Session.TryGetCurrentHighlight(out var generatedHighlight),
                    Is.True);
                Assert.That(generatedHighlight.Type, Is.EqualTo(HighlightType.LongestHidden));
                Assert.That(highlight.IsPlaying, Is.False);
                highlight.Tick(10f);
                Assert.That(highlight.IsPlaying, Is.False);
                Assert.That(match.Session.CurrentPhase, Is.EqualTo(MatchPhase.Result));
                Assert.That(playerTargets[0].position, Is.EqualTo(positions[0]));

                context.ServerTime = 431d;
                match.Runtime.Tick();
                Assert.That(appFlow.CurrentState, Is.EqualTo(AppFlowState.Result));
                Assert.That(match.Session.TryGetResult(out var result), Is.True);
                Assert.That(result.EndReason, Is.EqualTo(MatchEndReason.TimeExpired));

                using var rematch = factory.CreateSession(
                    playerIds,
                    new AcceptAllPlacements(),
                    CreateSpawnPoints(),
                    CreateItems(),
                    new System.Random(5678));
                Assert.That(match.LobbyStart.TryPrepareRematch(rematch.Session), Is.True);
                Assert.That(appFlow.CurrentState, Is.EqualTo(AppFlowState.Lobby));
                Assert.That(
                    match.LobbyStart.TryStart("host"),
                    Is.EqualTo(RoomStartResult.Started));

                yield return null;

                Assert.That(appFlow.CurrentState, Is.EqualTo(AppFlowState.InGame));
                Assert.That(rematch.Session.CurrentPhase, Is.EqualTo(MatchPhase.Hiding));
            }
            finally
            {
                foreach (var sceneObject in sceneObjects)
                {
                    Object.DestroyImmediate(sceneObject);
                }

                Object.DestroyImmediate(rules);
            }
        }

        private static Transform[] CreatePlayerTargets(
            int playerCount,
            ICollection<GameObject> sceneObjects)
        {
            var targets = new Transform[playerCount];
            for (var index = 0; index < playerCount; index++)
            {
                targets[index] = CreateSceneObject($"Player {index}", sceneObjects).transform;
            }

            return targets;
        }

        private static GameObject CreateSceneObject(
            string name,
            ICollection<GameObject> sceneObjects)
        {
            var sceneObject = new GameObject(name);
            sceneObjects.Add(sceneObject);
            return sceneObject;
        }

        private static RoomLobbySystem CreateLobby(int playerCount)
        {
            var request = new RoomCreateRequest(
                "Smoke Room",
                false,
                null,
                playerCount,
                "market-01");
            Assert.That(
                request.TryCreateSettings(playerCount, out var settings, out _),
                Is.True);
            return new RoomLobbySystem(settings, "host", playerCount);
        }

        private static ItemDefinition[] CreateItems()
        {
            return new[]
            {
                new ItemDefinition("bear", "toy"),
                new ItemDefinition("apple", "food")
            };
        }

        private static Pose[] CreateSpawnPoints()
        {
            return new[]
            {
                new Pose(Vector3.zero, Quaternion.identity),
                new Pose(Vector3.right, Quaternion.identity)
            };
        }

        private static Pose[] CreatePlayerPoses(IReadOnlyList<Vector3> positions)
        {
            var poses = new Pose[positions.Count];
            for (var index = 0; index < poses.Length; index++)
            {
                poses[index] = new Pose(positions[index], Quaternion.identity);
            }

            return poses;
        }

        private sealed class AcceptAllPlacements : IPlacementValidator
        {
            public bool IsValid(string objectId, Pose pose)
            {
                return true;
            }
        }

        private sealed class TestRuntimeContext : IMatchRuntimeContext
        {
            public double ServerTime { get; set; }
            public IReadOnlyList<Vector3> PlayerPositions { get; set; }
            public IReadOnlyList<Pose> PlayerPoses { get; set; }
            public IReadOnlyList<WorldObjectState> ReplayObjects { get; set; }
        }
    }
}
