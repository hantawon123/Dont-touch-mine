using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Client.Combat;
using Game.Client.Interactions;
using Game.Client.Players;
using Game.Core.Match;
using Game.Core.Lobby;
using Game.Core.Players;
using Game.Network.Match;
using Game.Network;
using Game.Network.Players;
using Game.Network.Session;
using Game.Server.Items;
using Game.Server.Match;
using Game.SOAP.Config;
using Fusion.Addons.KCC;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using NetworkTransform = Fusion.NetworkTransform;

namespace Game.Architecture.Tests
{
    public sealed class NetworkContractTests
    {
        [TestCase(false, true, 2, 6, 5, "playground", "food", false)]
        [TestCase(true, false, 2, 6, 5, "playground", "food", false)]
        [TestCase(true, true, 3, 2, 5, "playground", "food", false)]
        [TestCase(true, true, 2, 6, -1, "playground", "food", false)]
        [TestCase(true, true, 2, 6, 0, "playground", "food", true)]
        [TestCase(true, true, 2, 6, 5, "missing", "food", false)]
        [TestCase(true, true, 2, 6, 5, "playground", "unsupported", false)]
        [TestCase(true, true, 2, 6, 5, "playground", "food", true)]
        public void LobbySettingsValidation_EnforcesAuthorityRangesAndCategory(
            bool hasAuthority,
            bool hasValidSession,
            int currentPlayerCount,
            int maxPlayers,
            int destructionLimit,
            string mapId,
            string categoryId,
            bool expected)
        {
            Assert.That(
                MatchRuleSettings.TryCreate(
                    60,
                    5,
                    1.5f,
                    4,
                    categoryId,
                    out var rules,
                    out _),
                Is.True);

            Assert.That(
                NetworkRunnerService.TryValidateLobbySettingsRequest(
                    hasAuthority,
                    hasValidSession,
                    currentPlayerCount,
                    maxPlayers,
                    destructionLimit,
                    mapId,
                    rules,
                    out _),
                Is.EqualTo(expected));
        }

        [TestCase(Game.Core.Flow.AppFlowState.Lobby)]
        [TestCase(Game.Core.Flow.AppFlowState.InGame)]
        [TestCase(Game.Core.Flow.AppFlowState.Highlight)]
        [TestCase(Game.Core.Flow.AppFlowState.Result)]
        public void RoomDisconnect_LeavesEveryPhaseOnceOutsideCallback(Game.Core.Flow.AppFlowState phase)
        {
            using var room = new Game.Core.Lobby.RoomBrowserSystem();
            var network = new NetworkRunnerService(null, null, null, null, null, null);
            var flow = new Game.Core.Flow.AppFlowSystem();
            flow.TryTransitionTo(Game.Core.Flow.AppFlowState.Lobby);
            flow.TryRestoreSessionState(phase);
            var application = new DisconnectApplicationSpy();
            using var controller = new Game.Bootstrap.NetworkRoomDisconnectController(network, room, flow, application);
            controller.Start();
            room.RoomClosed(Game.Core.Rooms.RoomExitReason.HostClosed);
            Assert.That(application.OpenCount, Is.Zero, "Do not load scenes inside Fusion callbacks.");
            Assert.That(flow.CurrentState, Is.EqualTo(phase));
            controller.Tick();
            controller.Tick();
            room.RoomClosed(Game.Core.Rooms.RoomExitReason.HostClosed);
            controller.Tick();
            Assert.That(application.OpenCount, Is.EqualTo(1));
            Assert.That(flow.CurrentState, Is.EqualTo(Game.Core.Flow.AppFlowState.RoomBrowser));
            Assert.That(room.LastExit.CurrentValue, Is.EqualTo(Game.Core.Rooms.RoomExitReason.HostClosed),
                "The next browser view must still receive the reason.");
        }

        [Test]
        public void RoomDisconnect_VoluntaryDepartureAlsoWaitsForTick_AndDisposalStopsNavigation()
        {
            using var room = new Game.Core.Lobby.RoomBrowserSystem();
            var flow = new Game.Core.Flow.AppFlowSystem();
            flow.TryTransitionTo(Game.Core.Flow.AppFlowState.Lobby);
            var application = new DisconnectApplicationSpy();
            var controller = new Game.Bootstrap.NetworkRoomDisconnectController(
                new NetworkRunnerService(null, null, null, null, null, null), room, flow, application);
            controller.Start();
            room.RoomClosed(Game.Core.Rooms.RoomExitReason.Left);
            Assert.That(application.OpenCount, Is.Zero);
            controller.Tick();
            Assert.That(application.OpenCount, Is.EqualTo(1));
            Assert.That(room.LastExit.CurrentValue, Is.EqualTo(Game.Core.Rooms.RoomExitReason.Left));
            flow.TryTransitionTo(Game.Core.Flow.AppFlowState.Lobby);
            controller.Dispose();
            room.RoomClosed(Game.Core.Rooms.RoomExitReason.HostClosed);
            controller.Tick();
            Assert.That(application.OpenCount, Is.EqualTo(1));
        }

        [Test]
        public void RoomDisconnect_UnexpectedClientLossReportsHostConnectionLost()
        {
            foreach (Game.Core.Rooms.RoomExitReason reason in Enum.GetValues(typeof(Game.Core.Rooms.RoomExitReason)))
            {
                Assert.That(NetworkRunnerService.ResolveUnexpectedExit(true, reason),
                    Is.EqualTo(Game.Core.Rooms.RoomExitReason.HostClosed));
                Assert.That(NetworkRunnerService.ResolveUnexpectedExit(false, reason), Is.EqualTo(reason));
            }
        }

