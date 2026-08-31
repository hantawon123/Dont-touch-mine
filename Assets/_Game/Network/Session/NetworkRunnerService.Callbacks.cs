using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using Game.Core.Rooms;
using Game.Network.Lobby;
using Game.Network.Players;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Network.Session
{
    public sealed partial class NetworkRunnerService
    {
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

            ReportExit(Translate(reason));
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
            ReportExit(Translate(shutdownReason));
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

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            if (runner == null || !runner.IsRunning)
            {
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
            MigrateHostAsync(runner, hostMigrationToken).Forget();
        }

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
            var migrationRevision = ++_hostMigrationRevision;
            var migrationScene = runner.SceneInfo;
            NetworkRunner replacementRunner = null;
            Debug.Log("[Network] Host migration started.");

            try
            {
                await runner.Shutdown(shutdownReason: ShutdownReason.HostMigration);
                if (migrationRevision != _hostMigrationRevision) return;
                Debug.Log("[Network] Host migration: old runner stopped; creating replacement.");
                _spawner?.Clear();

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
                await UniTask.WaitUntil(() =>
                    migrationRevision != _hostMigrationRevision || !IsCurrentRunner(replacementRunner) ||
                    restoreFailure != null ||
                    CanCompleteHostMigration(replacementRunner.IsRunning, replacementRunner.IsResume,
                        replacementRunner.IsSceneManagerBusy));
                if (migrationRevision != _hostMigrationRevision || !IsCurrentRunner(replacementRunner)) return;
                if (restoreFailure != null)
                    throw new InvalidOperationException(
                        $"Could not restore the host migration snapshot: {restoreFailure.Message}", restoreFailure);

                ReadConfiguredSettings();
                if (replacementRunner.IsServer)
                {
                    MatchMigration = _matchStarter?.CaptureMigrationState();
                    foreach (var player in replacementRunner.ActivePlayers)
                        _spawner?.Spawn(replacementRunner, player, NicknameOf(replacementRunner, player));
                    if (MatchMigration != null && !IsResultSceneLoaded)
                    {
                        _matchRuntimeRestoreFailure = null;
                        _matchRuntimeRestorePending = true;
                        // The scene coordinator restores rules on a simulation tick. Keep gameplay paused
                        // and observe its result from Update, so failure cleanup never tears down a tick.
                        await UniTask.WaitUntil(() =>
                            migrationRevision != _hostMigrationRevision || !IsCurrentRunner(replacementRunner) ||
                            !_matchRuntimeRestorePending);
                        if (migrationRevision != _hostMigrationRevision || !IsCurrentRunner(replacementRunner)) return;
                        if (_matchRuntimeRestoreFailure != null)
                            throw new InvalidOperationException(
                                $"Could not restore match rules: {_matchRuntimeRestoreFailure.Message}",
                                _matchRuntimeRestoreFailure);
                    }
                    if (!(_matchStarter?.HasStartedMatch ?? false))
                        _spawner?.RepositionSeated(replacementRunner);
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

        public void OnSceneLoadStart(NetworkRunner runner)
        {
            if (IsCurrentRunner(runner)) IsResultSceneLoaded = false;
        }

        /// <summary>
        /// Re-seats everyone on the newly loaded scene's spawn points.
        /// </summary>
        /// <remarks>
        /// Order matters. Listeners run first so that the scene's spawn points
        /// have reached the spawner, and only then are the characters moved;
        /// moving first would place them on the points of the scene that has
        /// just been unloaded.
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
            Debug.Log(
                $"[Session] Scene load done: '{SceneManager.GetActiveScene().name}', " +
                $"IsServer={runner != null && runner.IsServer}.");

            SceneLoaded?.Invoke();
            if (!_hostMigrationInProgress &&
                (!runner.IsResume || !(_matchStarter?.HasStartedMatch ?? false)))
                _spawner?.RepositionSeated(runner);
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
            if (runner.IsServer)
            {
                if (type == HighlightReadyKeyType && version == HighlightReplayKeyVersion &&
                    sequence == _highlightTransferSequence && data.Length == 1 && data[0] == 1)
                    _highlightPendingPlayers.Remove(player);
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
            Debug.Log($"[Highlight] Received {data.Length:N0} compressed bytes; preparing replay.");
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
