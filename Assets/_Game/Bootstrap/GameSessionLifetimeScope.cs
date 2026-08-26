using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Home;
using Game.Core.Lobby;
using Game.Core.Maps;
using Game.Core.Rooms;
using Game.Network.Players;
using Game.Network.Session;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// What this scene's instance should do on play.
    /// </summary>
    public enum SessionStartMode
    {
        /// <summary>Watch the room list without entering a room.</summary>
        BrowseLobby = 0,

        /// <summary>Open a new room and take authority over it.</summary>
        CreateRoom = 1,

        /// <summary>
        /// Enter an existing room using the code below, presenting the password.
        /// </summary>
        EnterByCode = 2,

        /// <summary>Enter the first listed room, presenting the password below.</summary>
        EnterFirstListed = 3,
    }

    /// <summary>
    /// Scene scope that reaches matchmaking when its scene plays.
    /// </summary>
    /// <remarks>
    /// This deliberately does not live in <see cref="ProjectLifetimeScope"/>.
    /// That scope is a preloaded asset and therefore loads in every scene, so
    /// connecting there would reach Photon even when someone only wanted to
    /// inspect a map. Only scenes carrying this component connect.
    /// <para>
    /// Temporary scaffolding: it drives matchmaking from the inspector until the
    /// lobby screen exists to drive it from player choices.
    /// </para>
    /// </remarks>
    public sealed class GameSessionLifetimeScope : LifetimeScope
    {
        [SerializeField]
        [Tooltip("What this instance does on play.")]
        private SessionStartMode _mode = SessionStartMode.CreateRoom;

        [Header("Create room")]
        [SerializeField]
        [Tooltip("Name shown in the room list.")]
        private string _displayName = "Test room";

        [SerializeField]
        [Tooltip("Maximum players allowed in the room.")]
        private int _maxPlayers = 6;

        [SerializeField]
        [Tooltip("Leave empty to open the room to anyone.")]
        private string _password = string.Empty;

        [Header("Enter by code")]
        [SerializeField]
        [Tooltip("Code issued by the instance that opened the room.")]
        private string _roomCode = string.Empty;

        [Header("Spawning")]
        [SerializeField]
        [Tooltip("Marks where characters appear. Leave empty and they are " +
                 "placed in a ring around the origin instead.")]
        private MatchSceneConfiguration _sceneConfiguration;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(new SessionStartPlan(
                ResolveMode(),
                new RoomCreateRequest(
                    _displayName,
                    !string.IsNullOrEmpty(_password),
                    _password,
                    _maxPlayers,
                    MapCatalog.DefaultMapId),
                _roomCode,
                _password,
                CaptureSpawnPoses()));

            builder.RegisterEntryPoint<SessionAutoConnect>();
        }

        /// <summary>
        /// Reads the scene's spawn points, or returns none.
        /// </summary>
        /// <remarks>
        /// The failure is swallowed on purpose. A scene laid out for testing
        /// often has fewer points than a match needs, and refusing to connect
        /// over that would make the session untestable until the map is done.
        /// </remarks>
        private Pose[] CaptureSpawnPoses()
        {
            if (_sceneConfiguration == null)
            {
                return Array.Empty<Pose>();
            }

            try
            {
                return _sceneConfiguration.CaptureSpawnPoses();
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogWarning(
                    $"[Bootstrap] Ignoring the scene's spawn points: {exception.Message}",
                    this);

                return Array.Empty<Pose>();
            }
        }

        /// <summary>
        /// Lets a Multiplayer Play Mode tag override the inspector, so one scene
        /// can drive a host and a client at the same time.
        /// </summary>
        /// <remarks>
        /// Only the mode is taken from the tag. Room name, map, size and password
        /// still come from the inspector, so both instances agree on what the
        /// room is without anything being duplicated per player.
        /// </remarks>
        private SessionStartMode ResolveMode()
        {
            var role = SessionRoles.Current;

            switch (role)
            {
                case SessionRole.Host:
                    Debug.Log($"[Bootstrap] {SessionRoles.Describe()}: opening the room.");
                    return SessionStartMode.CreateRoom;

                case SessionRole.Client:
                    Debug.Log($"[Bootstrap] {SessionRoles.Describe()}: entering the room.");
                    return SessionStartMode.EnterFirstListed;

                default:
                    return _mode;
            }
        }
    }

    /// <summary>What to do on start, taken from the scene component.</summary>
    public readonly struct SessionStartPlan
    {
        public readonly SessionStartMode Mode;
        public readonly RoomCreateRequest CreateRequest;
        public readonly string RoomCode;
        public readonly string Password;

        /// <summary>Where characters appear. Empty if the scene marked none.</summary>
        public readonly IReadOnlyList<Pose> SpawnPoses;

        public SessionStartPlan(
            SessionStartMode mode,
            RoomCreateRequest createRequest,
            string roomCode,
            string password,
            IReadOnlyList<Pose> spawnPoses)
        {
            Mode = mode;
            CreateRequest = createRequest;
            RoomCode = roomCode;
            Password = password;
            SpawnPoses = spawnPoses;
        }
    }

    /// <summary>
    /// Drives matchmaking once the scene's container is ready.
    /// </summary>
    public sealed class SessionAutoConnect : IAsyncStartable
    {
        private const int ListPollIntervalMs = 200;
        private const int ListPollAttempts = 50;

        private readonly RoomUiCommands _commands;
        private readonly RoomBrowserSystem _state;
        private readonly NetworkRunnerService _network;
        private readonly PlayerSpawner _spawner;
        private readonly SessionStartPlan _plan;

        /// <summary>
        /// Handed to the overlay so a tester can rename themselves. The profile
        /// screen has no way into it yet, and without a rename the network only
        /// ever carries the first-run default.
        /// </summary>
        private readonly PlayerProfile _profile;

        public SessionAutoConnect(
            RoomUiCommands commands,
            RoomBrowserSystem state,
            NetworkRunnerService network,
            PlayerSpawner spawner,
            SessionStartPlan plan,
            PlayerProfile profile)
        {
            _commands = commands;
            _state = state;
            _network = network;
            _spawner = spawner;
            _plan = plan;
            _profile = profile;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            // A built player has no visible console, so session state has to be
            // on screen for anyone testing with two instances.
            SessionDebugOverlay.Attach(_network, _commands, _state, _profile);

            // Handed over before connecting: the host spawns the moment the
            // session starts, and a character placed before this arrives would
            // sit at the fallback position for the rest of the match.
            _spawner.UseSpawnPoses(_plan.SpawnPoses);

            switch (_plan.Mode)
            {
                case SessionStartMode.BrowseLobby:
                    await _commands.RefreshAsync(cancellation);
                    break;

                case SessionStartMode.CreateRoom:
                    Report(await _commands.CreateAsync(_plan.CreateRequest, cancellation));
                    break;

                case SessionStartMode.EnterByCode:
                    Report(await _commands.EnterByCodeAsync(
                        _plan.RoomCode, _plan.Password, cancellation));
                    break;

                case SessionStartMode.EnterFirstListed:
                    await EnterFirstListed(cancellation);
                    break;
            }
        }

        /// <summary>
        /// Stands in for a player picking a room out of the browser: waits for a
        /// list to arrive, then enters the first room with whatever password the
        /// inspector holds.
        /// </summary>
        private async UniTask EnterFirstListed(CancellationToken cancellation)
        {
            var failure = await _commands.RefreshAsync(cancellation);
            if (failure != RoomEntryFailure.None)
            {
                Debug.LogError($"[Bootstrap] Room refresh failed: {failure}");
                return;
            }

            var rooms = _state.Rooms;
            Debug.Log($"[Bootstrap] Waiting for a room listing. Have {rooms.CurrentValue.Count}.");

            var waited = 0;

            for (var i = 0; i < ListPollAttempts && rooms.CurrentValue.Count == 0; i++)
            {
                await UniTask.Delay(ListPollIntervalMs, cancellationToken: cancellation);
                waited = i + 1;
            }

            Debug.Log(
                $"[Bootstrap] Done waiting after {waited} attempts. " +
                $"Have {rooms.CurrentValue.Count} room(s).");

            if (rooms.CurrentValue.Count == 0)
            {
                Debug.LogError("[Bootstrap] No rooms are listed.");
                return;
            }

            var target = rooms.CurrentValue[0];
            Debug.Log(
                $"[Bootstrap] Entering listed room '{target.DisplayName}' " +
                $"(locked={target.IsLocked}).");

            Report(await _commands.EnterAsync(target.Id, _plan.Password, cancellation));
        }

        private void Report(RoomEntryResult result)
        {
            if (!result.Ok)
            {
                Debug.LogError($"[Bootstrap] {_plan.Mode} failed: {result.Failure}");
                return;
            }

            if (string.IsNullOrEmpty(result.RoomCode))
            {
                Debug.Log($"[Bootstrap] {_plan.Mode} succeeded.");
                return;
            }

            Debug.Log($"[Bootstrap] {_plan.Mode} succeeded. Room code: {result.RoomCode}");
        }
    }
}