        [Test]
        public void RoomDisconnectLifecycle_WaitsForRunnerDestructionAndRetainsFirstReason()
        {
            using var room = new Game.Core.Lobby.RoomBrowserSystem();
            var network = new NetworkRunnerService(null, room, null, null, null, null);
            var flow = new Game.Core.Flow.AppFlowSystem();
            flow.TryTransitionTo(Game.Core.Flow.AppFlowState.Lobby);
            flow.TryRestoreSessionState(Game.Core.Flow.AppFlowState.Result);
            var application = new DisconnectApplicationSpy();
            using var controller = new Game.Bootstrap.NetworkRoomDisconnectController(network, room, flow, application);
            var runnerObject = new GameObject("Disconnect Lifecycle Test");
            try
            {
                var runner = runnerObject.AddComponent<Fusion.NetworkRunner>();
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(NetworkRunnerService).GetField("_runner", flags).SetValue(network, runner);
                typeof(NetworkRunnerService).GetField("_exitReported", flags).SetValue(network, false);
                typeof(NetworkRunnerService).GetField("_isClientSession", flags).SetValue(network, true);
                controller.Start();
                network.OnDisconnectedFromServer(runner, Fusion.Sockets.NetDisconnectReason.Timeout);
                typeof(NetworkRunnerService).GetMethod("ReportPlayerCount", flags).Invoke(network, null);
                Assert.That(room.LastExit.CurrentValue, Is.EqualTo(Game.Core.Rooms.RoomExitReason.HostClosed));
                controller.Tick();
                Assert.That(application.OpenCount, Is.Zero);
                network.OnShutdown(runner, Fusion.ShutdownReason.Ok);
                controller.Tick();
                Assert.That(application.OpenCount, Is.Zero, "OnShutdown is not the end of Unity object destruction.");
                UnityEngine.Object.DestroyImmediate(runnerObject);
                controller.Tick();
                Assert.That(application.OpenCount, Is.EqualTo(1));
                Assert.That(room.LastExit.CurrentValue, Is.EqualTo(Game.Core.Rooms.RoomExitReason.HostClosed));
            }
            finally
            {
                if (runnerObject != null) UnityEngine.Object.DestroyImmediate(runnerObject);
            }
        }

        [Test]
        public void RoomDisconnectConfig_DisablesMigrationWithoutChangingHeapSettings()
        {
            var source = new Fusion.NetworkProjectConfig();
            source.HostMigration.EnableAutoUpdate = true;
            var pageShift = source.Heap.PageShift;
            var session = NetworkRunnerService.ConfigureSession(source);
            Assert.That(session.HostMigration.EnableAutoUpdate, Is.False);
            Assert.That(session.Heap.PageShift, Is.EqualTo(pageShift));
        }

        private sealed class DisconnectApplicationSpy : Game.Client.Home.IHomeApplicationHost
        {
            public int OpenCount { get; private set; }
            public void OpenRoomBrowser() => OpenCount++;
            public void Quit() { }
            public void OpenHome() { }
            public void OpenLobby() { }
        }

        [Test]
        public void LobbyEntry_WaitsForPlacementAndCameraFrames_AndClearsOnExit()
        {
            var view = new EntryTransitionSpy();
            var binder = new Game.Bootstrap.LobbyPlayerCameraBinder(
                new NetworkRunnerService(null, null, null, null, null, null), view);
            binder.UpdateEntryTransition(false, 100);
            Assert.That(view.Opacity, Is.EqualTo(1f));
            binder.UpdateEntryTransition(true, 101);
            binder.UpdateEntryTransition(true, 102);
            Assert.That(view.Opacity, Is.EqualTo(1f), "Do not reveal before camera rendering catches up.");
            binder.UpdateEntryTransition(false, 103);
            binder.UpdateEntryTransition(true, 104);
            Assert.That(view.Opacity, Is.EqualTo(1f), "Losing readiness restarts the render wait.");
            binder.UpdateEntryTransition(true, 106);
            Assert.That(view.Opacity, Is.Zero);
            binder.UpdateEntryTransition(false, 107);
            Assert.That(view.Opacity, Is.Zero, "A completed entry does not cover later gameplay or migration.");

            var nextVisit = new Game.Bootstrap.LobbyPlayerCameraBinder(
                new NetworkRunnerService(null, null, null, null, null, null), view);
            nextVisit.UpdateEntryTransition(false, 200);
            Assert.That(view.Opacity, Is.EqualTo(1f), "Re-entry starts a fresh placement wait.");
            nextVisit.Dispose();
            Assert.That(view.Opacity, Is.Zero, "Leaving before placement must not leave a black screen.");
        }

        private sealed class EntryTransitionSpy : Game.Client.Match.IHighlightTransitionView
        {
            public float Opacity { get; private set; }
            public void SetOpacity(float opacity) => Opacity = opacity;
        }

        [TestCase(false, true, false, 0d, false)]
        [TestCase(false, false, true, 10d, false)]
        [TestCase(true, false, false, 4.9d, false)]
        [TestCase(true, true, false, 0d, true)]
        [TestCase(true, false, true, 0d, true)]
        [TestCase(true, false, false, 5d, true)]
        public void HostMigration_RevealsOnlyAfterRuntimeAndCameraRecovery(
            bool runtimeReady, bool cameraReady, bool resultScene, double elapsed, bool expected)
        {
            Assert.That(Game.Bootstrap.HostMigrationPresentationController.CanReveal(
                runtimeReady, cameraReady, resultScene, elapsed), Is.EqualTo(expected));
        }

        [TestCase(PlayerPosture.Standing, 1.8f)]
        [TestCase(PlayerPosture.Crouching, 1.2f)]
        [TestCase(PlayerPosture.Prone, 0.6f)]
        public void HostMigration_MotorPreservesSavedPostureAndColliderHeight(PlayerPosture saved, float height)
        {
            var settings = new PlayerMovementSettings(4f, 7f, 720f, 1.1f, 2f,
                2f, 0.8f, 1.8f, 1.2f, 0.6f);
            var restored = NetworkPlayerMotor.ResolveSpawnPosture(true, saved);
            Assert.That(restored, Is.EqualTo(saved));
            Assert.That(NetworkPlayerMotor.HeightForPosture(restored, settings), Is.EqualTo(height));
            Assert.That(NetworkPlayerMotor.ResolvePosture(restored, true, default, default), Is.EqualTo(saved),
                "The first idle input tick must not stand up after restoration.");
            Assert.That(NetworkPlayerMotor.ResolveSpawnPosture(false, saved), Is.EqualTo(PlayerPosture.Standing),
                "A newly spawned character still starts standing.");
        }

