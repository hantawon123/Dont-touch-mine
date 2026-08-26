using System;
using System.Collections.Generic;
using Game.Bootstrap;
using Game.Core.Flow;
using Game.Core.Items;
using Game.Core.Lobby;
using Game.Core.Match;
using Game.Server.Items;
using Game.Server.Match;
using Game.Server.Players;
using Game.SOAP.Config;
using NUnit.Framework;
using UnityEngine;
using VContainer;

namespace Game.Tests.EditMode
{
    public sealed class MatchCompositionTests
    {
        [Test]
        public void ScopedRegistrations_ResolveOneSharedMatchState()
        {
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();

            try
            {
                var builder = new ContainerBuilder();
                builder.RegisterInstance(rules);
                builder.Register<MatchState>(Lifetime.Scoped).AsSelf().As<IMatchState>();
                builder.Register<MatchFlow>(Lifetime.Scoped);
                builder.Register<PlayerInteractionSystem>(Lifetime.Scoped);
                builder.Register<MatchRuntimeFactory>(Lifetime.Scoped);
                builder.Register<HighlightRuntimeFactory>(Lifetime.Scoped);

                using var container = builder.Build();
                var flow = container.Resolve<MatchFlow>();
                var state = container.Resolve<MatchState>();

                flow.Start(10d);

                Assert.That(state.CurrentPhase.CurrentValue, Is.EqualTo(MatchPhase.Hiding));
                Assert.That(container.Resolve<MatchState>(), Is.SameAs(state));
                Assert.That(container.Resolve<PlayerInteractionSystem>(), Is.Not.Null);
                Assert.That(container.Resolve<MatchRuntimeFactory>(), Is.Not.Null);
                Assert.That(container.Resolve<HighlightRuntimeFactory>(), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rules);
            }
        }

        [Test]
        public void HighlightRuntimeFactory_ComposesPlaybackForActiveSession()
        {
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();
            var sceneObjects = new List<GameObject>();

            try
            {
                var participants = new[] { "host", "guest" };
                var lobby = CreateLobby(participants.Length);
                var appFlow = new AppFlowSystem();
                Assert.That(appFlow.TryTransitionTo(AppFlowState.RoomBrowser), Is.True);
                Assert.That(appFlow.TryTransitionTo(AppFlowState.Lobby), Is.True);

                var matchFactory = new MatchRuntimeFactory(rules);
                using var match = matchFactory.Create(
                    lobby,
                    new TestRuntimeContext(participants.Length),
                    appFlow,
                    participants,
                    new AcceptAllPlacements(),
                    CreateSpawnPoints(),
                    CreateItems(),
                    new System.Random(1234));

                var players = new Transform[participants.Length];
                for (var index = 0; index < players.Length; index++)
                {
                    var player = new GameObject($"Player {index}");
                    sceneObjects.Add(player);
                    players[index] = player.transform;
                }

                var camera = new GameObject("Highlight Camera");
                var fallback = new GameObject("Highlight Fallback");
                sceneObjects.Add(camera);
                sceneObjects.Add(fallback);

                var factory = new HighlightRuntimeFactory();
                var playback = factory.Create(
                    match,
                    players,
                    Array.Empty<SceneWorldObjectReference>(),
                    camera.transform,
                    fallback.transform);

                Assert.That(playback, Is.Not.Null);
                Assert.That(playback.IsPlaying, Is.False);
                Assert.That(() => playback.Tick(0f), Throws.Nothing);
                Assert.That(
                    () => factory.Create(
                        match,
                        new[] { players[0] },
                        Array.Empty<SceneWorldObjectReference>(),
                        camera.transform,
                        fallback.transform),
                    Throws.TypeOf<InvalidOperationException>());
            }
            finally
            {
                foreach (var sceneObject in sceneObjects)
                {
                    UnityEngine.Object.DestroyImmediate(sceneObject);
                }

                UnityEngine.Object.DestroyImmediate(rules);
            }
        }

        [Test]
        public void RuntimeFactory_CreatesAndStartsOneConsistentMatchGraph()
        {
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();

            try
            {
                var participants = new[] { "host", "guest" };
                var lobby = CreateLobby(participants.Length);
                var appFlow = new AppFlowSystem();
                Assert.That(appFlow.TryTransitionTo(AppFlowState.RoomBrowser), Is.True);
                Assert.That(appFlow.TryTransitionTo(AppFlowState.Lobby), Is.True);

                var context = new TestRuntimeContext(participants.Length);
                var factory = new MatchRuntimeFactory(rules);
                using var composition = factory.Create(
                    lobby,
                    context,
                    appFlow,
                    participants,
                    new AcceptAllPlacements(),
                    CreateSpawnPoints(),
                    CreateItems(),
                    new System.Random(1234));

                Assert.That(composition.Session.Players.Players.Count, Is.EqualTo(2));
                Assert.That(
                    composition.LobbyStart.TryStart("host"),
                    Is.EqualTo(RoomStartResult.Started));
                Assert.That(
                    composition.State.CurrentPhase.CurrentValue,
                    Is.EqualTo(MatchPhase.Hiding));
                Assert.That(appFlow.CurrentState, Is.EqualTo(AppFlowState.InGame));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rules);
            }
        }

