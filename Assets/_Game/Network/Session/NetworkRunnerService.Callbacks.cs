using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using Fusion;
using Fusion.Addons.KCC;
using Fusion.Sockets;
using Photon.Realtime;
using Game.Core.Match;
using Game.Core.Players;
using Game.Core.Rooms;
using Game.Network.Lobby;
using Game.Network.Players;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Network.Session
{
    public sealed partial class NetworkRunnerService : ILobbyCallbacks
    {
        internal const double HostMigrationStageTimeoutSeconds = 60d;
        // ---- INetworkRunnerCallbacks ------------------------------------------
        // Only the connection lifecycle is handled here. Gameplay callbacks are
        // filled in by later steps.

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (!IsCurrentRunner(runner))
            {
                return;
            }

            Debug.Log($"[Network] Player joined: {player}.");
            // Snapshot seats must be restored before a join can allocate a new one.
            if (_hostMigrationInProgress) return;
            _spawner?.Spawn(runner, player, NicknameOf(runner, player));
            ReportPlayerCount();
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (!IsCurrentRunner(runner))
            {
                return;
            }

            Debug.Log($"[Network] Player left: {player}.");
            if (runner.IsServer)
            {
                _pendingKicks.Remove(player);
                _highlightPendingPlayers.Remove(player);
                _matchStarter?.TryHandlePlayerLeft(player);
            }

            _spawner?.Despawn(runner, player);
            if (player == runner.LocalPlayer)
            {
                _localInputMotor = null;
            }

            ReportPlayerCount();
        }

        public void OnConnectedToServer(NetworkRunner runner)
        {
            if (!IsCurrentRunner(runner))
            {
                return;
            }

            Debug.Log($"[Network] Connected to session '{RoomCode}'.");
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            if (!IsCurrentRunner(runner))
            {
                return;
            }

            Debug.LogWarning($"[Network] Disconnected: {reason}");
            if (_hostMigrationInProgress)
            {
                return;
            }

            ReportExit(ResolveUnexpectedExit(_isClientSession, Translate(reason)));
        }

        public void OnConnectFailed(
            NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            Debug.LogError($"[Network] Connect failed: {reason}");
        }

        /// <summary>
        /// Runs on the authority when a client asks to join. The password check
        /// belongs here: refusing at this point keeps the client out of the
        /// session entirely rather than kicking it after it is already inside.
        /// </summary>
        public void OnConnectRequest(
            NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            if (!IsCurrentRunner(runner))
            {
                request.Refuse();
                return;
            }

            // SessionInfo.PlayerCount can already include the connection that
            // is waiting for this decision. Count only accepted players so the
            // final available slot is not rejected as an off-by-one.
            if (_configuredMaxPlayers > 0 &&
                CountActivePlayers(runner) >= _configuredMaxPlayers)
            {
                Debug.Log("[Network] Refused a join: configured player limit reached.");
                request.Refuse();
                return;
            }

            if (string.IsNullOrEmpty(_expectedPassword))
            {
                request.Accept();
                return;
            }

            SessionConnectionTokenCodec.Decode(token, out var presented, out _);

            // Only the password admits anyone. The room code says which room to
            // reach and grants nothing, so a code read off the browser listing
            // is useless without the password. The nickname in the same token
            // grants nothing either and is not read here.
            if (SessionConnectionTokenCodec.MatchesPassword(
                    presented,
                    _expectedPassword))
            {
                request.Accept();
                return;
            }

            Debug.Log("[Network] Refused a join: wrong password.");
            request.Refuse();
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            if (!IsCurrentRunner(runner))
            {
                return;
            }

            Debug.Log($"[Network] Shutdown: {shutdownReason}");
            if (_hostMigrationInProgress &&
                shutdownReason == ShutdownReason.HostMigration)
            {
                ReleaseRunner(preserveMigrationState: true);
                return;
            }

            // Unexpected shutdown of the replacement runner ends this migration.
            _hostMigrationInProgress = false;
            _hostMigrationRevision++;
            ReportExit(ResolveUnexpectedExit(_isClientSession, Translate(shutdownReason)));
            ReleaseRunner();
        }

        /// <summary>
        /// Photon pushes the room list unprompted whenever it changes, so this
        /// converts and forwards it rather than answering a request.
        /// </summary>
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            if (!IsCurrentRunner(runner))
            {
                return;
            }

            _roomBuffer.Clear();

            if (sessionList != null)
            {
                for (var i = 0; i < sessionList.Count; i++)
                {
                    if (RoomSummaryMapper.TryToSummary(sessionList[i], out var room))
                    {
                        _roomBuffer.Add(room);
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"[Network] Ignored invalid room listing '{sessionList[i].Name}'.");
                    }
                }
            }

            Debug.Log($"[Network] Room list updated: {_roomBuffer.Count} room(s).");
            _roomListSink?.SetRooms(_roomBuffer);
        }

        public void OnJoinedLobby()
        {
        }

        public void OnLeftLobby()
        {
        }

        public void OnLobbyStatisticsUpdate(List<TypedLobbyInfo> lobbyStatistics)
        {
        }

        public void OnRoomListUpdate(List<RoomInfo> roomList)
        {
            if (!_browsingLobby || _matchmakingClient == null)
            {
                return;
            }

            if (roomList != null)
            {
                for (var i = 0; i < roomList.Count; i++)
                {
                    var info = roomList[i];
                    if (info == null || string.IsNullOrEmpty(info.Name))
                    {
                        continue;
                    }

                    if (info.RemovedFromList)
                    {
                        _realtimeRooms.Remove(info.Name);
                    }
                    else
                    {
                        _realtimeRooms[info.Name] = info;
                    }
                }
            }

            _roomBuffer.Clear();
            foreach (var info in _realtimeRooms.Values)
            {
                if (RoomSummaryMapper.TryToSummary(info, out var room))
                {
                    _roomBuffer.Add(room);
                }
            }

            Debug.Log($"[Network] Room list updated: {_roomBuffer.Count} room(s).");
            _roomListSink?.SetRooms(_roomBuffer);
        }

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            if (runner == null || !runner.IsRunning)
            {
                return;
            }

            if (_hostMigrationInProgress)
            {
                input.Set(default(NetworkPlayerInput));
                return;
            }

            if (_localInputMotor == null ||
                _localInputMotor.Object == null ||
                !_localInputMotor.Object.HasInputAuthority)
            {
                _localInputMotor = FindLocalInputMotor(runner);
            }

            input.Set(_localInputMotor == null
                ? default
                : _localInputMotor.CaptureInput());
        }

        private NetworkPlayerMotor FindLocalInputMotor(NetworkRunner runner)
        {
            var playerObject = runner.GetPlayerObject(runner.LocalPlayer);
            var motor = playerObject == null
                ? null
                : playerObject.GetComponent<NetworkPlayerMotor>();

            // SetPlayerObject is authority-owned state and can arrive after the
            // avatar itself on a client. The roster is populated from Spawned,
            // so it keeps input alive during that ordering window instead of
            // silently submitting default input for the whole local player.
            if (motor == null && _roster != null &&
                _roster.TryGetAvatar(runner.LocalPlayer, out var localAvatar))
            {
                motor = localAvatar.GetComponent<NetworkPlayerMotor>();
            }

            return motor;
        }

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
        {
            input.Set(default(NetworkPlayerInput));
        }

        public void OnHostMigration(
            NetworkRunner runner,
            HostMigrationToken hostMigrationToken)
        {
            // Migration is suspended: never reconnect or promote another participant.
            // MigrateHostAsync(runner, hostMigrationToken).Forget();
            if (!IsCurrentRunner(runner) || runner == null || _browsingLobby || _hostLossShutdownPending) return;
            // A disconnect callback can have already reported the reason, but a
            // migration callback still requires us to stop this runner explicitly.
            _hostLossShutdownPending = true;
            ReportExit(RoomExitReason.HostClosed);
            CloseAfterHostLostAsync(runner).Forget(exception => Debug.LogException(exception));
        }

        private async UniTask CloseAfterHostLostAsync(NetworkRunner runner)
        {
            // Leave Fusion's callback/simulation stack before disposing physics and voice.
            await UniTask.NextFrame(PlayerLoopTiming.Update);
            if (!IsCurrentRunner(runner) || runner == null || runner.IsShutdown) return;
            await runner.Shutdown();
        }

        /* Host migration suspended. Preserve the previous implementation for later restoration.
        private async UniTask MigrateHostAsync(
            NetworkRunner runner,
            HostMigrationToken hostMigrationToken)
        {
            if (!IsCurrentRunner(runner) || hostMigrationToken == null ||
                _hostMigrationInProgress)
            {
                return;
            }

            _hostMigrationInProgress = true;
            var migrationStartedAt = Time.realtimeSinceStartupAsDouble;
            var migrationRevision = ++_hostMigrationRevision;
            var migrationScene = runner.SceneInfo;
            var lobbyPoses = new Dictionary<PlayerRef, (Pose Pose, PlayerPosture Posture)>();
            if (_scenes != null && IsOnlyScene(migrationScene, _scenes.LobbyScene))
            {
                foreach (var avatar in PlayerAvatars)
                {
                    if (avatar == null || avatar.Object == null || !avatar.Object.IsValid) continue;
                    var motor = avatar.GetComponent<NetworkPlayerMotor>();
                    if (motor != null)
                        lobbyPoses[avatar.Owner] = (new Pose(avatar.transform.position, avatar.transform.rotation), motor.Posture);
                }
            }
            NetworkRunner replacementRunner = null;
            Debug.Log("[Network] Host migration started.");

            try
            {
                if (!Application.isBatchMode && HostMigrationStarting != null)
                {
                    await UniTask.WaitForEndOfFrame();
                    if (migrationRevision != _hostMigrationRevision || !IsCurrentRunner(runner)) return;
                    HostMigrationStarting.Invoke();
                }
                // EndOfFrame resumes inside camera rendering in the Editor.
                // Do not tear down Fusion, physics and voice on that render stack.
                await UniTask.NextFrame(PlayerLoopTiming.Update);
                if (migrationRevision != _hostMigrationRevision || !IsCurrentRunner(runner)) return;
                var shutdownStartedAt = Time.realtimeSinceStartupAsDouble;
                await runner.Shutdown(shutdownReason: ShutdownReason.HostMigration);
                if (migrationRevision != _hostMigrationRevision) return;
                Debug.Log($"[Network] Host migration: old runner stopped in {Time.realtimeSinceStartupAsDouble - shutdownStartedAt:F3}s; creating replacement.");
                _spawner?.Clear();

                // Let destruction finish and present the held frame before building
                // another runner and voice rig on the same frame as teardown.
                await UniTask.NextFrame(PlayerLoopTiming.Update);
                if (migrationRevision != _hostMigrationRevision) return;
                var connectionStartedAt = Time.realtimeSinceStartupAsDouble;
                var sceneManager = CreateRunner(
                    hostMigrationToken.GameMode != GameMode.Server);
                replacementRunner = _runner;
                Exception restoreFailure = null;
                var result = await replacementRunner.StartGame(new StartGameArgs
                {
                    GameMode = hostMigrationToken.GameMode,
                    PlayerUniqueId = _playerUniqueId,
                    HostMigrationToken = hostMigrationToken,
                    HostMigrationResume = resumedRunner =>
                    {
                        if (migrationRevision != _hostMigrationRevision || !IsCurrentRunner(resumedRunner)) return;
                        // Fusion invokes this from a coroutine, outside this async try/catch.
                        restoreFailure = TryRestoreHostMigrationSnapshot(() => ResumeHostMigration(resumedRunner));
                    },
                    ConnectionToken = SessionConnectionTokenCodec.Encode(
                        _expectedPassword,
                        _profile?.Nickname),
                    SceneManager = sceneManager,
                    Scene = migrationScene,
                });
                if (migrationRevision != _hostMigrationRevision || !IsCurrentRunner(replacementRunner)) return;
                Debug.Log($"[Network] Host migration: replacement StartGame took {Time.realtimeSinceStartupAsDouble - connectionStartedAt:F3}s.");

                if (!result.Ok)
                {
                    Debug.LogError(
                        $"Fusion could not resume the room: " +
                        $"{result.ShutdownReason} {result.ErrorMessage}");
                    _hostMigrationInProgress = false;
                    ReportExit(RoomExitReason.HostClosed);
                    // StartGame failure already owns Fusion teardown; do not shut it down again.
                    ReleaseAfterFusionShutdown(replacementRunner);
                    return;
                }

                // StartGame can succeed before Fusion invokes HostMigrationResume.
                // IsResume clears only after the callback and Fusion's remaining initialization.
                if (!await WaitForHostMigrationAsync(replacementRunner, migrationRevision, () =>
                    restoreFailure != null ||
                    CanCompleteHostMigration(replacementRunner.IsRunning, replacementRunner.IsResume,
                        replacementRunner.IsSceneManagerBusy), "Fusion initialization")) return;
                if (restoreFailure != null)
                    throw new InvalidOperationException(
                        $"Could not restore the host migration snapshot: {restoreFailure.Message}", restoreFailure);

                ReadConfiguredSettings();
                if (replacementRunner.IsServer)
                {
                    if (_matchStarter == null || _scenes == null)
                        throw new InvalidOperationException("Host migration requires room state and scene configuration.");
                    MatchMigration = _matchStarter.CaptureMigrationState();
                    var phase = ResolveHostMigrationPhase(MatchMigration?.Phase.Phase ?? MatchPhase.Waiting,
                        MatchMigration?.Result.HasValue ?? false);
                    var scene = phase switch
                    {
                        MatchPhase.Waiting => _scenes.LobbyScene,
                        MatchPhase.Result => _scenes.ResultScene,
                        _ => _scenes.MatchScene,
                    };
                    if (!scene.IsValid)
                        throw new InvalidOperationException($"No scene is configured for migrated phase {phase}.");
                    if (phase == MatchPhase.Result && !TryPublishMatchState(new MatchStateSnapshot(phase, 0d)))
                        throw new InvalidOperationException("Could not restore the completed match phase.");
                    // The departing peer's scene can be newer than the saved checkpoint.
                    // Room objects survive this correction; only the scene-owned runtime is rebuilt.
                    if (!IsOnlyScene(replacementRunner.SceneInfo, scene))
                    {
                        var load = replacementRunner.LoadScene(scene, LoadSceneMode.Single);
                        if (!await WaitForHostMigrationAsync(replacementRunner, migrationRevision,
                                () => load.IsDone, "checkpoint scene load")) return;
                        if (load.Error != null) throw load.Error;
                    }
                    // Scene entry points must subscribe before replaying the retained room state.
                    await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
                    if (migrationRevision != _hostMigrationRevision || !IsCurrentRunner(replacementRunner)) return;
                    _matchStarter.PublishSceneState();
                    foreach (var player in replacementRunner.ActivePlayers)
                        _spawner?.Spawn(replacementRunner, player, NicknameOf(replacementRunner, player));
                    if (phase == MatchPhase.Hiding || phase == MatchPhase.Searching)
                    {
                        _matchRuntimeRestoreFailure = null;
                        _matchRuntimeRestorePending = true;
                        // The scene coordinator restores rules on a simulation tick. Keep gameplay paused
                        // and observe its result from Update, so failure cleanup never tears down a tick.
                        if (!await WaitForHostMigrationAsync(replacementRunner, migrationRevision,
                                () => !_matchRuntimeRestorePending, "match runtime restore")) return;
                        if (_matchRuntimeRestoreFailure != null)
                            throw new InvalidOperationException(
                                $"Could not restore match rules: {_matchRuntimeRestoreFailure.Message}",
                                _matchRuntimeRestoreFailure);
                    }
                    // Lobby movement has no match-rule checkpoint to roll back. Keep the new
                    // host's last observed positions instead of seating everyone at the entrance.
                    if (phase == MatchPhase.Waiting)
                    {
                        foreach (var avatar in PlayerAvatars)
                        {
                            if (avatar == null || !lobbyPoses.TryGetValue(avatar.Owner, out var saved)) continue;
                            var motor = avatar.GetComponent<NetworkPlayerMotor>();
                            if (motor == null || !motor.TryRestoreScenePose(saved.Pose, saved.Posture))
                                throw new InvalidOperationException("Could not restore the lobby character pose.");
                        }
                    }
                    _spawner?.RefreshHost(replacementRunner);
                    if (!replacementRunner.SessionInfo.UpdateCustomProperties(
                        new Dictionary<string, SessionProperty>
                        {
                            [SessionPropertyKeys.HostNickname] = SanitiseNickname(_profile?.Nickname),
                        }))
                        Debug.LogWarning("[Network] Could not update the migrated room's host name.");
                }
                _roster?.Refresh(replacementRunner);
                _hostMigrationInProgress = false;
                _exitReported = false;
                ReportPlayerCount();
                Debug.Log($"[Network] Host migration runtime ready after {Time.realtimeSinceStartupAsDouble - migrationStartedAt:F3}s. IsServer={IsServer}.");

                try
                {
                    if (replacementRunner.IsServer && !await replacementRunner.PushHostMigrationSnapshot())
                    {
                        Debug.LogWarning(
                            "[Network] The migrated room resumed, but its first " +
                            "replacement snapshot could not be pushed.");
                    }
                }
                catch (Exception exception)
                {
                    // The room already resumed. A failed follow-up snapshot
                    // reduces the next migration's freshness but must not close
                    // this healthy session.
                    Debug.LogWarning(
                        $"[Network] Could not push the replacement host " +
                        $"snapshot: {exception.Message}");
                }

                if (migrationRevision != _hostMigrationRevision || !IsCurrentRunner(replacementRunner)) return;
                Debug.Log(
                    $"[Network] Host migration completed. IsServer={IsServer}.");
            }
            catch (Exception exception)
            {
                if (migrationRevision != _hostMigrationRevision) return;
                Debug.LogError($"[Network] Host migration failed: {exception.Message}");
                _hostMigrationInProgress = false;
                ReportExit(RoomExitReason.HostClosed);
                Shutdown();
            }
        }

        private async UniTask<bool> WaitForHostMigrationAsync(
            NetworkRunner runner, int revision, Func<bool> completed, string stage)
        {
            var startedAt = Time.realtimeSinceStartupAsDouble;
            await UniTask.WaitUntil(() => revision != _hostMigrationRevision || !IsCurrentRunner(runner) ||
                IsHostMigrationStageComplete(completed(), Time.realtimeSinceStartupAsDouble - startedAt, stage));
            var current = revision == _hostMigrationRevision && IsCurrentRunner(runner);
            if (current)
                Debug.Log($"[Network] Host migration: {stage} took {Time.realtimeSinceStartupAsDouble - startedAt:F3}s.");
            return current;
        }

        */

        internal static bool IsHostMigrationStageComplete(bool completed, double elapsed, string stage)
        {
            if (completed) return true;
            if (elapsed >= HostMigrationStageTimeoutSeconds)
                throw new TimeoutException($"Host migration timed out during {stage}.");
            return false;
        }

        internal static MatchPhase ResolveHostMigrationPhase(MatchPhase savedPhase, bool hasResult)
        {
            switch (savedPhase)
            {
                case MatchPhase.Waiting when !hasResult:
                case MatchPhase.Hiding when !hasResult:
                case MatchPhase.Searching when !hasResult:
                    return savedPhase;
                case MatchPhase.Highlight when hasResult:
                case MatchPhase.Result when hasResult:
                    return MatchPhase.Result; // The previous host's replay is unavailable.
                default:
                    throw new InvalidOperationException("The host snapshot has inconsistent match phase and result.");
            }
        }

        internal static bool IsOnlyScene(NetworkSceneInfo info, SceneRef expected) =>
            expected.IsValid && info.SceneCount == 1 && info.Scenes[0] == expected;

        internal static bool CanCompleteHostMigration(bool isRunning, bool isResuming, bool isSceneManagerBusy)
        {
            return isRunning && !isResuming && !isSceneManagerBusy;
        }

        internal static Exception TryRestoreHostMigrationSnapshot(Action restore)
        {
            try
            {
                restore();
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        /* Host migration suspended. No snapshot objects are recreated.
        private void ResumeHostMigration(NetworkRunner resumedRunner)
        {
            var playerByObject = new Dictionary<NetworkId, PlayerRef>();
            foreach (var pair in
                     resumedRunner.GetResumeSnapshotNetworkObjectPlayerObjects())
            {
                playerByObject[pair.Value] = pair.Key;
            }

            foreach (var snapshotObject in
                     resumedRunner.GetResumeSnapshotNetworkObjects())
            {
                // The former host has left; do not resurrect its avatar or seat.
                if (snapshotObject.TryGetBehaviour<PlayerAvatar>(out var snapshotAvatar) &&
                    snapshotAvatar.IsHost)
                    continue;

                var hasTransform = snapshotObject.TryGetBehaviour<NetworkTRSP>(
                    out var networkTransform);
                var position = hasTransform
                    ? networkTransform.Data.Position
                    : Vector3.zero;
                if (snapshotObject.TryGetBehaviour<KCC>(out var snapshotKcc))
                    position = snapshotKcc.GetNetworkBufferPosition();
                var rotation = hasTransform
                    ? networkTransform.Data.Rotation
                    : Quaternion.identity;
                var player = playerByObject.TryGetValue(
                    snapshotObject.Id,
                    out var mappedPlayer)
                    ? mappedPlayer
                    : PlayerRef.None;

                var restoredObject = resumedRunner.Spawn(
                    snapshotObject,
                    position,
                    rotation,
                    player,
                    (_, spawned) =>
                    {
                        spawned.CopyStateFrom(snapshotObject);
                        if (spawned.TryGetBehaviour<PlayerAvatar>(out var avatar))
                            avatar.IsHost = player.IsRealPlayer && player == resumedRunner.LocalPlayer;
                    });
                if (restoredObject == null)
                {
                    throw new InvalidOperationException(
                        $"Could not restore network object '{snapshotObject.Id}'.");
                }

                resumedRunner.MakeDontDestroyOnLoad(restoredObject.gameObject);
                if (restoredObject.TryGetBehaviour<NetworkPlayerMotor>(out var restoredMotor) &&
                    !restoredMotor.TryRestoreScenePose(new Pose(position, rotation), restoredMotor.Posture))
                    throw new InvalidOperationException("Could not resume the restored character's KCC.");
                if (player.IsRealPlayer &&
                    !(_spawner?.Restore(resumedRunner, player, restoredObject) ?? false))
                {
                    throw new InvalidOperationException(
                        $"Could not restore the character owned by {player}.");
                }
            }

            foreach (var sceneObject in
                     resumedRunner.GetResumeSnapshotNetworkSceneObjects())
            {
                sceneObject.Item1.CopyStateFrom(sceneObject.Item2);
            }
        }

        */

        public void OnSceneLoadStart(NetworkRunner runner)
        {
            if (!IsCurrentRunner(runner)) return;
            RaiseNetworkSceneLoadingPriority();
            IsResultSceneLoaded = false;
            _networkSceneLoadStartedAt = Time.realtimeSinceStartupAsDouble;
            Debug.Log(
                $"[SceneTiming] Fusion scene load started: current={SceneManager.GetActiveScene().name}, " +
                $"isServer={runner.IsServer}.");
        }

        /// <summary>
        /// Re-seats waiting-room players on the newly loaded scene's spawn points.
        /// </summary>
        /// <remarks>
        /// Order matters. Listeners run first so that the scene's spawn points
        /// have reached the spawner. Once a match has started, its runtime owns
        /// the role-specific placement; applying the generic points here would
        /// only teleport every character twice during the same transition.
        /// </remarks>
        public void OnSceneLoadDone(NetworkRunner runner)
        {
            if (!IsCurrentRunner(runner)) return;
            // Fusion merges loaded scenes into its own scene in multi-peer mode.
            // Their original Unity scene path is no longer a loaded-scene identity.
            IsResultSceneLoaded = false;
            if (_scenes != null && !string.IsNullOrEmpty(_scenes.ResultScenePath))
            {
                var resultScene = _scenes.ResultScene;
                var info = runner.SceneInfo;
                for (var index = 0; index < info.SceneCount; index++)
                    if (info.Scenes[index] == resultScene) IsResultSceneLoaded = true;
            }
            var lobbyLoaded = _scenes != null &&
                              IsOnlyScene(runner.SceneInfo, _scenes.LobbyScene);
            _highlightLobbyPrepared = _scenes != null &&
                ContainsScene(runner.SceneInfo, _scenes.LobbyScene) &&
                ContainsScene(runner.SceneInfo, _scenes.MatchScene);
            if (_highlightLobbyPrepared) _highlightLobbyLoadRequested = false;
            var tookOverPreloadedLobby = lobbyLoaded &&
                                         _preloadedLobbyRoots.Length > 0;
            if (tookOverPreloadedLobby)
            {
                for (var i = 0; i < _preloadedLobbyRoots.Length; i++)
                {
                    if (_preloadedLobbyRoots[i] != null)
                    {
                        _preloadedLobbyRoots[i].SetActive(true);
                    }
                }

                _preloadedLobbyRoots = Array.Empty<GameObject>();
            }

            if (lobbyLoaded)
            {
                ActivateLobbySceneAndUnloadFrontendAsync(runner)
                    .Forget(exception => Debug.LogException(exception));
            }

            Debug.Log(
                $"[Session] Scene load done: '{SceneManager.GetActiveScene().name}', " +
                $"IsServer={runner != null && runner.IsServer}.");
            Debug.Log(
                $"[SceneTiming] Fusion scene load completed: scene={SceneManager.GetActiveScene().name}, " +
                $"isServer={runner.IsServer}, " +
                $"elapsed={(_networkSceneLoadStartedAt < 0d ? 0d : Time.realtimeSinceStartupAsDouble - _networkSceneLoadStartedAt):F3}s.");
            _networkSceneLoadStartedAt = -1d;
            RestoreNetworkSceneLoadingPriority();
            if (lobbyLoaded && _roomEntryStartedAt >= 0d)
            {
                Debug.Log(
                    $"[SceneTiming] Room-to-Lobby ready: " +
                    $"total={Time.realtimeSinceStartupAsDouble - _roomEntryStartedAt:F3}s, " +
                    $"preloaded={tookOverPreloadedLobby}.");
                _roomEntryStartedAt = -1d;
                _lobbyPreloadStartedAt = -1d;
            }

            SceneLoaded?.Invoke();
            if (tookOverPreloadedLobby && runner.IsServer)
            {
                _spawner?.RepositionSeated(runner);
            }
            PublishSceneStateWhenReadyAsync(runner).Forget(exception => Debug.LogException(exception));
            if (!_hostMigrationInProgress &&
                !(_matchStarter?.HasStartedMatch ?? false) &&
                !lobbyLoaded)
                _spawner?.RepositionSeated(runner);
        }

        /// <summary>
        /// Finishes a preloaded Lobby takeover with the same Unity scene state as
        /// an ordinary single-scene load.
        /// </summary>
        /// <remarks>
        /// Lobby may already be loaded additively while Photon connects. Fusion
        /// can then adopt it without Unity making it active or unloading the
        /// Home/Room frontend pair. In single-peer mode that leaves Fusion's
        /// physics scene pointing at Room, so the visible Lobby avatar cannot
        /// move correctly. Select Lobby immediately, then leave Fusion's scene
        /// callback before unloading the old frontend scenes.
        /// </remarks>
        private async UniTask ActivateLobbySceneAndUnloadFrontendAsync(
            NetworkRunner runner)
        {
            if (_scenes == null || !_scenes.LobbyScene.IsValid)
            {
                return;
            }

            var lobby = SceneManager.GetSceneByBuildIndex(
                _scenes.LobbyScene.AsIndex);
            if (!lobby.IsValid() || !lobby.isLoaded)
            {
                return;
            }

            SceneManager.SetActiveScene(lobby);

            await UniTask.NextFrame(PlayerLoopTiming.Update);
            if (!IsCurrentRunner(runner) || !runner.IsRunning ||
                !IsOnlyScene(runner.SceneInfo, _scenes.LobbyScene))
            {
                return;
            }

            for (var index = SceneManager.sceneCount - 1; index >= 0; index--)
            {
                var loaded = SceneManager.GetSceneAt(index);
                // A Lobby session owns one build scene. buildIndex < 0 is a
                // Fusion/runtime scene and must remain under Fusion's control.
                if (!loaded.isLoaded || loaded == lobby || loaded.buildIndex < 0)
                {
                    continue;
                }

                _ = SceneManager.UnloadSceneAsync(loaded);
            }
        }

        private async UniTask PublishSceneStateWhenReadyAsync(NetworkRunner runner)
        {
            // A scene can load after MatchSessionState.Spawned without changing its revisions.
            var sceneInfo = runner.SceneInfo;
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
            if (IsCurrentRunner(runner) && runner.IsRunning && !runner.IsSceneManagerBusy &&
                runner.SceneInfo.Equals(sceneInfo))
                _matchStarter?.PublishSceneState();
        }

        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

        public void OnCustomAuthenticationResponse(
            NetworkRunner runner, Dictionary<string, object> data) { }

        public void OnReliableDataProgress(
            NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

        public void OnReliableDataReceived(
            NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data)
        {
            key.GetInts(out var type, out var version, out var sequence, out _);
            if (!IsCurrentRunner(runner))
            {
                return;
            }
            if (type == KickKeyType)
            {
                if (version != KickKeyVersion || sequence <= 0 || data.Length != 1 || data[0] != 1)
                    return;
                if (runner.IsServer)
                    DisconnectPendingKick(runner, player, sequence);
                else if (runner.IsClient)
                {
                    ReportExit(RoomExitReason.Kicked);
                    runner.SendReliableDataToServer(key, new byte[] { 1 });
                }
                return;
            }
            if (runner.IsServer)
            {
                if (type == ItemAssignmentRequestKeyType && version == ItemAssignmentKeyVersion &&
                    data.Length == 1 && data[0] == 1)
                    ResendItemAssignment(player); // The transport sender, never a client-supplied player id.
                if (type == HighlightReadyKeyType && version == HighlightReplayKeyVersion &&
                    sequence == _highlightTransferSequence && data.Length == 1 && data[0] == 1)
                    _highlightPendingPlayers.Remove(player);
                if (type == HighlightCompleteKeyType && version == HighlightReplayKeyVersion &&
                    sequence == _highlightTransferSequence && data.Length == 1 && data[0] == 1 &&
                    TryCompleteHighlightViewing(player))
                    runner.SendReliableDataToPlayer(player, key, new byte[] { 1 });
                return;
            }

            if (type == HighlightCompleteKeyType)
            {
                if (version == HighlightReplayKeyVersion &&
                    sequence == _receivedHighlightSequence &&
                    data.Length == 1 && data[0] == 1)
                {
                    _highlightCompletionRequested = false;
                    _localHighlightComplete = true;
                }
                return;
            }

            if (type == ItemAssignmentKeyType)
            {
                if (version != ItemAssignmentKeyVersion ||
                    data.Length == 0 || data.Length > MaxItemAssignmentBytes)
                {
                    Debug.LogWarning("[Match] Rejected invalid item assignment data.");
                    return;
                }

                var itemId = Encoding.UTF8.GetString(data.ToArray()).Trim();
                if (string.IsNullOrEmpty(itemId))
                {
                    Debug.LogWarning("[Match] Rejected empty item assignment.");
                    return;
                }

                ItemAssignmentReceived?.Invoke(itemId);
                return;
            }

            if (type != HighlightReplayKeyType ||
                version != HighlightReplayKeyVersion)
            {
                return;
            }

            if (!HighlightReplaySerializer.TryDeserializeCompressed(data, out var replay))
            {
                Debug.LogWarning("[Match] Rejected invalid highlight replay data.");
                return;
            }

            _receivedHighlightSequence = sequence;
            _highlightCompletionRequested = false;
            _localHighlightComplete = false;
            Debug.Log(
                $"[Highlight] Received {replay.Length} playable highlight(s), " +
                $"{data.Length:N0} compressed bytes; preparing replay.");
            HighlightReplayReceived?.Invoke(replay);
        }

        // The parameter type is obsolete in Fusion's own interface declaration,
        // so implementing this callback at all raises CS0618. Nothing to fix on
        // our side until Fusion changes the signature.
#pragma warning disable CS0618
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
#pragma warning restore CS0618
    }
}
