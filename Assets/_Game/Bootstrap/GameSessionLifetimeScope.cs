using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Ports;
using Game.Core.Rooms;
using Game.Server.Network;
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

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(new SessionStartPlan(
                _mode,
                new RoomCreateRequest(_displayName, null, _maxPlayers, _password),
                _roomCode,
                _password));

            builder.RegisterEntryPoint<SessionAutoConnect>();
        }
    }

    /// <summary>What to do on start, taken from the scene component.</summary>
    public readonly struct SessionStartPlan
    {
        public readonly SessionStartMode Mode;
        public readonly RoomCreateRequest CreateRequest;
        public readonly string RoomCode;
        public readonly string Password;

        public SessionStartPlan(
            SessionStartMode mode,
            RoomCreateRequest createRequest,
            string roomCode,
            string password)
        {
            Mode = mode;
            CreateRequest = createRequest;
            RoomCode = roomCode;
            Password = password;
        }
    }

    /// <summary>
    /// Drives matchmaking once the scene's container is ready.
    /// </summary>
    public sealed class SessionAutoConnect : IAsyncStartable
    {
        private const int ListPollIntervalMs = 200;
        private const int ListPollAttempts = 50;

        private readonly IRoomBrowser _browser;
        private readonly DebugRoomListSink _rooms;
        private readonly DebugRoomSessionSink _session;
        private readonly NetworkRunnerService _network;
        private readonly SessionStartPlan _plan;

        public SessionAutoConnect(
            IRoomBrowser browser,
            DebugRoomListSink rooms,
            DebugRoomSessionSink session,
            NetworkRunnerService network,
            SessionStartPlan plan)
        {
            _browser = browser;
            _rooms = rooms;
            _session = session;
            _network = network;
            _plan = plan;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            // A built player has no visible console, so session state has to be
            // on screen for anyone testing with two instances.
            SessionDebugOverlay.Attach(_network, _browser, _session);

            switch (_plan.Mode)
            {
                case SessionStartMode.BrowseLobby:
                    await _browser.RefreshAsync(cancellation);
                    break;

                case SessionStartMode.CreateRoom:
                    Report(await _browser.CreateAsync(_plan.CreateRequest, cancellation));
                    break;

                case SessionStartMode.EnterByCode:
                    Report(await _browser.EnterByCodeAsync(
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
            await _browser.RefreshAsync(cancellation);

            for (var i = 0; i < ListPollAttempts && _rooms.Rooms.Count == 0; i++)
            {
                await UniTask.Delay(ListPollIntervalMs, cancellationToken: cancellation);
            }

            if (_rooms.Rooms.Count == 0)
            {
                Debug.LogError("[Bootstrap] No rooms are listed.");
                return;
            }

            var target = _rooms.Rooms[0];
            Debug.Log(
                $"[Bootstrap] Entering listed room '{target.DisplayName}' " +
                $"(locked={target.IsLocked}).");

            Report(await _browser.EnterAsync(target.Id, _plan.Password, cancellation));
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
