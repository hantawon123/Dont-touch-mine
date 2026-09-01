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
using UnityEngine.TestTools;

namespace Game.Architecture.Tests
{
    public sealed class NetworkMatchRuntimeCoordinatorTests
    {
        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void Migration_ResumesWithoutStartingOrInitializingItemsAgain(bool hostLeft, bool invalidCheckpoint)
        {
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();
            try
            {
                var participants = new[] { new MatchParticipant("host", 0), new MatchParticipant("client", 1) };
                var factory = new MatchRuntimeFactory(rules);
                using var original = factory.CreateSessionFromParticipants(participants, new AcceptAllPlacements(),
                    CreateSpawnPoints(), CreateItems(), new System.Random(7));
                original.Session.Start(10d);
                original.Session.TryInitializeAssignedItem(0);
                var pose = new Pose(new Vector3(8, 2, 4), Quaternion.identity);
                var checkpoint = new MatchMigrationState
                {
                    CapturedAt = 20d, Phase = original.Session.CaptureStateSnapshot(),
                    Players = new[] { original.Session.CaptureMigrationPlayer(0, pose),
                        original.Session.CaptureMigrationPlayer(1, Pose.identity) },
                    Objects = new[] { new MatchMigrationObject { ObjectId = original.Session.Assignments[0].Item.ItemId,
                        Holder = 0, Pose = pose } },
                };
                var poses = new Dictionary<string, Pose> { ["host"] = pose, ["client"] = Pose.identity };
                if (hostLeft) poses.Remove("host");
                var network = new FakeNetworkAuthority(100d, poses)
                    { MatchMigration = checkpoint, IsRuntimeReady = false };
                if (invalidCheckpoint) checkpoint.Players[1].ItemId = "missing-item";
                using var room = new RoomBrowserSystem();
                var flow = new AppFlowSystem();
                flow.TryTransitionTo(AppFlowState.Lobby);
                flow.TryTransitionTo(AppFlowState.InGame);
                using var coordinator = new NetworkMatchRuntimeCoordinator(network, factory, new FakeSceneContext(), flow,
                    new NetworkMatchRuntimeConfiguration(new AcceptAllPlacements(), CreateSpawnPoints(), CreateItems(),
                        Array.Empty<WorldObjectState>(), Pose.identity, CreateWaitingPoints()), room);
                coordinator.Start();
                network.PublishLineUp(participants);
                network.PublishSimulationTick();
                Assert.That(network.BoundSession, Is.Null, "Ordinary ticks must stay paused during migration.");
                network.IsMatchRuntimeRestorePending = true;
                Assert.DoesNotThrow(() => network.PublishSimulationTick());
                Assert.That(network.RestoreReports, Is.EqualTo(1));
                Assert.That(network.IsMatchRuntimeRestorePending, Is.False);
                if (invalidCheckpoint)
                {
                    Assert.That(network.RestoreFailure, Is.Not.Null);
                    Assert.That(network.BoundSession, Is.Null);
                    Assert.DoesNotThrow(() => network.PublishSimulationTick());
                    Assert.That(network.RestoreReports, Is.EqualTo(1));
                    return;
                }
                Assert.That(network.RestoreFailure, Is.Null);
                Assert.That(network.IsRuntimeReady, Is.False, "Only the migration owner may resume gameplay.");
                network.IsRuntimeReady = true;
                network.PublishSimulationTick();
                Assert.That(network.BoundSession.CurrentPhase, Is.EqualTo(MatchPhase.Hiding));
                Assert.That(network.BoundSession.GetRemainingSeconds(100d), Is.EqualTo(hostLeft ? 30d : 50d));
                Assert.That(network.InitializedAssignmentPlayers, Is.EqualTo(hostLeft ? new[] { 1 } : Array.Empty<int>()));
                Assert.That(network.PublishedAssignmentPlayers, Is.EqualTo(hostLeft ? new[] { 1, 1 } : new[] { 0, 1 }));
                if (hostLeft)
                {
                    Assert.That(network.BoundSession.Players.ActivePlayerCount, Is.EqualTo(1));
                    Assert.That(network.BoundSession.TryGetResult(out _), Is.False);
                    Assert.That(network.BoundSession.TryGetHeldObjectId(0, out _), Is.False);
                    Assert.That(network.BoundSession.TryGetItemPlacement(0, out var dropped), Is.True);
                    Assert.That(dropped.Pose.position, Is.EqualTo(pose.position));
                    Assert.That(network.Controls[1], Is.True);
                    Assert.That(flow.CurrentState, Is.EqualTo(AppFlowState.InGame));
                }
                else Assert.That(network.TeleportedPoses[0].position, Is.EqualTo(pose.position));
                var teleports = network.TeleportedPlayers.Count;
                network.PublishSceneLoaded();
                network.PublishSimulationTick();
                Assert.That(network.TeleportedPlayers.Count, Is.EqualTo(teleports));
            }
            finally { UnityEngine.Object.DestroyImmediate(rules); }
        }

