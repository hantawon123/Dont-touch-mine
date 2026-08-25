using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Ports;
using Game.Core.Rooms;
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

        /// <summary>Enter an existing room using the code below.</summary>
        EnterByCode = 2,
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

        [Header("Enter by code")]
        [SerializeField]
        [Tooltip("Code issued by the instance that opened the room.")]
        private string _roomCode = string.Empty;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(new SessionStartPlan(
                _mode,
                new RoomCreateRequest(_displayName, null, _maxPlayers, null),
                _roomCode));

            builder.RegisterEntryPoint<SessionAutoConnect>();
        }
    }

    /// <summary>What to do on start, taken from the scene component.</summary>
    public readonly struct SessionStartPlan
    {
        public readonly SessionStartMode Mode;
        public readonly RoomCreateRequest CreateRequest;
        public readonly string RoomCode;

        public SessionStartPlan(
            SessionStartMode mode, RoomCreateRequest createRequest, string roomCode)
        {
            Mode = mode;
            CreateRequest = createRequest;
            RoomCode = roomCode;
        }
    }

    /// <summary>
    /// Drives matchmaking once the scene's container is ready.
    /// </summary>
    public sealed class SessionAutoConnect : IAsyncStartable
    {
        private readonly IRoomBrowser _browser;
        private readonly SessionStartPlan _plan;

        public SessionAutoConnect(IRoomBrowser browser, SessionStartPlan plan)
        {
            _browser = browser;
            _plan = plan;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            switch (_plan.Mode)
            {
                case SessionStartMode.BrowseLobby:
                    await _browser.RefreshAsync(cancellation);
                    break;

                case SessionStartMode.CreateRoom:
                    Report(await _browser.CreateAsync(_plan.CreateRequest, cancellation));
                    break;

                case SessionStartMode.EnterByCode:
                    Report(await _browser.EnterByCodeAsync(_plan.RoomCode, cancellation));
                    break;
            }
        }

        private void Report(RoomEntryResult result)
        {
            if (!result.Ok)
            {
                Debug.LogError($"[Bootstrap] {_plan.Mode} failed: {result.Failure}");
                return;
            }

            if (!string.IsNullOrEmpty(result.RoomCode))
            {
                Debug.Log($"[Bootstrap] {_plan.Mode} succeeded. Room code: {result.RoomCode}");
            }
        }
    }
}