        [TestCase(MatchPhase.Waiting, false, MatchPhase.Waiting)]
        [TestCase(MatchPhase.Hiding, false, MatchPhase.Hiding)]
        [TestCase(MatchPhase.Searching, false, MatchPhase.Searching)]
        [TestCase(MatchPhase.Highlight, true, MatchPhase.Result)]
        [TestCase(MatchPhase.Result, true, MatchPhase.Result)]
        public void HostMigration_RoutesFromCheckpointRatherThanDepartingScene(
            MatchPhase savedPhase, bool hasResult, MatchPhase expected)
        {
            Assert.That(NetworkRunnerService.ResolveHostMigrationPhase(savedPhase, hasResult), Is.EqualTo(expected));
            var expectedScene = Fusion.SceneRef.FromIndex(expected == MatchPhase.Waiting ? 1 :
                expected == MatchPhase.Result ? 3 : 2);
            foreach (var previousScene in new[] { 1, 2, 3 })
            {
                Fusion.NetworkSceneInfo info = Fusion.SceneRef.FromIndex(previousScene);
                Assert.That(NetworkRunnerService.IsOnlyScene(info, expectedScene),
                    Is.EqualTo(info.Scenes[0] == expectedScene));
            }
            Assert.That(NetworkRunnerService.IsOnlyScene(default, expectedScene), Is.False);
            Assert.That(NetworkRunnerService.IsOnlyScene(expectedScene, default), Is.False);
        }

        [TestCase(MatchPhase.Highlight, false)]
        [TestCase(MatchPhase.Result, false)]
        [TestCase(MatchPhase.Waiting, true)]
        [TestCase(MatchPhase.Hiding, true)]
        [TestCase(MatchPhase.Searching, true)]
        [TestCase((MatchPhase)99, false)]
        public void HostMigration_RejectsInconsistentCheckpoint(MatchPhase phase, bool hasResult)
        {
            Assert.Throws<InvalidOperationException>(() => NetworkRunnerService.ResolveHostMigrationPhase(phase, hasResult));
        }

        [Test]
        public void HostMigration_StageWaitHasAnUnscaledDeadline()
        {
            Assert.That(NetworkRunnerService.IsHostMigrationStageComplete(false, 59.9d, "runtime"), Is.False);
            var error = Assert.Throws<TimeoutException>(() =>
                NetworkRunnerService.IsHostMigrationStageComplete(false, 60d, "runtime"));
            Assert.That(error.Message, Does.Contain("runtime"));
            Assert.That(NetworkRunnerService.IsHostMigrationStageComplete(true, 60d, "runtime"), Is.True);
        }

        [Test]
        public void AssignmentRecipient_LateReadyRequestOnlyGetsItsPublishedAssignment()
        {
            var playing = new[] { new MatchParticipant("host", 0), new MatchParticipant("late", 1),
                new MatchParticipant("not-published", 2) };
            var published = new Dictionary<string, string> { ["host"] = "host-item", ["late"] = "late-item" };
            Assert.That(NetworkRunnerService.TryGetPublishedAssignment(published, playing, "late", out var item), Is.True);
            Assert.That(item, Is.EqualTo("late-item"));
            Assert.That(NetworkRunnerService.TryGetPublishedAssignment(published, playing, "late", out item), Is.True,
                "Repeated ready requests must remain safe if the previous response arrived too early.");
            Assert.That(item, Is.EqualTo("late-item"));
            Assert.That(NetworkRunnerService.TryGetPublishedAssignment(published, playing, "not-published", out _), Is.False,
                "Do not reveal an assignment before the authority publishes it.");
            Assert.That(NetworkRunnerService.TryGetPublishedAssignment(published, playing, "newcomer", out _), Is.False);
            Assert.That(NetworkRunnerService.TryGetPublishedAssignment(published, Array.Empty<MatchParticipant>(), "late", out _), Is.False);
            published.Clear(); // Runner replacement or rematch reset.
            Assert.That(NetworkRunnerService.TryGetPublishedAssignment(published, playing, "late", out _), Is.False);
        }

        [TestCase(false, false, false, false)]
        [TestCase(false, false, true, false)]
        [TestCase(false, true, false, false)]
        [TestCase(false, true, true, false)]
        [TestCase(true, false, false, true)]
        [TestCase(true, false, true, false)]
        [TestCase(true, true, false, false)]
        [TestCase(true, true, true, false)]
        public void HostMigration_CompletesOnlyAfterFusionAndSceneAreReady(
            bool running, bool resuming, bool sceneBusy, bool expected)
        {
            Assert.That(NetworkRunnerService.CanCompleteHostMigration(running, resuming, sceneBusy),
                Is.EqualTo(expected));
        }

        [Test]
        public void HostMigration_RestoresSeatsBeforeCreatingMissingPlayers()
        {
            var registry = new PlayerRegistry();
            var newHost = Fusion.PlayerRef.FromIndex(1);
            var newcomer = Fusion.PlayerRef.FromIndex(2);

            // StartGame succeeded, but the scene and snapshot have not resumed yet.
            if (NetworkRunnerService.CanCompleteHostMigration(false, true, true))
                registry.Add(newHost);
            Assert.That(registry.Count, Is.Zero, "Do not allocate seat 0 before restoring seat 1.");

            var failure = NetworkRunnerService.TryRestoreHostMigrationSnapshot(() =>
            {
                Assert.That(registry.Restore(newHost, 1), Is.True);
            });
            Assert.That(failure, Is.Null);
            Assert.That(NetworkRunnerService.CanCompleteHostMigration(true, true, false), Is.False,
                "Returning from our callback alone does not finish Fusion initialization.");

            if (NetworkRunnerService.CanCompleteHostMigration(true, false, false))
            {
                Assert.That(registry.Add(newHost), Is.EqualTo(1));
                Assert.That(registry.Add(newcomer), Is.Zero);
            }
            Assert.That(registry.Count, Is.EqualTo(2));
        }