        [Test]
        public void SessionFactory_CreatesFreshStateForRematch()
        {
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();

            try
            {
                var factory = new MatchRuntimeFactory(rules);
                var participants = new[] { "host", "guest" };
                using var first = factory.CreateSession(
                    participants,
                    new AcceptAllPlacements(),
                    CreateSpawnPoints(),
                    CreateItems(),
                    new System.Random(1));
                using var next = factory.CreateSession(
                    participants,
                    new AcceptAllPlacements(),
                    CreateSpawnPoints(),
                    CreateItems(),
                    new System.Random(2));

                Assert.That(first.Session.Start(10d), Is.True);
                Assert.That(first.State.CurrentPhase.CurrentValue, Is.EqualTo(MatchPhase.Hiding));
                Assert.That(next.State.CurrentPhase.CurrentValue, Is.EqualTo(MatchPhase.Waiting));
                Assert.That(next.State, Is.Not.SameAs(first.State));
                Assert.That(next.Session, Is.Not.SameAs(first.Session));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rules);
            }
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void ParticipantSnapshot_InitializesMatchByPlayerIndexForActualPlayerCount(
            int playerCount)
        {
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();

            try
            {
                var participants = new MatchParticipant[playerCount];
                for (var index = 0; index < playerCount; index++)
                {
                    var playerIndex = playerCount - index - 1;
                    participants[index] = new MatchParticipant(
                        $"player-{playerIndex}",
                        playerIndex);
                }

                var factory = new MatchRuntimeFactory(rules);
                using var composition = factory.CreateSessionFromParticipants(
                    participants,
                    new AcceptAllPlacements(),
                    CreateSpawnPoints(playerCount),
                    CreateItems(playerCount),
                    new System.Random(100 + playerCount));

                Assert.That(composition.Session.Players.Players.Count, Is.EqualTo(playerCount));
                Assert.That(composition.Session.Assignments.Count, Is.EqualTo(playerCount));
                for (var playerIndex = 0; playerIndex < playerCount; playerIndex++)
                {
                    Assert.That(
                        composition.Session.Players.GetPlayer(playerIndex).PlayerId,
                        Is.EqualTo($"player-{playerIndex}"));
                }

                Assert.That(composition.Session.Start(10d), Is.True);
                Assert.That(
                    composition.Session.GetRemainingSeconds(10d),
                    Is.EqualTo(30d * playerCount));
                Assert.That(
                    composition.Session.GetRemainingDestructionUses(playerCount - 1),
                    Is.EqualTo(rules.DestructionUsesPerPlayer));
                Assert.That(
                    composition.Session.GetSearchingSpawnPose(playerCount - 1),
                    Is.Not.EqualTo(default(Pose)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rules);
            }
        }

        [Test]
        public void ParticipantSnapshot_RejectsDuplicatePlayerIndices()
        {
            AssertInvalidParticipants(
                new MatchParticipant("first", 0),
                new MatchParticipant("second", 0));
        }

        [Test]
        public void ParticipantSnapshot_RejectsGappedPlayerIndices()
        {
            AssertInvalidParticipants(
                new MatchParticipant("first", 0),
                new MatchParticipant("second", 2));
        }

        [Test]
        public void ParticipantSnapshot_RejectsDuplicatePlayerIds()
        {
            AssertInvalidParticipants(
                new MatchParticipant("same", 0),
                new MatchParticipant("same", 1));
        }

        private static void AssertInvalidParticipants(params MatchParticipant[] participants)
        {
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();

            try
            {
                var factory = new MatchRuntimeFactory(rules);
                Assert.That(
                    () => factory.CreateSessionFromParticipants(
                        participants,
                        new AcceptAllPlacements(),
                        CreateSpawnPoints(participants.Length),
                        CreateItems(participants.Length),
                        new System.Random(1)),
                    Throws.ArgumentException);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rules);
            }
        }

        private static RoomLobbySystem CreateLobby(int playerCount)
        {
            var request = new Game.Core.Rooms.RoomCreateRequest(
                "Match Room",
                false,
                null,
                playerCount,
                "market-01");
            Assert.That(
                request.TryCreateSettings(playerCount, out var settings, out _),
                Is.True);
            return new RoomLobbySystem(settings, "host", playerCount);
        }

        private static Pose[] CreateSpawnPoints(int playerCount = 2)
        {
            var poses = new Pose[playerCount];
            for (var index = 0; index < playerCount; index++)
            {
                poses[index] = new Pose(Vector3.right * index, Quaternion.identity);
            }

            return poses;
        }

        private static ItemDefinition[] CreateItems(int playerCount = 2)
        {
            var items = new ItemDefinition[playerCount];
            for (var index = 0; index < playerCount; index++)
            {
                items[index] = new ItemDefinition($"item-{index}", $"category-{index}");
            }

            return items;
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
            private readonly Vector3[] playerPositions;
            private readonly Pose[] playerPoses;

            public TestRuntimeContext(int playerCount)
            {
                playerPositions = new Vector3[playerCount];
                playerPoses = new Pose[playerCount];
            }

            public double ServerTime => 10d;
            public IReadOnlyList<Vector3> PlayerPositions => playerPositions;
            public IReadOnlyList<Pose> PlayerPoses => playerPoses;
            public IReadOnlyList<WorldObjectState> ReplayObjects =>
                Array.Empty<WorldObjectState>();
        }
    }
}