        [TestCase(MatchPhase.Hiding)]
        [TestCase(MatchPhase.Searching)]
        [TestCase(MatchPhase.Highlight)]
        [TestCase(MatchPhase.Result)]
        public void DepartedAvatar_IsNotSentControlCommands(MatchPhase departurePhase)
        {
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();
            try
            {
                var poses = new Dictionary<string, Pose> { ["host"] = Pose.identity, ["client"] = Pose.identity };
                var network = new FakeNetworkAuthority(10d, poses);
                using var room = new RoomBrowserSystem();
                var appFlow = new AppFlowSystem();
                appFlow.TryTransitionTo(AppFlowState.Lobby);
                appFlow.TryTransitionTo(AppFlowState.InGame);
                using var coordinator = new NetworkMatchRuntimeCoordinator(network,
                    new MatchRuntimeFactory(rules), new FakeSceneContext(), appFlow,
                    new NetworkMatchRuntimeConfiguration(new AcceptAllPlacements(), CreateSpawnPoints(),
                        CreateItems(), Array.Empty<WorldObjectState>(), Pose.identity, CreateWaitingPoints()), room);
                coordinator.Start();
                network.PublishLineUp(new[] { new MatchParticipant("host", 0), new MatchParticipant("client", 1) });
                network.PublishSimulationTick();
                var session = network.BoundSession;
                if (departurePhase != MatchPhase.Hiding)
                {
                    network.ServerTime = session.CaptureStateSnapshot().PhaseEndsAt;
                    network.PublishSimulationTick();
                }
                if (departurePhase == MatchPhase.Highlight || departurePhase == MatchPhase.Result)
                {
                    session.SetHighlightCandidates(new[] {
                        new HighlightCandidate(HighlightType.FirstBlood, network.ServerTime, network.ServerTime + 4d, "client") });
                    network.ServerTime = session.CaptureStateSnapshot().PhaseEndsAt;
                    network.PublishSimulationTick();
                    if (departurePhase == MatchPhase.Result)
                        while (session.CompleteCurrentHighlight()) { }
                }
                Assert.That(session.CurrentPhase, Is.EqualTo(departurePhase));
                var hadResult = session.TryGetResult(out var originalResult);
                Assert.That(session.TryHandlePlayerLeft(1, Pose.identity, network.ServerTime), Is.True);
                Assert.That(session.TryHandlePlayerLeft(1, Pose.identity, network.ServerTime), Is.False);
                poses.Remove("client");
                network.MissingControlPlayers.Add(1);
                network.ControlCalls.Clear();
                Assert.DoesNotThrow(() => network.PublishSimulationTick());
                Assert.DoesNotThrow(() => network.PublishSimulationTick());
                Assert.That(network.ControlCalls, Has.No.Member(1));
                Assert.That(session.Players.IsActive(1), Is.False);
                if (hadResult)
                {
                    Assert.That(session.TryGetResult(out var afterLeave), Is.True);
                    Assert.That(afterLeave.EndReason, Is.EqualTo(originalResult.EndReason));
                    Assert.That(afterLeave.WinnerPlayerIndices, Is.EqualTo(originalResult.WinnerPlayerIndices));
                }
                else
                {
                    Assert.That(session.CurrentPhase, Is.EqualTo(departurePhase));
                    Assert.That(session.TryGetResult(out _), Is.False);
                    Assert.That(session.Players.ActivePlayerCount, Is.EqualTo(1));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(rules); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Authority_StartsTicksPublishesAndReleasesMatchRuntime(bool readinessTimeout)
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
                // 숨기기 페이즈: 숨기는 사람과 밖의 대기자 모두 조작 가능.
                Assert.That(network.Controls[0], Is.True);
                Assert.That(network.Controls[1], Is.True);
                Assert.That(network.TeleportedPlayers, Is.EqualTo(new[] { 1, 0 }));
                Assert.That(network.TeleportedPoses[0].position.z, Is.EqualTo(-10f));
                Assert.That(network.TeleportedPoses[1].position.z, Is.EqualTo(0f));

                network.ServerTime = 10d + rules.HidingTurnDurationSeconds;
                network.PublishSimulationTick();

                // 턴 교대 후에도 두 플레이어 모두 조작 가능.
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
                    Is.EqualTo(new[] { 1, 0, 1, 0 }));
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
                    Is.EqualTo(new[] { 1, 0, 1, 0, 0, 1 }));

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
                Assert.That(network.Snapshots[2].PhaseEndsAt, Is.Zero);
                Assert.That(network.HighlightReplay, Is.Null.Or.Empty);
                Assert.That(network.Controls[0], Is.True);
                Assert.That(network.Controls[1], Is.True);
                network.ServerTime += MatchSessionCoordinator.HighlightPostRollSeconds;
                network.PublishSimulationTick();
                Assert.That(network.Controls[0], Is.False);
                Assert.That(network.Controls[1], Is.False);
                Assert.That(network.HighlightReplay.Count, Is.EqualTo(1));
                Assert.That(
                    network.HighlightReplay[0].Candidate.Type,
                    Is.EqualTo(HighlightType.FirstBlood));
                Assert.That(
                    network.HighlightReplay[0].Clips[0].Frames,
                    Is.Not.Empty);

                // A transfer taking longer than the old two-second grace must not
                // consume any of the highlight's running time.
                network.ServerTime += 10d;
                network.PublishSimulationTick();
                Assert.That(network.BoundSession.CurrentPhase, Is.EqualTo(MatchPhase.Highlight));
                Assert.That(network.BoundSession.CaptureStateSnapshot().PhaseEndsAt, Is.Zero);

                if (readinessTimeout)
                {
                    network.ServerTime += 30d;
                    LogAssert.Expect(LogType.Warning, "[Highlight] Replay preparation timed out; skipping to results.");
                    network.PublishSimulationTick();
                }
                else
                {
                    network.IsHighlightReplayReady = true;
                    network.PublishSimulationTick();
                    Assert.That(network.Snapshots[3].Phase, Is.EqualTo(MatchPhase.Highlight));
                    Assert.That(network.Snapshots[3].PhaseEndsAt, Is.EqualTo(network.ServerTime +
                        HighlightPresentationTiming.ReadyLeadSeconds + 4d +
                        HighlightPresentationTiming.OverheadSeconds).Within(0.001));
                    network.ServerTime = network.Snapshots[3].PhaseEndsAt;
                    network.PublishSimulationTick();
                }

                Assert.That(network.Snapshots, Has.Count.EqualTo(readinessTimeout ? 4 : 5));
                Assert.That(network.Snapshots[network.Snapshots.Count - 1].Phase, Is.EqualTo(MatchPhase.Result));

                coordinator.Dispose();

                Assert.That(network.BoundSession, Is.Null);
                Assert.That(network.UnbindCount, Is.EqualTo(1));
                Assert.That(network.Controls[0], Is.True);
                Assert.That(network.Controls[1], Is.True);
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
            public MatchMigrationState MatchMigration { get; set; }
            public bool IsMatchRuntimeRestorePending { get; set; }
            public int RestoreReports { get; private set; }
            public Exception RestoreFailure { get; private set; }
            public void ReportMatchRuntimeRestored(Exception failure)
            {
                RestoreReports++;
                RestoreFailure = failure;
                IsMatchRuntimeRestorePending = false;
            }
            public bool IsRuntimeReady { get; set; } = true;
            public bool IsHighlightReplayReady { get; set; }
            public int DestructionLimit => PlaySettingsDraft.DefaultDestructionLimit;
            public double ServerTime { get; set; }
            public MatchSessionCoordinator BoundSession { get; private set; }
            public List<int> InitializedAssignmentPlayers { get; } = new();
            public List<int> PublishedAssignmentPlayers { get; } = new();
            public IReadOnlyList<HighlightReplayData> HighlightReplay { get; private set; }
            public List<MatchStateSnapshot> Snapshots { get; } = new();
            public Dictionary<int, bool> Controls { get; } = new();
            public HashSet<int> MissingControlPlayers { get; } = new();
            public List<int> ControlCalls { get; } = new();
            public List<int> TeleportedPlayers { get; } = new();
            public List<Pose> TeleportedPoses { get; } = new();
            public int UnbindCount { get; private set; }

            public event Action<IReadOnlyList<MatchParticipant>> LineUpReceived;
            public event Action SimulationTick;
            public event Action SceneLoaded;

            public void PublishSceneLoaded() => SceneLoaded?.Invoke();

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
                ControlCalls.Add(playerIndex);
                if (MissingControlPlayers.Contains(playerIndex)) return false;
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
