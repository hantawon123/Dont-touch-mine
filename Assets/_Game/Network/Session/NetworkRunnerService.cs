using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using Game.Core.Home;
using Game.Core.Lobby;
using Game.Core.Maps;
using Game.Core.Ports;
using Game.Core.Rooms;
using Game.Core.Match;
using Game.Core.Items;
using Game.Network.Match;
using Game.Network.Players;
using Game.Network.Voice;
using Game.Server.Items;
using Game.Server.Match;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Network.Session
{
    /// <summary>
    /// Owns every touch point with Fusion: creating the runner, starting and
    /// stopping a session, and receiving runner callbacks. Gameplay code asks
    /// this service instead of holding a <see cref="NetworkRunner"/> itself.
    /// </summary>
    /// <remarks>
    /// The game mode is a parameter rather than a constant, and authority is
    /// exposed only as <see cref="IsServer"/>. Moving from a player-hosted match
    /// to a dedicated server is therefore a change at the call site, not a
    /// rewrite of the gameplay layer.
    /// </remarks>
    public sealed partial class NetworkRunnerService :
        INetworkRunnerCallbacks,
        IMatchSceneDirector,
        INetworkMatchRuntimeSource,
        INetworkPlayerReplayStateSource,
        INetworkMatchAuthority,
        INetworkMatchEvents,
        INetworkHighlightReady,
        INetworkResultNavigation,
        ILobbyChatTransport,
        IDisposable
    {
        private const string RunnerObjectName = "[NetworkRunner]";
        private const string MatchmakingRegion = "kr";
        private const int ItemAssignmentKeyType = 0x4954454D;
        private const int ItemAssignmentKeyVersion = 1;
        private const int ItemAssignmentRequestKeyType = 0x49545251;
        private readonly Dictionary<string, string> _publishedItemAssignments = new(StringComparer.Ordinal);
        private const int MaxItemAssignmentBytes = 128;
        private const int HighlightReplayKeyType = 0x484C5452;
        private const int HighlightReplayKeyVersion = 4;
        private const int HighlightReadyKeyType = 0x484C5244;
        private readonly HashSet<PlayerRef> _highlightPendingPlayers = new();
        private int _receivedHighlightSequence;
        public bool IsHighlightReplayReady => _highlightPendingPlayers.Count == 0;

        public bool TryConfirmHighlightReady()
        {
            if (_runner == null || !_runner.IsRunning) return false;
            if (IsServer)
                _highlightPendingPlayers.Remove(_runner.LocalPlayer);
            else
            {
                if (_receivedHighlightSequence == 0) return false;
                _runner.SendReliableDataToServer(ReliableKey.FromInts(
                    HighlightReadyKeyType, HighlightReplayKeyVersion, _receivedHighlightSequence, 0),
                    new byte[] { 1 });
            }
            return true;
        }

        private readonly IRoomListSink _roomListSink;
        private readonly IRoomSessionSink _sessionSink;

        /// <summary>
        /// Told when players arrive and leave. The runner is passed in on each
        /// call rather than held by the spawner, so there is only ever one
        /// answer to which runner is current.
        /// </summary>
        private readonly PlayerSpawner _spawner;

        /// <summary>Where the room's roster is reported.</summary>
        private readonly IRoomParticipantSink _participantSink;

        /// <summary>
        /// This peer's own name, sent to the room so the others can show it.
        /// </summary>
        /// <remarks>
        /// Read at the moment a session starts rather than held as a string, so
        /// a name changed between two rooms is the name the second room sees.
        /// </remarks>
        private readonly PlayerProfile _profile;

        /// <summary>Where the authority's decision about starting is reported.</summary>
        private readonly IMatchStartSink _matchStartSink;

        /// <summary>
        /// Which scenes this layer may load. Optional so tests and scenes
        /// without a map can still open a session; a match then reports that it
        /// has nowhere to go rather than failing to construct.
        /// </summary>
        private readonly NetworkScenes _scenes;

        /// <summary>
        /// Raised on every peer once a networked scene has finished loading.
        /// </summary>
        /// <remarks>
        /// Exists because the scene's own contents are not this layer's business.
        /// Spawn points are marked by a component in <c>Game.Bootstrap</c>, which
        /// this assembly cannot reference, so Bootstrap listens here and hands
        /// the points over. No Fusion type is passed, so a listener does not need
        /// to reference Fusion either.
        /// </remarks>
        public event Action SceneLoaded;
        // Raised at end-of-frame, before the old runner destroys its avatars.
        public event Action HostMigrationStarting;

        private readonly List<RoomSummary> _roomBuffer = new List<RoomSummary>();

        private NetworkRunner _runner;
        private GameObject _runnerObject;
        private PlayerRoster _roster;
        private MatchStarter _matchStarter;
        private NetworkPlayerMotor _localInputMotor;

        public event Action<MatchStateSnapshot> MatchStateReceived;
        public event Action<LobbyChatMessage> ChatReceived;
        public event Action<string> ItemAssignmentReceived;
        public event Action<IReadOnlyList<MatchObjectStateSnapshot>> ObjectStatesReceived;
        public event Action<PlayerItemDestroyedEvent> ItemDestroyedReceived;
        public event Action<PlayerStunnedEvent> PlayerStunnedReceived;
        public event Action<ObjectThrownEvent> ObjectThrownReceived;
        public event Action<FinalWarningStartedEvent> FinalWarningReceived;
        public event Action<IReadOnlyList<bool>> ParticipantActivityReceived;
        public event Action<IReadOnlyList<PlayerInteractionStateSnapshot>>
            PlayerInteractionStatesReceived;
        public event Action<IReadOnlyList<HighlightReplayData>> HighlightReplayReceived;
        public event Action<MatchResult> MatchResultReceived;
        public event Action<IReadOnlyList<MatchParticipant>> LineUpReceived;
        public event Action SimulationTick;

        /// <summary>
        /// Password this peer requires from joiners while it is the authority. A
        /// plain field, never a networked property, so it is never replicated.
        /// </summary>
        private string _expectedPassword;

        /// <summary>
        /// True while the runner is browsing a lobby rather than playing a
        /// session. Entering a room replaces that runner with a fresh one.
        /// </summary>
        private bool _browsingLobby;

        /// <summary>
        /// Guards against reporting the same departure twice. Fusion can raise
        /// both a disconnect and a shutdown for one exit.
        /// </summary>
        private bool _exitReported = true;
        private bool _isClientSession;
        private bool _hostLossShutdownPending;
        private NetworkRunner _departingRunner;
        // Do not load a local scene while Fusion is still unloading its network scene.
        public bool IsRoomExitPending => _departingRunner != null;
        private int _itemAssignmentTransferSequence;
        private int _highlightTransferSequence;
        private bool _hostMigrationInProgress;
        private bool _matchRuntimeRestorePending;
        private Exception _matchRuntimeRestoreFailure;
        public bool IsMatchRuntimeRestorePending => _hostMigrationInProgress && _matchRuntimeRestorePending;

        public void ReportMatchRuntimeRestored(Exception failure)
        {
            if (!IsMatchRuntimeRestorePending) return;
            _matchRuntimeRestoreFailure = failure;
            _matchRuntimeRestorePending = false;
        }
        private bool _roomInitializationInProgress;
        private int _hostMigrationRevision;
        public MatchMigrationState MatchMigration { get; private set; }
        // Stable across replacement runners; identity only, not authentication.
        private readonly long _playerUniqueId = BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 0) | 1L;
        private int _configuredMaxPlayers;
        private int _destructionLimit = PlaySettingsDraft.DefaultDestructionLimit;

        private bool _disposed;

        public NetworkRunnerService(
            IRoomListSink roomListSink,
            IRoomSessionSink sessionSink,
            IRoomParticipantSink participantSink,
            IMatchStartSink matchStartSink,
            PlayerSpawner spawner,
            PlayerProfile profile,
            NetworkScenes scenes = null)
        {
            _roomListSink = roomListSink;
            _sessionSink = sessionSink;
            _participantSink = participantSink;
            _matchStartSink = matchStartSink;
            _spawner = spawner;
            _profile = profile;
            _scenes = scenes;
        }

        /// <summary>
        /// Longest nickname the network carries. Matches the
        /// <c>NetworkString&lt;_32&gt;</c> the character replicates, so a name
        /// that survives this survives the trip intact.
        /// </summary>
        private const int NicknameLimit = 32;

        /// <summary>
        /// The name to show for a player, taken from what they presented when
        /// they joined.
        /// </summary>
        /// <remarks>
        /// The authority's own name comes from its profile: it never connected to
        /// itself, so it presented no token.
        /// </remarks>
        private string NicknameOf(NetworkRunner runner, PlayerRef player)
        {
            if (player == runner.LocalPlayer)
            {
                return SanitiseNickname(_profile?.Nickname);
            }

            SessionConnectionTokenCodec.Decode(
                runner.GetPlayerConnectionToken(player),
                out _,
                out var presented);
            return SanitiseNickname(presented);
        }

        /// <summary>
        /// Makes a name presented by another peer safe to show.
        /// </summary>
        /// <remarks>
        /// The bytes came from someone else, so this is untrusted text. Control
        /// characters are dropped because they can hide or reorder what a reader
        /// sees, and the length is capped so one player cannot push the others
        /// out of a list. An empty result is left empty rather than replaced with
        /// a placeholder: only presentation knows what to show instead.
        /// </remarks>
        internal static string SanitiseNickname(string presented)
        {
            if (string.IsNullOrWhiteSpace(presented))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(presented.Length);

            foreach (var character in presented)
            {
                if (!char.IsControl(character))
                {
                    builder.Append(character);
                }

                if (builder.Length >= NicknameLimit)
                {
                    break;
                }
            }

            return builder.ToString().Trim();
        }

        public bool IsRunning => _runner != null && _runner.IsRunning;

        /// <summary>
        /// The local microphone for the session that is running, or null on a
        /// dedicated server and between sessions.
        /// </summary>
        /// <remarks>
        /// Exposed rather than injected because the rig is built with the runner
        /// and replaced with it. Anything holding a reference across a shutdown
        /// would be holding a destroyed component.
        /// </remarks>
        public IVoiceControl Voice { get; private set; }

        /// <summary>
        /// True once this peer has taken a lobby runner, including while that
        /// runner is still connecting.
        /// </summary>
        /// <remarks>
        /// The connecting half matters. Leaving the browser no longer cancels
        /// the connect, so opening it again can find one still in flight, and a
        /// caller asking this is asking whether there is a lobby runner to
        /// replace. Answering "no" until the connect lands would let a second
        /// runner be built beside the first, leaving an orphan that keeps
        /// pushing room lists into this service.
        /// </remarks>
        public bool IsBrowsingLobby => _browsingLobby;

        /// <summary>
        /// True on whichever peer holds authority. Identical for a player host
        /// and a dedicated server, so gameplay never asks which one it is.
        /// </summary>
        public bool IsServer => _runner != null && _runner.IsServer;
        public bool IsRuntimeReady => IsRunning && !_exitReported && !_hostMigrationInProgress;
        public bool IsHostMigrationInProgress => _hostMigrationInProgress;
        // Includes connecting/loading, but excludes a standalone scene and room browsing.
        public bool HasRoomSession => _hostMigrationInProgress || (_runner != null && !_browsingLobby);

        public IReadOnlyList<PlayerAvatar> PlayerAvatars =>
            _roster?.Avatars ?? Array.Empty<PlayerAvatar>();

        public bool TrySendChat(string text)
        {
            return _matchStarter != null && _matchStarter.RequestLobbyChat(text);
        }

        public double ServerTime
        {
            get
            {
                if (!IsRunning)
                {
                    throw new InvalidOperationException(
                        "Network time is unavailable before the runner starts.");
                }

                return _runner.SimulationTime;
            }
        }

        public bool TryGetPlayerPose(string playerId, out Pose pose)
        {
            if (_roster != null)
            {
                return _roster.TryGetPose(playerId, out pose);
            }

            pose = default;
            return false;
        }

        public bool TryGetPlayerReplayState(
            string playerId,
            out NetworkPlayerReplayState state)
        {
            if (_roster != null && _roster.TryGetAvatar(playerId, out var avatar))
            {
                var motor = avatar.GetComponent<NetworkPlayerMotor>();
                if (motor != null)
                {
                    state = new NetworkPlayerReplayState(
                        motor.Posture,
                        motor.AnimationGrounded,
                        motor.AttackSequence);
                    return true;
                }
            }

            state = default;
            return false;
        }

        public string RoomCode
        {
            get
            {
                if (_runner == null)
                {
                    return null;
                }

                var info = _runner.SessionInfo;
                return info.IsValid ? info.Name : null;
            }
        }

        public int PlayerCount
        {
            get
            {
                if (_runner == null)
                {
                    return 0;
                }

                var info = _runner.SessionInfo;
                return info.IsValid ? info.PlayerCount : 0;
            }
        }

        public int MaxPlayers
        {
            get
            {
                if (_configuredMaxPlayers > 0)
                {
                    return _configuredMaxPlayers;
                }

                if (_runner == null)
                {
                    return 0;
                }

                var info = _runner.SessionInfo;
                return info.IsValid ? info.MaxPlayers : 0;
            }
        }

        public string RoomDisplayName
        {
            get
            {
                if (_runner == null || !_runner.SessionInfo.IsValid)
                {
                    return null;
                }

                return SessionPropertyMapper.ReadString(
                    _runner.SessionInfo,
                    SessionPropertyKeys.DisplayName,
                    null);
            }
        }

        public int DestructionLimit => _destructionLimit;

        /// <summary>
        /// Connects to the matchmaking lobby so the room list starts arriving
        /// through <see cref="IRoomListSink"/>. Does not enter a room.
        /// </summary>
        public async UniTask<SessionStartResult> JoinLobbyAsync(CancellationToken cancellation)
        {
            await UniTask.WaitUntil(() => !IsRoomExitPending, cancellationToken: cancellation);
            if (IsRunning || _roomInitializationInProgress)
            {
                return SessionStartResult.Failed(
                    SessionFailure.AlreadyRunning, "A session is already running.");
            }

            CreateRunner(provideInput: false);
            _browsingLobby = true;

            // Held locally because this attempt may outlive its own claim on the
            // service: it is no longer cancelled when the browser closes, so a
            // newer attempt can take over before this one lands.
            var runner = _runner;

            StartGameResult result;
            try
            {
                result = await runner.JoinSessionLobby(
                    SessionLobby.ClientServer, null, null, null, cancellation);
            }
            catch (OperationCanceledException)
            {
                ReleaseAfterFusionShutdown(runner);
                throw;
            }

            if (!result.Ok)
            {
                var failure = SessionStartResult.Classify(result.ShutdownReason);

                ReleaseAfterFusionShutdown(runner);

                // Fusion answers a cancelled connect with a failed result rather
                // than by throwing, and leaving the browser mid-connect is the
                // ordinary way to reach it. Reported as the cancellation it is,
                // so callers unwind quietly instead of recording a verdict on
                // the project-wide browser state for the next screen to find.
                if (failure == SessionFailure.Canceled)
                {
                    throw new OperationCanceledException(
                        $"Joining the matchmaking lobby was cancelled. {result.ErrorMessage}");
                }

                Debug.LogError(
                    $"[Network] Could not join lobby: {failure} " +
                    $"({result.ShutdownReason}) {result.ErrorMessage}");

                return SessionStartResult.Failed(failure, result.ErrorMessage);
            }

            Debug.Log("[Network] Joined matchmaking lobby.");
            return SessionStartResult.Success();
        }

        public async UniTask<SessionStartResult> StartAsync(
            SessionRequest request, CancellationToken cancellation)
        {
            await UniTask.WaitUntil(() => !IsRoomExitPending, cancellationToken: cancellation);
            if (_roomInitializationInProgress)
                return SessionStartResult.Failed(SessionFailure.AlreadyRunning,
                    "Room initialization or its cleanup is still in progress.");
            if (_browsingLobby)
            {
                // The lobby runner cannot also play a session, so it is replaced.
                Shutdown();
            }
            else if (IsRunning)
            {
                return SessionStartResult.Failed(
                    SessionFailure.AlreadyRunning, "A session is already running.");
            }

            _expectedPassword = request.Password;
            _configuredMaxPlayers = request.MaxPlayers > 0
                ? request.MaxPlayers
                : 0;
            _destructionLimit = PlaySettingsDraft.DefaultDestructionLimit;

            var sceneManager = CreateRunner(request.Mode != GameMode.Server);

            // See JoinLobbyAsync: the attempt tears down its own runner, not
            // whichever one the service holds when the answer arrives.
            var runner = _runner;

            // Fusion is handed a linked token rather than the caller's own.
            // It registers a callback that shuts the runner down when the token
            // fires, and the token we are given belongs to whichever scene asked
            // to connect. That is right while connecting and wrong afterwards:
            // the session is a project-wide singleton, and loading a networked
            // scene destroys the scene that asked, which would otherwise cancel
            // the token and take the running session down with it. Cutting the
            // link once StartGame returns keeps cancellation working during the
            // attempt without letting a scene outlive its authority over it.
            var startCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellation);

            var args = new StartGameArgs
            {
                Config = ConfigureSession(NetworkProjectConfig.Global),
                GameMode = request.Mode,
                PlayerUniqueId = _playerUniqueId,
                SessionName = request.RoomCode,
                SessionProperties = SessionPropertyMapper.BuildForStart(
                    request,
                    SanitiseNickname(_profile?.Nickname)),
                ConnectionToken = SessionConnectionTokenCodec.Encode(
                    request.Password,
                    _profile?.Nickname),
                EnableClientSessionCreation = request.AllowCreate,
                SceneManager = sceneManager,
                Scene = CaptureCurrentScene(),
                StartGameCancellationToken = startCancellation.Token,
            };

            if (request.MaxPlayers > 0)
            {
                // Photon keeps the physical room at the project maximum so the
                // host may raise its chosen limit later. OnConnectRequest below
                // enforces the smaller, user-visible limit.
                args.PlayerCount = request.AllowCreate
                    ? RoomSettings.MaxPlayerCount
                    : request.MaxPlayers;
            }

            StartGameResult result;
            try
            {
                result = await runner.StartGame(args);
            }
            catch (OperationCanceledException)
            {
                ReleaseAfterFusionShutdown(runner);
                throw;
            }
            finally
            {
                // Disposing releases the callback Fusion registered, so the token
                // can never fire again. This one line is what stops a match scene
                // load from ending the session.
                startCancellation.Dispose();
            }

            if (!result.Ok)
            {
                var failure = SessionStartResult.Classify(result.ShutdownReason);

                ReleaseAfterFusionShutdown(runner);

                // Cancelled the same way a lobby connect is: the screen that
                // asked went away. See JoinLobbyAsync for why this unwinds
                // instead of answering.
                if (failure == SessionFailure.Canceled)
                {
                    throw new OperationCanceledException(
                        $"Starting session '{request.RoomCode}' was cancelled. " +
                        result.ErrorMessage);
                }

                Debug.LogError(
                    $"[Network] Could not start session '{request.RoomCode}' as {request.Mode}: " +
                    $"{failure} ({result.ShutdownReason}) {result.ErrorMessage}");

                return SessionStartResult.Failed(failure, result.ErrorMessage);
            }

            if (!IsCurrentRunner(runner) || !runner.IsRunning)
                throw new OperationCanceledException("The session start was superseded or stopped.");

            _roomInitializationInProgress = true;
            try
            {
                var initialized = await CompleteRoomInitializationAsync(() =>
                {
                    _isClientSession = runner.IsClient && !runner.IsServer;
                    _exitReported = false;
                    ReadConfiguredSettings();
                    // Publish again after Fusion finalizes the local player identity.
                    _spawner?.RefreshHost(runner);
                    _roster?.Refresh(runner);
                    _spawner?.SpawnRoomObjects(runner);
                    ReportPlayerCount();
                }, () => CleanupFailedRoomInitializationAsync(runner));

                if (initialized.Ok)
                    Debug.Log($"[Network] Session '{RoomCode}' started as {request.Mode}. IsServer={IsServer}");
                else
                    Debug.LogError($"[Network] Room initialization failed: {initialized.Detail}");
                return initialized;
            }
            finally
            {
                _roomInitializationInProgress = false;
            }
        }

        internal static NetworkProjectConfig ConfigureSession(NetworkProjectConfig config)
        {
            // Runtime-only policy; the serialized project settings remain available for restoration.
            // config.HostMigration.EnableAutoUpdate = true;
            config.HostMigration.EnableAutoUpdate = false;
            return config;
        }

        internal static async UniTask<SessionStartResult> CompleteRoomInitializationAsync(
            Action initialize, Func<UniTask> cleanup)
        {
            try
            {
                initialize();
                return SessionStartResult.Success();
            }
            catch (Exception failure)
            {
                // Do not report failure (and enable retry) until cleanup completes.
                await cleanup();
                if (failure is OperationCanceledException) throw;
                return SessionStartResult.Failed(SessionFailure.Unknown, failure.Message);
            }
        }

        private async UniTask CleanupFailedRoomInitializationAsync(NetworkRunner runner)
        {
            if (!IsCurrentRunner(runner)) return;
            _hostMigrationRevision++;
            _hostMigrationInProgress = false;
            _exitReported = true; // A room that failed to open is not a voluntary departure.
            ReleaseRunner();
            // This path follows a successful StartGame, unlike Fusion-owned start failures.
            // Dropping ownership first prevents callbacks or Shutdown() from stopping it twice.
            if (runner != null && runner.IsRunning)
                await runner.Shutdown();
            else
                ReleaseAfterFusionShutdown(runner);
        }

        public bool TryApplyLobbySettings(
            int maxPlayers,
            int destructionLimit,
            string mapId)
        {
            if (!IsServer || _runner == null || !_runner.SessionInfo.IsValid ||
                maxPlayers < RoomSettings.MinPlayerCount ||
                maxPlayers > RoomSettings.MaxPlayerCount ||
                maxPlayers < PlayerCount ||
                destructionLimit < PlaySettingsDraft.MinDestructionLimit ||
                destructionLimit > PlaySettingsDraft.MaxDestructionLimit ||
                !MapCatalog.Contains(mapId))
            {
                return false;
            }

            var properties = SessionPropertyMapper.BuildLobbySettings(
                maxPlayers,
                destructionLimit,
                mapId);

            if (!_runner.SessionInfo.UpdateCustomProperties(properties))
            {
                return false;
            }

            _configuredMaxPlayers = maxPlayers;
            _destructionLimit = destructionLimit;
            ReportPlayerCount();
            return true;
        }

        /// <summary>
        /// Asks the authority to start a match. Anyone may ask; the authority
        /// decides and answers only the peer that asked.
        /// </summary>
        public void RequestMatchStart()
        {
            if (!IsRunning || _browsingLobby || _runnerObject == null)
            {
                return;
            }

            _runnerObject.GetComponent<MatchStarter>()?.RequestStart(_runner);
        }

        public bool RequestReturnToLobby() =>
            IsRuntimeReady && !_browsingLobby &&
            _matchStarter != null &&
            _matchStarter.RequestReturnToLobby();

        /// <summary>Moves every seated player onto positions owned by the current scene.</summary>
        public void RepositionPlayers(IReadOnlyList<Pose> poses)
        {
            if (!IsServer)
            {
                return;
            }

            _spawner?.UseSpawnPoses(poses);
            if (!_hostMigrationInProgress) _spawner?.RepositionSeated(_runner);
        }

        public bool TryPublishMatchState(MatchStateSnapshot snapshot)
        {
            return IsServer && _matchStarter != null &&
                   _matchStarter.TryPublishSnapshot(snapshot);
        }

        public bool TryPublishItemAssignments(
            IReadOnlyList<PlayerItemAssignment> assignments)
        {
            if (!IsServer || _roster == null || _matchStarter == null || assignments == null)
            {
                return false;
            }

            var participants = new List<RoomParticipant>(assignments.Count);
            _roster.Capture(participants);
            if (assignments.Count == 0)
            {
                return false;
            }

            for (var index = 0; index < assignments.Count; index++)
            {
                var assignment = assignments[index];
                var playerIndex = assignment.PlayerIndex;
                var itemId = assignment.Item.ItemId?.Trim();
                if (string.IsNullOrEmpty(itemId) ||
                    !TryResolveAssignmentRecipient(_matchStarter.PlayingParticipants,
                        participants, playerIndex, out var playerId) ||
                    !_roster.TryGetPlayer(playerId, out var target))
                {
                    return false;
                }

                var payload = Encoding.UTF8.GetBytes(itemId);
                if (payload.Length > MaxItemAssignmentBytes)
                {
                    return false;
                }

                // Accept for delivery even if a restored avatar's connection is not back yet.
                // Its scene requests this published assignment again when ready.
                _publishedItemAssignments[playerId] = itemId;
                SendItemAssignment(target, itemId);
            }

            return true;
        }

        public bool RequestItemAssignment()
        {
            if (!IsRuntimeReady || _browsingLobby || !_runner.LocalPlayer.IsRealPlayer) return false;
            if (IsServer) return ResendItemAssignment(_runner.LocalPlayer);
            _runner.SendReliableDataToServer(ReliableKey.FromInts(
                ItemAssignmentRequestKeyType, ItemAssignmentKeyVersion, ++_itemAssignmentTransferSequence, 0),
                new byte[] { 1 });
            return true;
        }

        private bool ResendItemAssignment(PlayerRef requester)
        {
            if (!IsRuntimeReady || !IsServer || _matchStarter == null || !requester.IsRealPlayer ||
                !TryGetPublishedAssignment(_publishedItemAssignments, _matchStarter.PlayingParticipants,
                    PlayerRegistry.IdOf(requester), out var itemId)) return false;
            SendItemAssignment(requester, itemId);
            return true;
        }

        internal static bool TryGetPublishedAssignment(IReadOnlyDictionary<string, string> published,
            IReadOnlyList<MatchParticipant> playing, string requesterId, out string itemId)
        {
            itemId = null;
            foreach (var participant in playing)
                if (participant.PlayerId == requesterId)
                    return published.TryGetValue(requesterId, out itemId);
            return false;
        }

        private void SendItemAssignment(PlayerRef target, string itemId)
        {
            if (target == _runner.LocalPlayer)
                ItemAssignmentReceived?.Invoke(itemId);
            else
                _runner.SendReliableDataToPlayer(target, ReliableKey.FromInts(
                    ItemAssignmentKeyType, ItemAssignmentKeyVersion, ++_itemAssignmentTransferSequence, 0),
                    Encoding.UTF8.GetBytes(itemId));
        }

        internal static bool TryResolveAssignmentRecipient(
            IReadOnlyList<MatchParticipant> playing,
            IReadOnlyList<RoomParticipant> present,
            int playerIndex,
            out string playerId)
        {
            // Match indices stay frozen; the current room list shrinks on departure.
            playerId = null;
            if (playerIndex < 0 || playerIndex >= playing.Count) return false;
            var assignedPlayerId = playing[playerIndex].PlayerId;
            foreach (var participant in present)
            {
                if (participant.PlayerId != assignedPlayerId) continue;
                playerId = assignedPlayerId;
                return true;
            }
            return false;
        }

        public bool TryInitializeAssignedItems(
            IReadOnlyList<PlayerItemAssignment> assignments)
        {
            return IsServer && _matchStarter != null &&
                   _matchStarter.TryInitializeAssignedItems(assignments);
        }

        public bool TryPublishHighlightReplay(
            IReadOnlyList<HighlightReplayData> replay)
        {
            if (!IsServer || replay == null)
            {
                return false;
            }

            byte[] payload;
            try
            {
                payload = HighlightReplaySerializer.SerializeCompressed(replay);
            }
            catch (ArgumentException exception)
            {
                Debug.LogError($"[Match] Invalid highlight replay: {exception.Message}");
                return false;
            }

            var key = ReliableKey.FromInts(
                HighlightReplayKeyType,
                HighlightReplayKeyVersion,
                ++_highlightTransferSequence,
                0);
            _highlightPendingPlayers.Clear();
            foreach (var player in _runner.ActivePlayers) _highlightPendingPlayers.Add(player);
            Debug.Log($"[Highlight] Sending {payload.Length:N0} compressed bytes to {_highlightPendingPlayers.Count} peers.");
            HighlightReplayReceived?.Invoke(replay);
            foreach (var player in _runner.ActivePlayers)
            {
                if (player != _runner.LocalPlayer)
                {
                    _runner.SendReliableDataToPlayer(player, key, payload);
                }
            }

            return true;
        }

        public bool TrySetPlayerControls(int playerIndex, bool enabled)
        {
            return IsServer && _matchStarter != null &&
                   _matchStarter.TrySetPlayerControls(playerIndex, enabled);
        }

        public bool TryTeleportPlayer(int playerIndex, Pose pose)
        {
            return IsServer && _matchStarter != null &&
                   _matchStarter.TryTeleportPlayer(playerIndex, pose);
        }

        public bool BindMatchSession(
            MatchSessionCoordinator session,
            Pose shredderEjectionPose)
        {
            if (!IsServer || _matchStarter == null || session == null)
            {
                return false;
            }

            _matchStarter.BindSession(session, shredderEjectionPose);
            MatchMigration = null;
            return true;
        }

        public bool UnbindMatchSession(MatchSessionCoordinator session)
        {
            return _matchStarter != null &&
                   _matchStarter.UnbindSession(session);
        }

        public bool RequestHoldObject(string objectId) =>
            _matchStarter != null && _matchStarter.RequestHoldObject(objectId);

        public bool RequestDropHeldObject(Pose pose) =>
            _matchStarter != null && _matchStarter.RequestDropHeldObject(pose);

        public bool RequestReleaseHeldObject(Pose pose) =>
            _matchStarter != null && _matchStarter.RequestReleaseHeldObject(pose);

        public bool RequestThrowHeldObject(Pose pose, Vector3 initialVelocity) =>
            _matchStarter != null &&
            _matchStarter.RequestThrowHeldObject(pose, initialVelocity);

        public bool TryConfirmObjectSettled(
            string objectId,
            Pose pose,
            int expectedVersion) =>
            IsServer && _matchStarter != null &&
            _matchStarter.TryConfirmObjectSettled(
                objectId,
                pose,
                expectedVersion);

        public bool RequestHitPlayer(int targetPlayerIndex) =>
            _matchStarter != null && _matchStarter.RequestHitPlayer(targetPlayerIndex);

        public bool RequestUseShredder() =>
            _matchStarter != null && _matchStarter.RequestUseShredder();

        /// <summary>
        /// Leaves the current session. Fusion tears the runner down itself, so
        /// this does not await anything.
        /// </summary>
        public void Shutdown()
        {
            _hostMigrationRevision++;
            _hostMigrationInProgress = false;
            var runner = _runner;

            // A voluntary room exit must be reported before references are
            // cleared. Lobby runners and runners that never started a session
            // do not represent a room departure.
            if (runner != null && runner.IsRunning && !_browsingLobby)
            {
                if (runner.IsServer && runner.SessionInfo.IsValid)
                {
                    try
                    {
                        runner.SessionInfo.IsOpen = false;
                        runner.SessionInfo.IsVisible = false;
                    }
                    catch (Exception exception)
                    {
                        // Losing the cloud connection must not prevent local cleanup.
                        Debug.LogWarning($"[Network] Could not hide the closing room: {exception.Message}");
                    }
                }
                ReportExit(RoomExitReason.Left);
            }

            ReleaseRunner();

            // Unity's equality covers already destroyed instances.
            if (runner == null)
            {
                return;
            }

            if (runner.IsRunning)
            {
                // Fusion detaches its callbacks and destroys the runner object
                // as part of its own teardown, so we must not race it by
                // removing callbacks ourselves.
                runner.Shutdown();
            }
            else
            {
                UnityEngine.Object.Destroy(runner.gameObject);
            }
        }

        /// <summary>
        /// Clears this service after Fusion has already torn the runner down on
        /// its own, which is how every failed start arrives.
        /// </summary>
        /// <remarks>
        /// A refused or cancelled <c>StartGame</c> reaches us through Fusion's
        /// <c>ShutdownAndBuildResult</c>, so the runner is already stopping by
        /// the time the result is read. Calling <see cref="Shutdown"/> here
        /// would re-enter <c>NetworkRunner.Shutdown</c> from inside Fusion's own
        /// teardown.
        /// <para>
        /// Cancellation makes that fatal rather than merely redundant. The token
        /// belongs to the screen, so it fires from
        /// <c>LifetimeScope.OnDestroy</c>, and a token fires its registrations
        /// synchronously: the continuation runs on the destroy stack, in the
        /// middle of a scene load, and takes Fusion down re-entrantly with it.
        /// Leaving the room browser while it was still connecting froze the game
        /// exactly this way.
        /// </para>
        /// </remarks>
        /// <param name="runner">
        /// The runner the failed attempt started, which is not always the one
        /// this service holds now. An abandoned connect finishes on its own
        /// since it is no longer cancelled, and can land after a newer attempt
        /// has taken over, so its own runner is the only thing it may clear.
        /// </param>
        private void ReleaseAfterFusionShutdown(NetworkRunner runner)
        {
            // No room departure to report: a session that never started is not
            // one this peer can leave.
            if (IsCurrentRunner(runner))
            {
                ReleaseRunner();
            }

            // Unity's equality covers already destroyed instances.
            if (runner == null)
            {
                return;
            }

            // Still running means Fusion's teardown is mid-flight and owns the
            // object; it destroys the runner as it finishes. Only one Fusion has
            // already let go of can be left behind, and that one is ours.
            if (!runner.IsRunning)
            {
                UnityEngine.Object.Destroy(runner.gameObject);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Shutdown();
        }

        /// <summary>
        /// Builds the runner object. Fusion needs a scene manager on the same
        /// object to drive networked scene loading, so it is added here.
        /// </summary>
        /// <param name="provideInput">
        /// False for a dedicated server, which has no local player and therefore
        /// no input to contribute.
        /// </param>
        private INetworkSceneManager CreateRunner(bool provideInput)
        {
            _hostLossShutdownPending = false;
            _isClientSession = false;
            // Photon room lists are region-local. Leaving this empty lets each
            // PC choose a different "best" region, so teammates can create a
            // valid room that the others can never list.
            Fusion.Photon.Realtime.PhotonAppSettings.Global.AppSettings.FixedRegion =
                MatchmakingRegion;

            _runnerObject = new GameObject(RunnerObjectName);
            UnityEngine.Object.DontDestroyOnLoad(_runnerObject);

            var sceneManager = _runnerObject.AddComponent<NetworkSceneManagerDefault>();

            _runner = _runnerObject.AddComponent<NetworkRunner>();
            _runner.ProvideInput = provideInput;
            _runner.AddCallbacks(this);

            // Voice rides on the same object because its client reads the runner
            // for the session it should follow. A dedicated server has no
            // microphone and nobody to hear it, so it does not carry one.
            Voice = provideInput ? VoiceRig.Attach(_runner) : null;

            // Sits on the runner so that characters, which Fusion spawns and the
            // container therefore cannot inject, can still reach it.
            _roster = _runnerObject.AddComponent<PlayerRoster>();
            _roster.Bind(_participantSink);
            _matchStarter = _runnerObject.AddComponent<MatchStarter>();

            // This service is the scene director: it already owns the runner's
            // scene manager, the initial scene and the scene callbacks, so the
            // starter can confirm a line-up without learning what a scene is.
            _matchStarter.Bind(_matchStartSink, _roster, this);
            _matchStarter.MatchStateReceived += OnMatchStateReceived;
            _matchStarter.LobbyChatReceived += OnLobbyChatReceived;
            _matchStarter.ObjectStatesReceived += OnObjectStatesReceived;
            _matchStarter.ItemDestroyedReceived += OnItemDestroyedReceived;
            _matchStarter.PlayerStunnedReceived += OnPlayerStunnedReceived;
            _matchStarter.ObjectThrownReceived += OnObjectThrownReceived;
            _matchStarter.FinalWarningReceived += OnFinalWarningReceived;
            _matchStarter.ParticipantActivityReceived += OnParticipantActivityReceived;
            _matchStarter.PlayerInteractionStatesReceived +=
                OnPlayerInteractionStatesReceived;
            _matchStarter.MatchResultReceived += OnMatchResultReceived;
            _matchStarter.LineUpReceived += OnLineUpReceived;
            _matchStarter.SimulationTick += OnSimulationTick;

            return sceneManager;
        }

        /// <summary>
        /// The scene the session opens in, so that Fusion has something to
        /// replicate as the current scene from the start.
        /// </summary>
        /// <remarks>
        /// Without this the runner reports <c>started with no scene</c> and scene
        /// synchronisation never engages, which makes a later
        /// <see cref="EnterMatchScene"/> a no-op. The scene has to be in the
        /// build list to have an index Fusion can send, and one that is not is
        /// left empty rather than guessed at: a wrong index would load a
        /// different scene on the clients than the host is in.
        /// </remarks>
        private static NetworkSceneInfo CaptureCurrentScene()
        {
            var info = new NetworkSceneInfo();
            var active = SceneManager.GetActiveScene();

            if (active.buildIndex < 0 ||
                active.buildIndex >= SceneManager.sceneCountInBuildSettings)
            {
                Debug.LogWarning(
                    $"[Session] '{active.name}' is not in the build scene list, " +
                    "so the session starts without a synchronised scene. Add it " +
                    "under File > Build Profiles.");

                return info;
            }

            // Additive, not Single. This only tells Fusion which scene the
            // session begins in; the scene is already loaded. Declaring it as
            // Single invites the scene manager to reload it while the peer is
            // still connecting, which would destroy the scope that is driving
            // the connection. Replacing the scene is what EnterMatchScene does.
            info.AddSceneRef(SceneRef.FromIndex(active.buildIndex), LoadSceneMode.Additive);
            return info;
        }

        /// <summary>
        /// Takes the room into the map. Only the authority may change the
        /// networked scene; Fusion carries the change to everyone else.
        /// </summary>
        public void EnterMatchScene(NetworkRunner runner)
        {
            if (runner == null || !runner.IsRunning || !runner.IsServer)
            {
                return;
            }

            if (_scenes == null)
            {
                Debug.LogError(
                    "[Session] No NetworkScenes asset is assigned, so the match " +
                    "cannot move into a map. Set it on ProjectLifetimeScope.");
                return;
            }

            var scene = _scenes.MatchScene;

            if (!scene.IsValid)
            {
                // NetworkScenes has already said which of the two reasons it is.
                return;
            }

            // Single, not Additive: the lobby scene would otherwise stay loaded
            // behind the map, leaving two cameras rendering and the lobby's
            // geometry inside it.
            runner.LoadScene(scene, LoadSceneMode.Single);
            Debug.Log("[Session] Loading the match scene for everyone.");
        }

        public bool IsResultSceneLoaded { get; private set; }

        public bool EnterResultScene()
        {
            if (!IsServer || !IsRuntimeReady) return false;
            if (IsResultSceneLoaded) return true;
            if (_scenes == null)
            {
                Debug.LogError("[Session] NetworkScenes must be assigned to load results.");
                return false;
            }
            var scene = _scenes.ResultScene;
            if (!scene.IsValid) return false;
            _runner.LoadScene(scene, LoadSceneMode.Single);
            return true;
        }

        public bool EnterLobbyScene(NetworkRunner runner)
        {
            if (runner == null || !runner.IsRunning || !runner.IsServer)
            {
                return false;
            }

            if (_scenes == null)
            {
                Debug.LogError(
                    "[Session] No NetworkScenes asset is assigned, so the room " +
                    "cannot return to the lobby. Set it on ProjectLifetimeScope.");
                return false;
            }

            var scene = _scenes.LobbyScene;
            if (!scene.IsValid)
            {
                return false;
            }

            runner.LoadScene(scene, LoadSceneMode.Single);
            Debug.Log("[Session] Returning everyone to the lobby scene.");
            return true;
        }

        /// <summary>
        /// Enters the room lobby through Fusion after a room create or join.
        /// The host publishes the scene change; clients receive the host's scene
        /// through the normal Fusion scene synchronisation path.
        /// </summary>
        public bool EnterLobbyScene()
        {
            if (!IsRunning || _browsingLobby)
            {
                return false;
            }

            return !_runner.IsServer || EnterLobbyScene(_runner);
        }

        /// <summary>
        /// Drops our references to the runner without touching it, so the caller
        /// can decide how to tear it down.
        /// </summary>
        private void ReleaseRunner(bool preserveMigrationState = false)
        {
            _publishedItemAssignments.Clear();
            _matchRuntimeRestorePending = false;
            _matchRuntimeRestoreFailure = null;
            MatchMigration = null;
            IsResultSceneLoaded = false;
            _highlightPendingPlayers.Clear();
            _receivedHighlightSequence = 0;
            // The rig is a component on the runner object and goes down with it.
            // A caller that kept talking to it afterwards would be talking to a
            // destroyed component.
            Voice = null;

            // A room list is a snapshot owned by the current lobby runner. Once
            // that runner is gone, retaining its last snapshot shows rooms that
            // may already have disappeared when the browser is opened again.
            _roomBuffer.Clear();
            _roomListSink?.SetRooms(_roomBuffer);

            // Emptied while the runner object still exists. It is destroyed with
            // the session, and presentation would otherwise keep showing the
            // people who were in the room we just left.
            if (_runnerObject != null)
            {
                _runnerObject.GetComponent<PlayerRoster>()?.Clear();
                _matchStarter?.Clear();
            }

            if (_matchStarter != null)
            {
                _matchStarter.MatchStateReceived -= OnMatchStateReceived;
                _matchStarter.LobbyChatReceived -= OnLobbyChatReceived;
                _matchStarter.ObjectStatesReceived -= OnObjectStatesReceived;
                _matchStarter.ItemDestroyedReceived -= OnItemDestroyedReceived;
                _matchStarter.PlayerStunnedReceived -= OnPlayerStunnedReceived;
                _matchStarter.ObjectThrownReceived -= OnObjectThrownReceived;
                _matchStarter.FinalWarningReceived -= OnFinalWarningReceived;
                _matchStarter.ParticipantActivityReceived -= OnParticipantActivityReceived;
                _matchStarter.PlayerInteractionStatesReceived -=
                    OnPlayerInteractionStatesReceived;
                _matchStarter.MatchResultReceived -= OnMatchResultReceived;
                _matchStarter.LineUpReceived -= OnLineUpReceived;
                _matchStarter.SimulationTick -= OnSimulationTick;
            }

            _runner = null;
            _runnerObject = null;
            _roster = null;
            _matchStarter = null;
            _localInputMotor = null;
            if (!preserveMigrationState)
            {
                _expectedPassword = null;
                _configuredMaxPlayers = 0;
                _destructionLimit = PlaySettingsDraft.DefaultDestructionLimit;
            }
            _browsingLobby = false;

            // The characters go with the session, but the seating does not clear
            // itself. Left behind, the next room would start numbering from
            // wherever the last one stopped.
            if (!preserveMigrationState)
            {
                _spawner?.Clear();
            }
        }

        private void OnMatchStateReceived(MatchStateSnapshot snapshot)
        {
            MatchStateReceived?.Invoke(snapshot);
        }

        private void OnLobbyChatReceived(LobbyChatMessage message)
        {
            ChatReceived?.Invoke(message);
        }

        private void OnObjectStatesReceived(
            IReadOnlyList<MatchObjectStateSnapshot> states)
        {
            ObjectStatesReceived?.Invoke(states);
        }

        private void OnItemDestroyedReceived(PlayerItemDestroyedEvent confirmedEvent)
        {
            ItemDestroyedReceived?.Invoke(confirmedEvent);
        }

        private void OnPlayerStunnedReceived(PlayerStunnedEvent confirmedEvent)
        {
            PlayerStunnedReceived?.Invoke(confirmedEvent);
        }

        private void OnObjectThrownReceived(ObjectThrownEvent confirmedEvent)
        {
            ObjectThrownReceived?.Invoke(confirmedEvent);
        }

        private void OnFinalWarningReceived(FinalWarningStartedEvent confirmedEvent)
        {
            FinalWarningReceived?.Invoke(confirmedEvent);
        }

        private void OnParticipantActivityReceived(IReadOnlyList<bool> active)
        {
            ParticipantActivityReceived?.Invoke(active);
        }

        private void OnPlayerInteractionStatesReceived(
            IReadOnlyList<PlayerInteractionStateSnapshot> states)
        {
            PlayerInteractionStatesReceived?.Invoke(states);
        }

        private void OnMatchResultReceived(MatchResult result)
        {
            MatchResultReceived?.Invoke(result);
        }

        private void OnLineUpReceived(IReadOnlyList<MatchParticipant> participants)
        {
            if (participants == null || participants.Count == 0)
            {
                MatchMigration = null;
                _publishedItemAssignments.Clear();
            }
            LineUpReceived?.Invoke(participants);
        }

        private void OnSimulationTick()
        {
            // During migration only the requested runtime restoration may consume this tick.
            if (!IsRuntimeReady && !IsMatchRuntimeRestorePending) return;
            SimulationTick?.Invoke();
        }

        private bool IsCurrentRunner(NetworkRunner runner) =>
            ReferenceEquals(runner, _runner);

        private void ReadConfiguredSettings()
        {
            if (_runner == null || !_runner.SessionInfo.IsValid)
            {
                return;
            }

            var info = _runner.SessionInfo;
            _configuredMaxPlayers = SessionPropertyMapper.ReadInt(
                info,
                SessionPropertyKeys.MaxPlayers,
                info.MaxPlayers);
            _destructionLimit = SessionPropertyMapper.ReadInt(
                info,
                SessionPropertyKeys.DestructionLimit,
                PlaySettingsDraft.DefaultDestructionLimit);
        }

        /// <summary>
        /// Counts the runner's live players rather than reading the session
        /// listing, which lags a tick behind a player leaving.
        /// </summary>
        private void ReportPlayerCount()
        {
            if (_runner == null || _browsingLobby || _exitReported)
            {
                return;
            }

            _sessionSink?.PlayerCountChanged(
                CountActivePlayers(_runner),
                MaxPlayers);
        }

        private static int CountActivePlayers(NetworkRunner runner)
        {
            var count = 0;
            foreach (var _ in runner.ActivePlayers)
            {
                count++;
            }

            return count;
        }

        /// <summary>
        /// Reports the departure once. Fusion can raise a disconnect and a
        /// shutdown for the same exit, and presentation should react once.
        /// </summary>
        private void ReportExit(RoomExitReason reason)
        {
            if (_exitReported || _browsingLobby)
            {
                return;
            }

            _exitReported = true;
            _departingRunner = _runner;
            Debug.Log($"[Network] Left the room: {reason}");
            _sessionSink?.RoomClosed(reason);
        }

        internal static RoomExitReason ResolveUnexpectedExit(bool clientSession, RoomExitReason reason) =>
            clientSession ? RoomExitReason.HostClosed : reason;

        private static RoomExitReason Translate(ShutdownReason reason)
        {
            switch (reason)
            {
                case ShutdownReason.Ok:
                case ShutdownReason.OperationCanceled:
                    // The only way this peer stops cleanly is by asking to.
                    return RoomExitReason.Left;

                case ShutdownReason.GameClosed:
                case ShutdownReason.ServerInRoom:
                case ShutdownReason.HostMigration:
                // Observed when the authority leaves: Fusion reports
                // DisconnectReason=ServerLogic, "Server has disconnected".
                case ShutdownReason.DisconnectedByPluginLogic:
                    return RoomExitReason.HostClosed;

                case ShutdownReason.ConnectionTimeout:
                case ShutdownReason.ConnectionRefused:
                case ShutdownReason.PhotonCloudTimeout:
                case ShutdownReason.OperationTimeout:
                    return RoomExitReason.Disconnected;

                default:
                    return RoomExitReason.Unknown;
            }
        }

        private static RoomExitReason Translate(NetDisconnectReason reason)
        {
            switch (reason)
            {
                case NetDisconnectReason.Requested:
                    return RoomExitReason.Left;

                case NetDisconnectReason.ByRemote:
                    // The authority closed the connection from its side.
                    return RoomExitReason.HostClosed;

                case NetDisconnectReason.Timeout:
                case NetDisconnectReason.SendWindowFull:
                case NetDisconnectReason.ProtocolError:
                case NetDisconnectReason.SequenceOutOfBounds:
                    return RoomExitReason.Disconnected;

                default:
                    return RoomExitReason.Unknown;
            }
        }

    }
}