        [Test]
        public void HostMigration_RestoreFailureReturnsToTheMigrationOwner()
        {
            var expected = new InvalidOperationException("Could not restore player 2.");
            Exception failure = null;
            Assert.DoesNotThrow(() => failure = NetworkRunnerService.TryRestoreHostMigrationSnapshot(
                () => throw expected));
            Assert.That(failure, Is.SameAs(expected),
                "A coroutine callback failure must reach migration cleanup instead of escaping Fusion.");
        }

        [Test]
        public void RoomStatePrefabs_EachFitTheLegacyHeapPage()
        {
            var sessionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Content/Prefabs/MatchSession.prefab");
            var checkpointPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Content/Prefabs/MatchMigrationCheckpoint.prefab");
            Assert.That(sessionPrefab, Is.Not.Null);
            Assert.That(checkpointPrefab, Is.Not.Null);
            Assert.That(sessionPrefab.GetComponent<MatchSessionState>(), Is.Not.Null);
            Assert.That(sessionPrefab.GetComponent<MatchMigrationCheckpoint>(), Is.Null);
            Assert.That(checkpointPrefab.GetComponent<MatchMigrationCheckpoint>(), Is.Not.Null);
            var prefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabs>(
                "Assets/_Game/Content/Settings/NetworkPrefabs.asset");
            Assert.That(prefabs.MatchSession.gameObject, Is.SameAs(sessionPrefab));
            Assert.That(prefabs.MatchMigrationCheckpoint.gameObject, Is.SameAs(checkpointPrefab));
            foreach (var prefab in new[] { sessionPrefab, checkpointPrefab })
            {
                var wordCount = Fusion.NetworkObject.GetWordCount(
                    prefab.GetComponent<Fusion.NetworkObject>());
                Assert.That(wordCount, Is.GreaterThan(Fusion.NetworkObjectHeader.WORDS));
                Assert.DoesNotThrow(() =>
                    PlayerSpawner.ValidateRoomObjectStateSize(wordCount, 15));
            }
        }

        [TestCase(10499, 15, false)] // Observed 41,996-byte MatchSession vs the old 32 KiB page.
        [TestCase(10499, 16, true)]
        [TestCase(16384, 16, true)]
        [TestCase(16385, 16, false)]
        [TestCase(0, 16, false)]
        public void RoomObjectStateSize_RejectsOverflowBeforeSpawn(int words, int pageShift, bool fits)
        {
            if (fits)
                Assert.DoesNotThrow(() => PlayerSpawner.ValidateRoomObjectStateSize(words, pageShift));
            else
                Assert.Throws<InvalidOperationException>(() =>
                    PlayerSpawner.ValidateRoomObjectStateSize(words, pageShift));
        }

        [Test]
        public void RoomInitialization_SuccessDoesNotCleanUp()
        {
            var initialized = false;
            var result = NetworkRunnerService.CompleteRoomInitializationAsync(
                () => initialized = true,
                () => throw new InvalidOperationException("Successful rooms must remain running."))
                .GetAwaiter().GetResult();
            Assert.That(initialized, Is.True);
            Assert.That(result.Ok, Is.True);
        }

        [Test]
        public void RoomInitialization_FailureWaitsForCleanupBeforeRetry()
        {
            var cleanup = new UniTaskCompletionSource();
            var cleanupCalls = 0;
            var pending = NetworkRunnerService.CompleteRoomInitializationAsync(
                () => throw new InvalidOperationException("room object failed"),
                () => { cleanupCalls++; return cleanup.Task; });
            Assert.That(cleanupCalls, Is.EqualTo(1));
            Assert.That(pending.Status, Is.EqualTo(UniTaskStatus.Pending));
            cleanup.TrySetResult();
            var failure = pending.GetAwaiter().GetResult();
            Assert.That(failure.Ok, Is.False);
            Assert.That(failure.Failure, Is.EqualTo(SessionFailure.Unknown));
            Assert.That(failure.Detail, Is.EqualTo("room object failed"));
            var retry = NetworkRunnerService.CompleteRoomInitializationAsync(
                () => { }, () => { cleanupCalls++; return UniTask.CompletedTask; }).GetAwaiter().GetResult();
            Assert.That(retry.Ok, Is.True);
            Assert.That(cleanupCalls, Is.EqualTo(1));
        }

        [Test]
        public void RoomInitialization_CancellationAlsoCleansUp()
        {
            var cleaned = false;
            Assert.Throws<OperationCanceledException>(() =>
                NetworkRunnerService.CompleteRoomInitializationAsync(
                    () => throw new OperationCanceledException(),
                    () => { cleaned = true; return UniTask.CompletedTask; }).GetAwaiter().GetResult());
            Assert.That(cleaned, Is.True);
        }

        [Test]
        public void Participant_UsesOnlyStablePlayerIndex()
        {
            var participant = new MatchParticipant("player", 3);

            Assert.That(participant.PlayerIndex, Is.EqualTo(3));
            Assert.That(typeof(MatchParticipant).GetProperty("Seat"), Is.Null);
        }

        [Test]
        public void Participant_ConvertsRoomSeatOrderToContiguousPlayerIndices()
        {
            var participants = MatchParticipant.FromRoomParticipants(new[]
            {
                new Game.Core.Rooms.RoomParticipant("late-seat", 5, false),
                new Game.Core.Rooms.RoomParticipant("host", 0, true),
                new Game.Core.Rooms.RoomParticipant("middle-seat", 3, false),
            });

            Assert.That(
                Array.ConvertAll(participants, participant => participant.PlayerId),
                Is.EqualTo(new[] { "host", "middle-seat", "late-seat" }));
            Assert.That(
                Array.ConvertAll(participants, participant => participant.PlayerIndex),
                Is.EqualTo(new[] { 0, 1, 2 }));
        }

