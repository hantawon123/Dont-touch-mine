using System.Collections;
using System.Collections.Generic;
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
        public IEnumerator HomeToMatchEnd_CompletesRuntimeFlow()
        {
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();
            var state = new MatchState();

            try
            {
                var playerIds = new[] { "host", "guest" };
                var positions = new[] { Vector3.zero, Vector3.right };
                var appFlow = new AppFlowSystem();
                var session = new MatchSessionCoordinator(
                    rules,
                    state,
                    new MatchFlow(rules, state, playerIds.Length),
                    new PlayerInteractionSystem(rules, playerIds.Length),
                    playerIds,
                    new AcceptAllPlacements(),
                    CreateSpawnPoints(),
                    CreateItems(),
                    new System.Random(1234));
                var context = new TestRuntimeContext
                {
                    ServerTime = 10d,
                    PlayerPositions = positions,
                    PlayerPoses = CreatePlayerPoses(positions),
                    ReplayObjects = new WorldObjectState[0]
                };
                var runtime = new MatchRuntimeController(session, context, appFlow);
                var start = new LobbyMatchStartCoordinator(
                    CreateLobby(playerIds.Length),
                    runtime,
                    appFlow);

                Assert.That(appFlow.CurrentState, Is.EqualTo(AppFlowState.Home));
                Assert.That(appFlow.TryTransitionTo(AppFlowState.RoomBrowser), Is.True);
                Assert.That(appFlow.TryTransitionTo(AppFlowState.Lobby), Is.True);
                Assert.That(start.TryStart("host"), Is.EqualTo(RoomStartResult.Started));
                Assert.That(appFlow.CurrentState, Is.EqualTo(AppFlowState.InGame));
                Assert.That(session.SetHighlightCandidates(new[]
                {
                    new HighlightCandidate(HighlightType.FinalMoment, 420d, 430d, "final")
                }), Is.True);

                yield return null;

                context.ServerTime = 430d;
                runtime.Tick();
                Assert.That(appFlow.CurrentState, Is.EqualTo(AppFlowState.Highlight));
                Assert.That(session.CompleteCurrentHighlight(), Is.True);

                context.ServerTime = 431d;
                runtime.Tick();
                Assert.That(appFlow.CurrentState, Is.EqualTo(AppFlowState.Result));
                Assert.That(session.TryGetResult(out var result), Is.True);
                Assert.That(result.EndReason, Is.EqualTo(MatchEndReason.TimeExpired));
                Assert.That(appFlow.TryTransitionTo(AppFlowState.Lobby), Is.True);

                yield return null;

                Assert.That(appFlow.CurrentState, Is.EqualTo(AppFlowState.Lobby));
            }
            finally
            {
                state.Dispose();
                Object.DestroyImmediate(rules);
            }
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
