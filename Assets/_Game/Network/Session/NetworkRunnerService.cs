using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using Game.Core.Home;
using Game.Core.Ports;
using Game.Core.Rooms;
using Game.Core.Match;
using Game.Core.Items;
using Game.Network.Lobby;
using Game.Network.Match;
using Game.Network.Players;
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
    public sealed class NetworkRunnerService :
        INetworkRunnerCallbacks,
        IMatchSceneDirector,
        INetworkMatchRuntimeSource,
        INetworkMatchAuthority,
        INetworkMatchEvents,
        IDisposable
    {
        private const string RunnerObjectName = "[NetworkRunner]";

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

        private readonly List<RoomSummary> _roomBuffer = new List<RoomSummary>();

        private NetworkRunner _runner;
        private GameObject _runnerObject;
        private PlayerRoster _roster;
        private MatchStarter _matchStarter;

        public event Action<MatchStateSnapshot> MatchStateReceived;
        public event Action<string> ItemAssignmentReceived;
        public event Action<IReadOnlyList<MatchObjectStateSnapshot>> ObjectStatesReceived;
        public event Action<PlayerItemDestroyedEvent> ItemDestroyedReceived;
        public event Action<PlayerStunnedEvent> PlayerStunnedReceived;
        public event Action<ObjectThrownEvent> ObjectThrownReceived;
        public event Action<FinalWarningStartedEvent> FinalWarningReceived;
        public event Action<IReadOnlyList<bool>> ParticipantActivityReceived;
        public event Action<IReadOnlyList<PlayerInteractionStateSnapshot>>
            PlayerInteractionStatesReceived;
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
        private bool _exitReported;

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

            DecodeToken(runner.GetPlayerConnectionToken(player), out _, out var presented);
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

        /// <summary>True while the room list is being received.</summary>
        public bool IsBrowsingLobby => _browsingLobby && IsRunning;

        /// <summary>
        /// True on whichever peer holds authority. Identical for a player host
        /// and a dedicated server, so gameplay never asks which one it is.
        /// </summary>
        public bool IsServer => _runner != null && _runner.IsServer;

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
                if (_runner == null)
                {
                    return 0;
                }

                var info = _runner.SessionInfo;
                return info.IsValid ? info.MaxPlayers : 0;
            }
        }

        /// <summary>
        /// Connects to the matchmaking lobby so the room list starts arriving
        /// through <see cref="IRoomListSink"/>. Does not enter a room.
        /// </summary>
        public async UniTask<SessionStartResult> JoinLobbyAsync(CancellationToken cancellation)
        {
            if (IsRunning)
            {
                return SessionStartResult.Failed(
                    SessionFailure.AlreadyRunning, "A session is already running.");
            }

            CreateRunner(provideInput: false);
            _browsingLobby = true;

            StartGameResult result;
            try
            {
                result = await _runner.JoinSessionLobby(
                    SessionLobby.ClientServer, null, null, null, cancellation);
            }
            catch (OperationCanceledException)
            {
                Shutdown();
                throw;
            }

            if (!result.Ok)
            {
                var failure = SessionStartResult.Classify(result.ShutdownReason);
                Debug.LogError(
                    $"[Network] Could not join lobby: {failure} " +
                    $"({result.ShutdownReason}) {result.ErrorMessage}");

                Shutdown();
                return SessionStartResult.Failed(failure, result.ErrorMessage);
            }

            Debug.Log("[Network] Joined matchmaking lobby.");
            return SessionStartResult.Success();
        }

        public async UniTask<SessionStartResult> StartAsync(
            SessionRequest request, CancellationToken cancellation)
        {
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

            var sceneManager = CreateRunner(request.Mode != GameMode.Server);

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
                GameMode = request.Mode,
                SessionName = request.RoomCode,
                SessionProperties = BuildProperties(request, _profile?.Nickname),
                ConnectionToken = EncodeToken(request.Password, _profile?.Nickname),
                EnableClientSessionCreation = request.AllowCreate,
                SceneManager = sceneManager,
                Scene = CaptureCurrentScene(),
                StartGameCancellationToken = startCancellation.Token,
            };

            if (request.MaxPlayers > 0)
            {
                args.PlayerCount = request.MaxPlayers;
            }

            StartGameResult result;
            try
            {
                result = await _runner.StartGame(args);
            }
            catch (OperationCanceledException)
            {
                Shutdown();
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
                Debug.LogError(
                    $"[Network] Could not start session '{request.RoomCode}' as {request.Mode}: " +
                    $"{failure} ({result.ShutdownReason}) {result.ErrorMessage}");

                Shutdown();
                return SessionStartResult.Failed(failure, result.ErrorMessage);
            }

            _exitReported = false;

            Debug.Log(
                $"[Network] Session '{RoomCode}' started as {request.Mode}. IsServer={IsServer}");

            // The room needs somewhere to record that a match started before
            // anyone can ask for one, and only the authority may create it.
            _spawner?.SpawnRoomObjects(_runner);

            ReportPlayerCount();
            return SessionStartResult.Success();
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

        public bool TryPublishMatchState(MatchStateSnapshot snapshot)
        {
            return IsServer && _matchStarter != null &&
                   _matchStarter.TryPublishSnapshot(snapshot);
        }

        public bool TryPublishItemAssignments(
            IReadOnlyList<PlayerItemAssignment> assignments)
        {
            return IsServer && _matchStarter != null &&
                   _matchStarter.TryPublishItemAssignments(assignments);
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
            return true;
        }

        public bool UnbindMatchSession(MatchSessionCoordinator session)
        {
            return _matchStarter != null &&
                   _matchStarter.UnbindSession(session);
        }

        public bool RequestHoldObject(string objectId) =>
            _matchStarter != null && _matchStarter.RequestHoldObject(objectId);

        public bool RequestReleaseHeldObject(Pose pose) =>
            _matchStarter != null && _matchStarter.RequestReleaseHeldObject(pose);

        public bool RequestThrowHeldObject(Pose pose, Vector3 initialVelocity) =>
            _matchStarter != null &&
            _matchStarter.RequestThrowHeldObject(pose, initialVelocity);

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
            var runner = _runner;

            // A voluntary room exit must be reported before references are
            // cleared. Lobby runners and runners that never started a session
            // do not represent a room departure.
            if (runner != null && runner.IsRunning && !_browsingLobby)
            {
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
            _runnerObject = new GameObject(RunnerObjectName);
            UnityEngine.Object.DontDestroyOnLoad(_runnerObject);

            var sceneManager = _runnerObject.AddComponent<NetworkSceneManagerDefault>();

            _runner = _runnerObject.AddComponent<NetworkRunner>();
            _runner.ProvideInput = provideInput;
            _runner.AddCallbacks(this);

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
            _matchStarter.ItemAssignmentReceived += OnItemAssignmentReceived;
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

        /// <summary>
        /// Drops our references to the runner without touching it, so the caller
        /// can decide how to tear it down.
        /// </summary>
        private void ReleaseRunner()
        {
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
                _matchStarter.ItemAssignmentReceived -= OnItemAssignmentReceived;
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
            _expectedPassword = null;
            _browsingLobby = false;

            // The characters go with the session, but the seating does not clear
            // itself. Left behind, the next room would start numbering from
            // wherever the last one stopped.
            _spawner?.Clear();
        }

        private void OnMatchStateReceived(MatchStateSnapshot snapshot)
        {
            MatchStateReceived?.Invoke(snapshot);
        }

        private void OnItemAssignmentReceived(string itemId)
        {
            ItemAssignmentReceived?.Invoke(itemId);
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
            LineUpReceived?.Invoke(participants);
        }

        private void OnSimulationTick()
        {
            SimulationTick?.Invoke();
        }

        private bool IsCurrentRunner(NetworkRunner runner) =>
            ReferenceEquals(runner, _runner);

        /// <summary>
        /// Session properties are readable by anyone browsing the lobby, so only
        /// the peer opening the room writes them and no secret goes in.
        /// </summary>
        private static Dictionary<string, SessionProperty> BuildProperties(
            in SessionRequest request, string nickname)
        {
            if (request.Mode == GameMode.Client)
            {
                return null;
            }

            var properties = new Dictionary<string, SessionProperty>();

            if (!string.IsNullOrEmpty(request.DisplayName))
            {
                properties[SessionPropertyKeys.DisplayName] = request.DisplayName;
            }

            if (!string.IsNullOrEmpty(request.MapId))
            {
                properties[SessionPropertyKeys.MapId] = request.MapId;
            }

            var hostNickname = SanitiseNickname(nickname);

            if (hostNickname.Length > 0)
            {
                properties[SessionPropertyKeys.HostNickname] = hostNickname;
            }

            properties[SessionPropertyKeys.Locked] = !string.IsNullOrEmpty(request.Password);

            return properties;
        }

        /// <summary>
        /// Counts the runner's live players rather than reading the session
        /// listing, which lags a tick behind a player leaving.
        /// </summary>
        private void ReportPlayerCount()
        {
            if (_runner == null || _browsingLobby)
            {
                return;
            }

            var count = 0;
            foreach (var _ in _runner.ActivePlayers)
            {
                count++;
            }

            _sessionSink?.PlayerCountChanged(count, MaxPlayers);
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
            Debug.Log($"[Network] Left the room: {reason}");
            _sessionSink?.RoomClosed(reason);
        }

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

        /// <summary>Marks the layout below, so a change can be recognised.</summary>
        private const byte TokenVersion = 1;

        /// <summary>Version byte plus the two bytes holding the password length.</summary>
        private const int TokenHeaderSize = 3;

        /// <summary>
        /// Packs what a joiner hands the host before it is let in: the password it
        /// presents, and the name it asks to be shown as.
        /// </summary>
        /// <remarks>
        /// The password is length-prefixed rather than separated by a character.
        /// A password may contain anything, so any separator could occur inside
        /// one and split it in the wrong place, refusing a correct password.
        /// <para>
        /// The nickname travels here because the alternative is an RPC from the
        /// client, and RPCs do not work from this assembly. It arrives before the
        /// character is spawned, which is exactly when the authority needs it.
        /// </para>
        /// </remarks>
        internal static byte[] EncodeToken(string password, string nickname)
        {
            var passwordBytes = Encoding.UTF8.GetBytes(password ?? string.Empty);
            var nicknameBytes = Encoding.UTF8.GetBytes(nickname ?? string.Empty);

            if (passwordBytes.Length == 0 && nicknameBytes.Length == 0)
            {
                return null;
            }

            if (passwordBytes.Length > ushort.MaxValue)
            {
                // Unreachable with any real password, and silently truncating one
                // would refuse a join for a reason nobody could see.
                Debug.LogError("[Network] The password is too long to send.");
                return null;
            }

            var token = new byte[TokenHeaderSize + passwordBytes.Length + nicknameBytes.Length];
            token[0] = TokenVersion;
            token[1] = (byte)(passwordBytes.Length >> 8);
            token[2] = (byte)(passwordBytes.Length & 0xFF);

            passwordBytes.CopyTo(token, TokenHeaderSize);
            nicknameBytes.CopyTo(token, TokenHeaderSize + passwordBytes.Length);

            return token;
        }

        /// <summary>
        /// Reads a token back. Anything unreadable yields empty values rather
        /// than an exception: the bytes come from another peer, so a malformed
        /// token is a thing that happens and not a bug to crash on.
        /// </summary>
        internal static void DecodeToken(byte[] token, out string password, out string nickname)
        {
            password = string.Empty;
            nickname = string.Empty;

            if (token == null || token.Length < TokenHeaderSize || token[0] != TokenVersion)
            {
                return;
            }

            var passwordLength = (token[1] << 8) | token[2];

            if (TokenHeaderSize + passwordLength > token.Length)
            {
                return;
            }

            password = Encoding.UTF8.GetString(token, TokenHeaderSize, passwordLength);

            var nicknameStart = TokenHeaderSize + passwordLength;
            nickname = Encoding.UTF8.GetString(
                token, nicknameStart, token.Length - nicknameStart);
        }

        private static bool Matches(string presented, string expected)
        {
            return !string.IsNullOrEmpty(expected)
                   && string.Equals(presented, expected, StringComparison.Ordinal);
        }

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
                _matchStarter?.TryHandlePlayerLeft(player);
            }

            _spawner?.Despawn(runner, player);
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

            if (string.IsNullOrEmpty(_expectedPassword))
            {
                request.Accept();
                return;
            }

            DecodeToken(token, out var presented, out _);

            // Only the password admits anyone. The room code says which room to
            // reach and grants nothing, so a code read off the browser listing
            // is useless without the password. The nickname in the same token
            // grants nothing either and is not read here.
            if (Matches(presented, _expectedPassword))
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

            var playerObject = runner.GetPlayerObject(runner.LocalPlayer);
            var motor = playerObject == null
                ? null
                : playerObject.GetComponent<NetworkPlayerMotor>();

            input.Set(motor == null ? default : motor.CaptureInput());
        }

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
        {
            input.Set(default(NetworkPlayerInput));
        }

        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

        public void OnSceneLoadStart(NetworkRunner runner) { }

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
            Debug.Log(
                $"[Session] Scene load done: '{SceneManager.GetActiveScene().name}', " +
                $"IsServer={runner != null && runner.IsServer}.");

            SceneLoaded?.Invoke();
            _spawner?.RepositionSeated(runner);
        }

        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

        public void OnCustomAuthenticationResponse(
            NetworkRunner runner, Dictionary<string, object> data) { }

        public void OnReliableDataProgress(
            NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

        public void OnReliableDataReceived(
            NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }

        // The parameter type is obsolete in Fusion's own interface declaration,
        // so implementing this callback at all raises CS0618. Nothing to fix on
        // our side until Fusion changes the signature.
#pragma warning disable CS0618
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
#pragma warning restore CS0618
    }
}