        [TestCase(2, 0)]
        [TestCase(3, 0)]
        [TestCase(3, 1)]
        [TestCase(6, 0)]
        public void AssignmentRecipient_AfterDepartureUsesFrozenLineUp(int playerCount, int leavingIndex)
        {
            var present = new List<Game.Core.Rooms.RoomParticipant>();
            for (var i = 0; i < playerCount; i++)
                present.Add(new Game.Core.Rooms.RoomParticipant($"player-{i}", i, i == 0));
            var playing = MatchParticipant.FromRoomParticipants(present);
            present.RemoveAt(leavingIndex);

            for (var i = 0; i < playerCount; i++)
            {
                var found = NetworkRunnerService.TryResolveAssignmentRecipient(playing, present, i, out var id);
                Assert.That(found, Is.EqualTo(i != leavingIndex));
                Assert.That(id, Is.EqualTo(i == leavingIndex ? null : $"player-{i}"));
            }

            // Reusing a room seat must never deliver the departed player's secret assignment.
            present.Add(new Game.Core.Rooms.RoomParticipant("newcomer", leavingIndex, false));
            Assert.That(NetworkRunnerService.TryResolveAssignmentRecipient(
                playing, present, leavingIndex, out var replacement), Is.False);
            Assert.That(replacement, Is.Null);
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void AssignmentRecipient_RejectsOutOfRangeMatchIndex(int playerIndex)
        {
            var present = new[]
            {
                new Game.Core.Rooms.RoomParticipant("host", 0, true),
                new Game.Core.Rooms.RoomParticipant("guest", 5, false),
            };
            var playing = MatchParticipant.FromRoomParticipants(present);
            Assert.That(NetworkRunnerService.TryResolveAssignmentRecipient(
                playing, present, playerIndex, out var id), Is.False);
            Assert.That(id, Is.Null);
        }

        [Test]
        public void InputIntent_NormalizesMovementYawAndButtons()
        {
            var input = new PlayerInputIntent(
                3f,
                4f,
                -90f,
                PlayerInputButtons.Jump | PlayerInputButtons.Sprint);

            Assert.That(input.MoveX, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(input.MoveY, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(input.LookYawDegrees, Is.EqualTo(270f));
            Assert.That(input.IsPressed(PlayerInputButtons.Jump), Is.True);
            Assert.That(input.IsPressed(PlayerInputButtons.Prone), Is.False);
        }

        [Test]
        public void InputIntent_RejectsInvalidNetworkValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PlayerInputIntent(float.NaN, 0f, 0f, PlayerInputButtons.None));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PlayerInputIntent(0f, 0f, 0f, (PlayerInputButtons)128));
        }

        [Test]
        public void NetworkInput_PreservesIntentAndUsesCameraYaw()
        {
            var intent = new PlayerInputIntent(
                0f,
                1f,
                90f,
                PlayerInputButtons.Jump |
                PlayerInputButtons.Sprint |
                PlayerInputButtons.Attack);
            var input = NetworkPlayerInput.FromIntent(intent);
            var direction = NetworkPlayerMotor.ToWorldDirection(
                input.Move,
                input.LookYawDegrees);

            Assert.That(input.IsPressed(NetworkPlayerButton.Jump), Is.True);
            Assert.That(input.IsPressed(NetworkPlayerButton.Sprint), Is.True);
            Assert.That(input.IsPressed(NetworkPlayerButton.Attack), Is.True);
            Assert.That(direction.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(direction.z, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void NetworkPlayerPrefab_HasAuthoritativeMovementComponents()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Content/Prefabs/NetworkedPlayer.prefab");

            Assert.That(prefab, Is.Not.Null, "NetworkedPlayer prefab is missing.");
            Assert.That(prefab.GetComponent<NetworkPlayerMotor>(), Is.Not.Null,
                "NetworkPlayerMotor is missing.");
            var kcc = prefab.GetComponent<KCC>();
            var rigidbody = prefab.GetComponent<Rigidbody>();
            var processor = prefab.GetComponent<PlayerKCCMovementProcessor>();

            Assert.That(kcc, Is.Not.Null,
                "KCC is missing.");
            Assert.That(rigidbody, Is.Not.Null,
                "KCC Rigidbody is missing.");
            Assert.That(rigidbody.isKinematic, Is.True);
            Assert.That(rigidbody.useGravity, Is.False);
            Assert.That(processor, Is.Not.Null,
                "Player KCC environment processor is missing.");
            Assert.That(kcc.Settings.Processors, Does.Contain(processor));
            var playerLayer = LayerMask.NameToLayer("Player");
            Assert.That(playerLayer, Is.GreaterThanOrEqualTo(0),
                "The Player layer is missing.");
            Assert.That(kcc.Settings.ColliderLayer, Is.EqualTo(playerLayer),
                "KCCCollider must use the Player layer so the third-person " +
                "camera does not treat its own body as a wall.");
            Assert.That(prefab.GetComponent<NetworkTransform>(), Is.Null,
                "KCC must be the only network transform writer.");
            Assert.That(prefab.GetComponent<CharacterController>().enabled, Is.False,
                "The inherited CharacterController must not compete with KCC.");
            Assert.That(
                prefab.GetComponent<PlayerMovement>(),
                Is.InstanceOf<IPlayerInputIntentSource>());
        }

        [Test]
        public void NetworkPlayerPrefab_LimitsLocalInputToItsOwner()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Content/Prefabs/NetworkedPlayer.prefab");
            var avatar = prefab.GetComponent<PlayerAvatar>();
            var ownerOnly = new SerializedObject(avatar).FindProperty("_ownerOnly");
            var types = new HashSet<Type>();

            for (var index = 0; index < ownerOnly.arraySize; index++)
            {
                var behaviour = ownerOnly.GetArrayElementAtIndex(index)
                    .objectReferenceValue as Behaviour;
                Assert.That(behaviour, Is.Not.Null);
                types.Add(behaviour.GetType());
            }

            Assert.That(types, Is.EquivalentTo(new[]
            {
                typeof(PlayerMovement),
                typeof(PlayerInteractor),
                typeof(ItemPlacementController),
                typeof(PlayerCombatant),
            }));
        }

        [Test]
        public void NetworkPlayer_UsesTheSameMovementSettingsAsLocalPlayer()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Content/Prefabs/NetworkedPlayer.prefab");
            var source = prefab.GetComponent<PlayerMovement>();
            var settings = source.MovementSettings;
            var config = AssetDatabase.LoadAssetAtPath<MovementConfigSO>(
                "Assets/_Game/Content/Config/MovementConfig.asset");

            Assert.That(config, Is.Not.Null);
            Assert.That(settings.WalkSpeed, Is.EqualTo(config.WalkSpeed));
            Assert.That(settings.SprintSpeed, Is.EqualTo(config.SprintSpeed));
            Assert.That(settings.RotationSpeedDegrees,
                Is.EqualTo(config.RotationSpeedDegrees));
            Assert.That(settings.JumpHeight, Is.EqualTo(config.JumpHeight));
            Assert.That(settings.GravityMultiplier,
                Is.EqualTo(config.GravityMultiplier));
            Assert.That(settings.CrouchSpeed, Is.EqualTo(config.CrouchSpeed));
            Assert.That(settings.ProneSpeed, Is.EqualTo(config.ProneSpeed));
            Assert.That(settings.StandHeight, Is.EqualTo(config.StandHeight));
            Assert.That(settings.CrouchHeight, Is.EqualTo(config.CrouchHeight));
            Assert.That(settings.ProneHeight, Is.EqualTo(config.ProneHeight));
            Assert.That(settings.MaxStamina, Is.EqualTo(config.MaxStamina));
            Assert.That(settings.StaminaDrainPerSecond,
                Is.EqualTo(config.StaminaDrainPerSecond));
            Assert.That(settings.StaminaRecoveryPerSecond,
                Is.EqualTo(config.StaminaRecoveryPerSecond));
        }

