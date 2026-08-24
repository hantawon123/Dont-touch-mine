using System.Threading;
using Cysharp.Threading.Tasks;
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
        /// <summary>Browse the room list without entering a room.</summary>
        BrowseLobby = 0,

        /// <summary>Open or enter a fixed room, whichever is needed.</summary>
        AutoConnect = 1,
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
    /// Temporary scaffolding: it exists to exercise matchmaking before any UI
    /// exists. The lobby screen replaces it with explicit player choices.
    /// </para>
    /// </remarks>
    public sealed class GameSessionLifetimeScope : LifetimeScope
    {
        [SerializeField]
        [Tooltip("Browse the room list, or open/enter the room below.")]
        private SessionStartMode _mode = SessionStartMode.AutoConnect;

        [SerializeField]
        [Tooltip("Room code used when the mode is AutoConnect.")]
        private string _roomCode = "TESTROOM";

        [SerializeField]
        [Tooltip("Room display name shown in the room list.")]
        private string _displayName = "Test room";

        [SerializeField]
        [Tooltip("Maximum players allowed in the room.")]
        private int _maxPlayers = 6;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(new SessionStartPlan(
                _mode,
                SessionRequest.AutoConnect(_roomCode, _displayName, _maxPlayers)));

            builder.RegisterEntryPoint<SessionAutoConnect>();
        }
    }

    /// <summary>What to do on start, resolved from the scene component.</summary>
    public readonly struct SessionStartPlan
    {
        public readonly SessionStartMode Mode;
        public readonly SessionRequest Request;

        public SessionStartPlan(SessionStartMode mode, SessionRequest request)
        {
            Mode = mode;
            Request = request;
        }
    }

    /// <summary>
    /// Reaches matchmaking once the scene's container is ready.
    /// </summary>
    public sealed class SessionAutoConnect : IAsyncStartable
    {
        private readonly NetworkRunnerService _network;
        private readonly SessionStartPlan _plan;

        public SessionAutoConnect(NetworkRunnerService network, SessionStartPlan plan)
        {
            _network = network;
            _plan = plan;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            var result = _plan.Mode == SessionStartMode.BrowseLobby
                ? await _network.JoinLobbyAsync(cancellation)
                : await _network.StartAsync(_plan.Request, cancellation);

            if (!result.Ok)
            {
                Debug.LogError(
                    $"[Bootstrap] {_plan.Mode} failed: {result.Failure} {result.Detail}");
            }
        }
    }
}
