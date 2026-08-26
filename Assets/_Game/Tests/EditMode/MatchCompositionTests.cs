using System;
using System.Collections.Generic;
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

                using var container = builder.Build();
                var flow = container.Resolve<MatchFlow>();
                var state = container.Resolve<MatchState>();

                flow.Start(10d);

                Assert.That(state.CurrentPhase.CurrentValue, Is.EqualTo(MatchPhase.Hiding));
                Assert.That(container.Resolve<MatchState>(), Is.SameAs(state));
                Assert.That(container.Resolve<PlayerInteractionSystem>(), Is.Not.Null);
                Assert.That(container.Resolve<MatchRuntimeFactory>(), Is.Not.Null);
            }
            finally
            {
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

        private static Pose[] CreateSpawnPoints()
        {
            return new[]
            {
                new Pose(Vector3.zero, Quaternion.identity),
                new Pose(Vector3.right, Quaternion.identity)
            };
        }

        private static ItemDefinition[] CreateItems()
        {
            return new[]
            {
                new ItemDefinition("bear", "toy"),
                new ItemDefinition("apple", "food")
            };
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