        [Test]
        public void NetworkPlayer_PostureInputChangesPoseAndMovementSpeed()
        {
            var crouchInput = NetworkPlayerInput.FromIntent(new PlayerInputIntent(
                0f,
                1f,
                0f,
                PlayerInputButtons.Crouch));
            var settings = new PlayerMovementSettings(
                4f, 7f, 720f, 1.1f, 2f, 2f, 0.8f, 1.8f, 1.2f, 0.6f);

            var posture = NetworkPlayerMotor.ResolvePosture(
                PlayerPosture.Standing,
                true,
                crouchInput,
                default);

            Assert.That(posture, Is.EqualTo(PlayerPosture.Crouching));
            Assert.That(
                NetworkPlayerMotor.MoveSpeedForPosture(settings, posture, true),
                Is.EqualTo(2f));
        }

        [Test]
        public void NetworkPlayer_SprintSpeedUsesRoomMultiplier()
        {
            var settings = new PlayerMovementSettings(4f, 7f, 720f, 1.1f, 2f);

            Assert.That(
                NetworkPlayerMotor.MoveSpeedForPosture(
                    settings,
                    PlayerPosture.Standing,
                    true,
                    1.5f),
                Is.EqualTo(10.5f));
        }

        [Test]
        public void SharedMovementKinematics_JumpRisesThenFallsContinuously()
        {
            var settings = new PlayerMovementSettings(4f, 7f, 720f, 1.1f, 2f);
            const float deltaTime = 1f / 60f;

            var takeoff = PlayerMovementKinematics.StepVerticalVelocity(
                0f, true, true, -9.81f, deltaTime, settings);
            var nextTick = PlayerMovementKinematics.StepVerticalVelocity(
                takeoff, false, false, -9.81f, deltaTime, settings);

            Assert.That(takeoff, Is.GreaterThan(0f));
            Assert.That(nextTick, Is.LessThan(takeoff));
            Assert.That(nextTick, Is.GreaterThan(0f));
        }

