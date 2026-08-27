using System;
using System.Collections.Generic;
using Game.Bootstrap;
using Game.Core.Flow;
using Game.Core.Items;
using Game.Core.Lobby;
using Game.Core.Match;
using Game.Core.Players;
using Game.Network.Match;
using Game.Server.Items;
using Game.Server.Match;
using Game.SOAP.Config;
using NUnit.Framework;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class NetworkMatchRuntimeCoordinatorTests
    {
        [Test]
        public void Authority_StartsTicksPublishesAndReleasesMatchRuntime()
        {
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();

            try
            {
                var network = new FakeNetworkAuthority(10d, new Dictionary<string, Pose>
                {
                    ["host"] = Pose.identity,
                    ["client"] = new Pose(Vector3.right, Quaternion.identity),
                });
                using var roomState = new RoomBrowserSystem();
                var appFlow = new AppFlowSystem();
                Assert.That(appFlow.TryTransitionTo(AppFlowState.Lobby), Is.True);
                Assert.That(appFlow.TryTransitionTo(AppFlowState.InGame), Is.True);
                var coordinator = new NetworkMatchRuntimeCoordinator(
                    network,
                    new MatchRuntimeFactory(rules),
                    new FakeSceneContext(),
                    appFlow,
                    new NetworkMatchRuntimeConfiguration(
                        new AcceptAllPlacements(),
                        CreateSpawnPoints(),
                        CreateItems(),
                        Array.Empty<WorldObjectState>(),
                        new Pose(Vector3.forward, Quaternion.identity),
                        CreateWaitingPoints()),
                    roomState);

                coordinator.Start();
                network.PublishLineUp(new[]
                {
                    new MatchParticipant("host", 0),
                    new MatchParticipant("client", 1),
                });
                network.PublishSimulationTick();

                Assert.That(network.BoundSession, Is.Not.Null);
                Assert.That(network.InitializedAssignmentPlayers, Is.EqualTo(new[] { 0 }));
                Assert.That(network.PublishedAssignmentPlayers, Is.EqualTo(new[] { 0 }));
                Assert.That(network.Snapshots, Has.Count.EqualTo(1));
                Assert.That(network.Snapshots[0].Phase, Is.EqualTo(MatchPhase.Hiding));
                Assert.That(network.Controls[0], Is.True);
                Assert.That(network.Controls[1], Is.True);
                Assert.That(network.TeleportedPlayers, Is.EqualTo(new[] { 0, 1 }));
                Assert.That(network.TeleportedPoses[1].position.z, Is.EqualTo(-10f));

                network.ServerTime = 10d + rules.HidingTurnDurationSeconds;
                network.PublishSimulationTick();

                Assert.That(network.Controls[0], Is.True);
                Assert.That(network.Controls[1], Is.True);
                Assert.That(
                    network.InitializedAssignmentPlayers,
                    Is.EqualTo(new[] { 0, 1 }));
                Assert.That(
                    network.PublishedAssignmentPlayers,
                    Is.EqualTo(new[] { 0, 1 }));
                // 턴 교대: 새로 숨는 1번만 집으로, 직전에 숨긴 0번만 대기 구역으로 이동한다.
                Assert.That(
                    network.TeleportedPlayers,
                    Is.EqualTo(new[] { 0, 1, 1, 0 }));
                Assert.That(network.TeleportedPoses[2].position.z, Is.EqualTo(0f));
                Assert.That(network.TeleportedPoses[3].position.z, Is.EqualTo(-10f));

                network.ServerTime = network.Snapshots[0].PhaseEndsAt;
                network.PublishSimulationTick();

                Assert.That(network.Snapshots, Has.Count.EqualTo(2));
                Assert.That(network.Snapshots[1].Phase, Is.EqualTo(MatchPhase.Searching));
                Assert.That(network.Controls[0], Is.True);
                Assert.That(network.Controls[1], Is.True);
                Assert.That(
                    network.TeleportedPlayers,
                    Is.EqualTo(new[] { 0, 1, 1, 0, 0, 1 }));

                var searchingStartedAt = network.Snapshots[0].PhaseEndsAt;
                Assert.That(
                    network.BoundSession.RegisterHit(
                        0, 1, Vector3.right, searchingStartedAt),
                    Is.EqualTo(HitResult.Registered));
                Assert.That(
                    network.BoundSession.RegisterHit(
                        0, 1, Vector3.right, searchingStartedAt),
                    Is.EqualTo(HitResult.Registered));
                Assert.That(
                    network.BoundSession.RegisterHit(
                        0, 1, Vector3.right, searchingStartedAt),
                    Is.EqualTo(HitResult.Stunned));
                network.ServerTime = searchingStartedAt;
                network.PublishSimulationTick();

                Assert.That(network.Controls[0], Is.True);
                Assert.That(network.Controls[1], Is.False);

                network.ServerTime = searchingStartedAt +
                                     rules.StunDurationSeconds + 1d;
                network.PublishSimulationTick();

                Assert.That(network.Controls[1], Is.True);

                Assert.That(
                    network.BoundSession.SetHighlightCandidates(new[]
                    {
                        new HighlightCandidate(
                            HighlightType.FirstBlood,
                            searchingStartedAt,
                            searchingStartedAt + 4d,
                            "client"),
                    }),
                    Is.True);
                network.ServerTime = network.Snapshots[1].PhaseEndsAt;
                network.PublishSimulationTick();

                Assert.That(network.Snapshots, Has.Count.EqualTo(3));
                Assert.That(network.Snapshots[2].Phase, Is.EqualTo(MatchPhase.Highlight));
                Assert.That(network.HighlightReplay.Count, Is.EqualTo(1));
                Assert.That(
                    network.HighlightReplay[0].Candidate.Type,
                    Is.EqualTo(HighlightType.FirstBlood));
                Assert.That(
                    network.HighlightReplay[0].Clips[0].Frames,
                    Is.Not.Empty);

                network.ServerTime = network.Snapshots[2].PhaseEndsAt;
                network.PublishSimulationTick();

                Assert.That(network.Snapshots, Has.Count.EqualTo(4));
                Assert.That(network.Snapshots[3].Phase, Is.EqualTo(MatchPhase.Result));

                coordinator.Dispose();

                Assert.That(network.BoundSession, Is.Null);
                Assert.That(network.UnbindCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rules);
            }
        }

        private static Pose[] CreateSpawnPoints()
        {
            var poses = new Pose[MatchRulesSO.MaxPlayerCount];
            for (var index = 0; index < poses.Length; index++)
            {
                poses[index] = new Pose(Vector3.right * index, Quaternion.identity);
            }

            return poses;
        }

        private static ItemDefinition[] CreateItems()
        {
            return new[]
            {
                new ItemDefinition("item-0", "category-0"),
                new ItemDefinition("item-1", "category-1"),
            };
        }

        private static Pose[] CreateWaitingPoints()
        {
            var poses = new Pose[MatchRulesSO.MaxPlayerCount];
            for (var index = 0; index < poses.Length; index++)
            {
                poses[index] = new Pose(
                    new Vector3(index, 0f, -10f),
                    Quaternion.identity);
            }

            return poses;
        }

        private sealed class AcceptAllPlacements : IPlacementValidator
        {
            public bool IsValid(string objectId, Pose pose) => true;
        }

        private sealed class FakeSceneContext : IMatchRuntimeContext
        {
            public double ServerTime => 0d;
            public IReadOnlyList<Vector3> PlayerPositions => Array.Empty<Vector3>();
            public IReadOnlyList<Pose> PlayerPoses => Array.Empty<Pose>();
            public IReadOnlyList<WorldObjectState> ReplayObjects =>
                Array.Empty<WorldObjectState>();
        }

        private sealed class FakeNetworkAuthority : INetworkMatchAuthority
        {
            private readonly IReadOnlyDictionary<string, Pose> poses;

            public FakeNetworkAuthority(
                double serverTime,
                IReadOnlyDictionary<string, Pose> poses)
            {
                ServerTime = serverTime;
                this.poses = poses;
            }

            public bool IsServer => true;
            public int DestructionLimit => PlaySettingsDraft.DefaultDestructionLimit;
            public double ServerTime { get; set; }
            public MatchSessionCoordinator BoundSession { get; private set; }
            public List<int> InitializedAssignmentPlayers { get; } = new();
            public List<int> PublishedAssignmentPlayers { get; } = new();
            public IReadOnlyList<HighlightReplayData> HighlightReplay { get; private set; }
            public List<MatchStateSnapshot> Snapshots { get; } = new();
            public Dictionary<int, bool> Controls { get; } = new();
            public List<int> TeleportedPlayers { get; } = new();
            public List<Pose> TeleportedPoses { get; } = new();
            public int UnbindCount { get; private set; }

            public event Action<IReadOnlyList<MatchParticipant>> LineUpReceived;
            public event Action SimulationTick;

            public bool TryGetPlayerPose(string playerId, out Pose pose) =>
                poses.TryGetValue(playerId, out pose);

            public bool BindMatchSession(
                MatchSessionCoordinator session,
                Pose shredderEjectionPose)
            {
                BoundSession = session;
                return true;
            }

            public bool UnbindMatchSession(MatchSessionCoordinator session)
            {
                if (!ReferenceEquals(BoundSession, session))
                {
                    return false;
                }

                BoundSession = null;
                UnbindCount++;
                return true;
            }

            public bool TryPublishMatchState(MatchStateSnapshot snapshot)
            {
                Snapshots.Add(snapshot);
                return true;
            }

            public bool TryPublishItemAssignments(
                IReadOnlyList<PlayerItemAssignment> assignments)
            {
                foreach (var assignment in assignments)
                {
                    PublishedAssignmentPlayers.Add(assignment.PlayerIndex);
                }

                return true;
            }

            public bool TryInitializeAssignedItems(
                IReadOnlyList<PlayerItemAssignment> assignments)
            {
                foreach (var assignment in assignments)
                {
                    InitializedAssignmentPlayers.Add(assignment.PlayerIndex);
                }

                return true;
            }

            public bool TryPublishHighlightReplay(
                IReadOnlyList<HighlightReplayData> replay)
            {
                HighlightReplay = replay;
                return true;
            }

            public bool TrySetPlayerControls(int playerIndex, bool enabled)
            {
                Controls[playerIndex] = enabled;
                return true;
            }

            public bool TryTeleportPlayer(int playerIndex, Pose pose)
            {
                TeleportedPlayers.Add(playerIndex);
                TeleportedPoses.Add(pose);
                return true;
            }

            public void PublishLineUp(IReadOnlyList<MatchParticipant> participants)
            {
                LineUpReceived?.Invoke(participants);
            }

            public void PublishSimulationTick()
            {
                SimulationTick?.Invoke();
            }
        }
    }
}
