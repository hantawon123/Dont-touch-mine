using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Maps;
using Game.Network.Session;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Starts the normal authenticated network flow for the isolated bot test scene.
    /// </summary>
    public class BotNetworkTestLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<BotNetworkTestConnector>();
        }
    }

    /// <summary>
    /// Opens or joins one fixed room without asking the production dedicated-server
    /// pool for a room. This exists only for the isolated two-editor bot smoke test.
    /// </summary>
    public sealed class BotNetworkTestConnector : IAsyncStartable
    {
        private const string RoomCode = "BOTKCC";
        private const int ClientHeadStartDelayMs = 5_000;

        private readonly NetworkRunnerService network;

        public BotNetworkTestConnector(NetworkRunnerService network)
        {
            this.network = network;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            var role = SessionRoles.Current;
            var request = role == SessionRole.Client
                ? SessionRequest.Join(RoomCode, null)
                : SessionRequest.Create(
                    RoomCode,
                    "Bot network test",
                    MapCatalog.DefaultMapId,
                    6,
                    null,
                    isPrivate: true);

            Debug.Log(
                $"[Bot Network Test] {SessionRoles.Describe()}: starting {request.Mode}.");

            // Give the main editor enough time to publish the fixed room before
            // the virtual client tries to enter it.
            if (role == SessionRole.Client)
            {
                await UniTask.Delay(
                    ClientHeadStartDelayMs,
                    cancellationToken: cancellation);
            }

            var result = await network.StartAsync(request, cancellation);
            if (!result.Ok)
            {
                Debug.LogError(
                    $"[Bot Network Test] {request.Mode} failed: " +
                    $"{result.Failure} ({result.Detail}).");
                return;
            }

            Debug.Log(
                $"[Bot Network Test] {request.Mode} connected. " +
                $"Authority={network.IsServer}.");
        }
    }
}
