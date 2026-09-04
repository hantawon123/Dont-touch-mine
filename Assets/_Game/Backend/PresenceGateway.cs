using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Backend;
using Game.Core.Ports;

namespace Game.Backend
{
    /// <summary>
    /// <see cref="IPresenceGateway"/> against the backend's presence endpoints.
    /// </summary>
    public sealed class PresenceGateway : IPresenceGateway
    {
        private const string Presence = "/api/v1/presence";

        private readonly BackendClient client;

        public PresenceGateway(BackendClient client)
        {
            this.client = client;
        }

        public UniTask<BackendResult> ReportAsync(
            string sessionId, CancellationToken cancellation)
        {
            // Two bodies, not one with a null field. The server reads a missing
            // sessionId as online and any present value as in-game, and
            // JsonUtility writes a null string as "" — which would report this
            // player as being in a game whose room has no name.
            object body = string.IsNullOrWhiteSpace(sessionId)
                ? new EmptyBodyDto()
                : new UpdatePresenceRequestDto { sessionId = sessionId };

            return client.CallAsync(
                HttpMethod.Put, Presence, body, BackendAuth.UserId, cancellation);
        }

        public UniTask<BackendResult> GoOfflineAsync(CancellationToken cancellation)
        {
            return client.CallAsync(
                HttpMethod.Delete, Presence, null, BackendAuth.UserId, cancellation);
        }
    }
}