        [Test]
        public void MatchStateSnapshot_RejectsInvalidReplicatedState()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new MatchStateSnapshot((MatchPhase)99, 1d));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new MatchStateSnapshot(MatchPhase.Hiding, double.NaN));
        }

        [Test]
        public void MatchStarter_ForwardsReplicatedPhaseSnapshot()
        {
            var gameObject = new GameObject("MatchStarterTest");

            try
            {
                var starter = gameObject.AddComponent<MatchStarter>();
                var expected = new MatchStateSnapshot(MatchPhase.Searching, 120d);
                var received = new MatchStateSnapshot();
                var wasReceived = false;

                starter.MatchStateReceived += snapshot =>
                {
                    received = snapshot;
                    wasReceived = true;
                };

                starter.PublishSnapshot(expected);

                Assert.That(wasReceived, Is.True);
                Assert.That(received.Phase, Is.EqualTo(MatchPhase.Searching));
                Assert.That(received.PhaseEndsAt, Is.EqualTo(120d));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ShredderEjectionVelocity_UsesSpotForwardAndAddsLift()
        {
            var rotation = Quaternion.Euler(0f, 90f, 0f);

            var velocity = MatchStarter.CalculateShredderEjectionVelocity(rotation);

            Assert.That(velocity.y, Is.GreaterThan(0f));
            Assert.That(Vector3.Dot(velocity, rotation * Vector3.forward), Is.GreaterThan(0f));
        }

        [Test]
        public void ActionRequestRpcs_DeriveRequesterFromRpcInfo()
        {
            var names = new[]
            {
                "RPC_RequestHold",
                "RPC_RequestDrop",
                "RPC_RequestRelease",
                "RPC_RequestThrow",
                "RPC_RequestHit",
                "RPC_RequestShredder",
                "RPC_RequestReturnToLobby",
                "RPC_RequestMatchChat",
            };

            foreach (var name in names)
            {
                var rpc = typeof(MatchSessionState).GetMethod(name);

                Assert.That(rpc, Is.Not.Null, name);
                var attribute = (Fusion.RpcAttribute)Attribute.GetCustomAttribute(
                    rpc,
                    typeof(Fusion.RpcAttribute));
                Assert.That(attribute, Is.Not.Null, name);
                Assert.That(attribute.Sources, Is.EqualTo(Fusion.RpcSources.All), name);
                Assert.That(
                    attribute.Targets,
                    Is.EqualTo(Fusion.RpcTargets.StateAuthority),
                    name);
                Assert.That(
                    Array.Exists(
                        rpc.GetParameters(),
                        parameter => parameter.ParameterType == typeof(Fusion.RpcInfo)),
                    Is.True,
                    name);
                Assert.That(
                    Array.Exists(
                        rpc.GetParameters(),
                        parameter => parameter.Name == "playerIndex"),
                    Is.False,
                    name);
            }
        }

        [Test]
        public void NetworkScenes_ContainsBuildListedMatchAndLobbyScenes()
        {
            var scenes = AssetDatabase.LoadAssetAtPath<NetworkScenes>(
                "Assets/_Game/Content/Settings/NetworkScenes.asset");

            Assert.That(scenes, Is.Not.Null);
            Assert.That(scenes.MatchScene.IsValid, Is.True);
            Assert.That(scenes.LobbyScene.IsValid, Is.True);
            Assert.That(scenes.MatchScene, Is.Not.EqualTo(scenes.LobbyScene));
        }

        [Test]
        public void MatchEventRpcs_BroadcastOnlyAuthorityConfirmedData()
        {
            var names = new[]
            {
                "RPC_NotifyItemDestroyed",
                "RPC_NotifyPlayerItemStatuses",
                "RPC_NotifyPlayerStunned",
                "RPC_NotifyObjectThrown",
                "RPC_NotifyFinalWarning",
                "RPC_NotifyMatchChat",
            };

            foreach (var name in names)
            {
                var rpc = typeof(MatchSessionState).GetMethod(name);

                Assert.That(rpc, Is.Not.Null, name);
                var attribute = (Fusion.RpcAttribute)Attribute.GetCustomAttribute(
                    rpc,
                    typeof(Fusion.RpcAttribute));
                Assert.That(attribute, Is.Not.Null, name);
                Assert.That(
                    attribute.Sources,
                    Is.EqualTo(Fusion.RpcSources.StateAuthority),
                    name);
                Assert.That(
                    attribute.Targets,
                    Is.EqualTo(Fusion.RpcTargets.All),
                    name);
            }

            var statusRpc = typeof(MatchSessionState).GetMethod(
                "RPC_NotifyPlayerItemStatuses");
            Assert.That(
                Array.Exists(
                    statusRpc.GetParameters(),
                    parameter => parameter.ParameterType == typeof(byte[])),
                Is.True,
                "Item status notifications must carry only the encoded public status list.");

            var destroyedRpc = typeof(MatchSessionState).GetMethod(
                "RPC_NotifyItemDestroyed");
            Assert.That(
                Array.Exists(
                    destroyedRpc.GetParameters(),
                    parameter => parameter.Name.IndexOf(
                        "owner",
                        StringComparison.OrdinalIgnoreCase) >= 0),
                Is.False,
                "Destroyed item notifications must not reveal its owner.");

            var statusProperties = typeof(PlayerItemStatusSnapshot).GetProperties();
            Assert.That(statusProperties, Has.Length.EqualTo(2));
            Assert.That(
                Array.Exists(statusProperties, property =>
                    string.Equals(property.Name, "ItemId", StringComparison.Ordinal)),
                Is.True);
            Assert.That(
                Array.Exists(statusProperties, property =>
                    string.Equals(property.Name, "IsDestroyed", StringComparison.Ordinal)),
                Is.True);
            Assert.That(
                Array.Exists(statusProperties, property =>
                    property.Name.IndexOf("owner", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    property.Name.IndexOf("player", StringComparison.OrdinalIgnoreCase) >= 0),
                Is.False,
                "Public item status snapshots must not expose assignment ownership.");
        }

        [Test]
        public void MatchStarter_ForwardsReplicatedActivityAndResult()
        {
            var gameObject = new GameObject("MatchStarterTest");

            try
            {
                var starter = gameObject.AddComponent<MatchStarter>();
                IReadOnlyList<bool> receivedActivity = null;
                IReadOnlyList<PlayerInteractionStateSnapshot> receivedInteractions = null;
                MatchResult? receivedResult = null;
                IReadOnlyList<PlayerItemStatusSnapshot> secondReceivedStatuses = null;
                starter.ParticipantActivityReceived += value =>
                    receivedActivity = value;
                starter.PlayerInteractionStatesReceived += value =>
                    receivedInteractions = value;
                starter.MatchResultReceived += value => receivedResult = value;

                starter.PublishParticipantActivity(new[] { true, false });
                starter.PublishPlayerInteractionStates(new[]
                {
                    new PlayerInteractionStateSnapshot(0, 12d, 4),
                    new PlayerInteractionStateSnapshot(1, 0d, 5),
                });
                IReadOnlyList<PlayerItemStatusSnapshot> receivedStatuses = null;
                starter.PlayerItemStatusesReceived += value => receivedStatuses = value;
                starter.PlayerItemStatusesReceived += value => secondReceivedStatuses = value;
                starter.PublishPlayerItemStatuses(new[]
                {
                    new PlayerItemStatusSnapshot("Soda_01", false),
                    new PlayerItemStatusSnapshot("Burger_01", true),
                });
                starter.PublishMatchResult(new MatchResult(
                    MatchEndReason.LastPlayerStanding,
                    300d,
                    new[] { 0 }));

                Assert.That(receivedActivity, Is.EqualTo(new[] { true, false }));
                Assert.That(receivedInteractions.Count, Is.EqualTo(2));
                Assert.That(receivedInteractions[0].StunEndsAt, Is.EqualTo(12d));
                Assert.That(
                    receivedInteractions[0].RemainingDestructionUses,
                    Is.EqualTo(4));
                Assert.That(receivedStatuses, Is.Not.Null);
                Assert.That(receivedStatuses.Count, Is.EqualTo(2));
                Assert.That(receivedStatuses[0].ItemId, Is.EqualTo("Soda_01"));
                Assert.That(receivedStatuses[0].IsDestroyed, Is.False);
                Assert.That(receivedStatuses[1].ItemId, Is.EqualTo("Burger_01"));
                Assert.That(receivedStatuses[1].IsDestroyed, Is.True);
                Assert.That(secondReceivedStatuses, Is.SameAs(receivedStatuses));
                Assert.That(receivedResult.HasValue, Is.True);
                Assert.That(
                    receivedResult.Value.EndReason,
                    Is.EqualTo(MatchEndReason.LastPlayerStanding));
                Assert.That(receivedResult.Value.EndedAt, Is.EqualTo(300d));
                Assert.That(receivedResult.Value.WinnerPlayerIndices, Is.EqualTo(new[] { 0 }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void PlayerInteractionState_IsPersistentNetworkedData()
        {
            var stunEndsAt = typeof(MatchSessionState).GetProperty("StunEndsAt");
            var remainingUses = typeof(MatchSessionState).GetProperty(
                "RemainingDestructionUses");

            Assert.That(stunEndsAt, Is.Not.Null);
            Assert.That(remainingUses, Is.Not.Null);
            Assert.That(
                Attribute.IsDefined(stunEndsAt, typeof(Fusion.NetworkedAttribute)),
                Is.True);
            Assert.That(
                Attribute.IsDefined(remainingUses, typeof(Fusion.NetworkedAttribute)),
                Is.True);

            var snapshot = new PlayerInteractionStateSnapshot(1, 15d, 3);
            Assert.That(snapshot.IsStunned(14.99d), Is.True);
            Assert.That(snapshot.IsStunned(15d), Is.False);
        }

        [Test]
        public void PlayerStamina_IsPersistentNetworkedData()
        {
            var stamina = typeof(NetworkPlayerMotor).GetProperty("CurrentStamina");
            var exhausted = typeof(NetworkPlayerMotor).GetProperty("IsSprintExhausted");

            Assert.That(stamina, Is.Not.Null);
            Assert.That(exhausted, Is.Not.Null);
            Assert.That(Attribute.IsDefined(stamina, typeof(Fusion.NetworkedAttribute)), Is.True);
            Assert.That(Attribute.IsDefined(exhausted, typeof(Fusion.NetworkedAttribute)), Is.True);
        }

        [Test]
        public void ObjectStateSnapshot_PreservesPhysicsAndVisibilityState()
        {
            var pose = new Pose(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 45f, 0f));
            var velocity = new Vector3(4f, 5f, 6f);
            var snapshot = new MatchObjectStateSnapshot(
                "Soda_01",
                2,
                pose,
                velocity,
                true,
                7,
                true);

            Assert.That(snapshot.ObjectId, Is.EqualTo("Soda_01"));
            Assert.That(snapshot.HolderPlayerIndex, Is.EqualTo(2));
            Assert.That(snapshot.Pose, Is.EqualTo(pose));
            Assert.That(snapshot.InitialVelocity, Is.EqualTo(velocity));
            Assert.That(snapshot.IsDestroyed, Is.True);
            Assert.That(snapshot.Version, Is.EqualTo(7));
            Assert.That(snapshot.IsPhysicsActive, Is.True);
            Assert.That(snapshot.IsPendingEjection, Is.False);
        }

        [Test]
        public void HighlightReplaySerializer_RoundTripsOrderTimingAndFrames()
        {
            var segment = new HighlightSegment(10d, 12d, 2d);
            var candidate = new HighlightCandidate(
                HighlightType.FirstBlood,
                new[] { segment },
                "player-1",
                eventAt: 11.5d,
                score: 87d,
                actorPlayerIndex: 2,
                secondaryPlayerIndex: 3);
            var frame = new HighlightReplayFrame(
                11d,
                new[]
                {
                    new Pose(Vector3.one, Quaternion.Euler(0f, 45f, 0f)),
                },
                new[]
                {
                    new WorldObjectState(
                        "Soda_01",
                        new Pose(Vector3.right, Quaternion.identity)),
                });
            var source = new[]
            {
                new HighlightReplayData(
                    candidate,
                    new[] { new HighlightReplayClip(segment, new[] { frame }) }),
            };

            var payload = HighlightReplaySerializer.Serialize(source);

            Assert.That(
                HighlightReplaySerializer.TryDeserialize(payload, out var restored),
                Is.True);
            Assert.That(restored, Has.Length.EqualTo(1));
            Assert.That(restored[0].Candidate.Type, Is.EqualTo(HighlightType.FirstBlood));
            Assert.That(restored[0].Candidate.TargetId, Is.EqualTo("player-1"));
            Assert.That(restored[0].Candidate.EventAt, Is.EqualTo(11.5d));
            Assert.That(restored[0].Candidate.Score, Is.EqualTo(87d));
            Assert.That(restored[0].Candidate.ActorPlayerIndex, Is.EqualTo(2));
            Assert.That(restored[0].Candidate.SecondaryPlayerIndex, Is.EqualTo(3));
            Assert.That(restored[0].Clips[0].Segment.PlaybackSpeed, Is.EqualTo(2d));
            Assert.That(restored[0].Clips[0].Frames[0].RecordedAt, Is.EqualTo(11d));
            Assert.That(
                restored[0].Clips[0].Frames[0].WorldObjects[0].ObjectId,
                Is.EqualTo("Soda_01"));
        }
    }
}
