using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Backend;
using Game.Core.Ports;
using UnityEngine;

namespace Game.Backend
{
    /// <summary>
    /// <see cref="IPresenceGateway"/> against the backend, over whichever wire
    /// is up.
    /// </summary>
    /// <remarks>
    /// Prefers the notification socket. A room change is one frame on a
    /// connection that already exists, against a request that opens one, and the
    /// server binds the frame to this player from the HELLO it already has. When
    /// the socket is down — between reconnects, or in a build without the
    /// package — the same report goes over REST, which the server keeps for
    /// exactly that. Either way the caller sees one result.
    /// <para>
    /// The socket path cannot fail visibly: the server acknowledges no frame and
    /// a bad one is dropped in silence. So the frame is built to be right by
    /// construction — the same two shapes the REST body uses — and "handed to a
    /// connected socket" is reported as success, which is all that can be known.
    /// </para>
    /// </remarks>
    public sealed class PresenceGateway : IPresenceGateway
    {
        private const string Presence = "/api/v1/presence";
        private const string PresenceFrameType = "PRESENCE";

        private readonly BackendClient client;
        private readonly INotificationFrameSender frames;

        public PresenceGateway(BackendClient client, INotificationFrameSender frames)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.frames = frames ?? throw new ArgumentNullException(nameof(frames));
        }

        public UniTask<BackendResult> ReportAsync(
            string sessionId, RoomSessionKind kind, CancellationToken cancellation)
        {
            var inRoom = !string.IsNullOrWhiteSpace(sessionId);
            var kindName = KindName(kind);

            // Two shapes, not one with a null field, for both wires. The server
            // reads a missing sessionId as out of a room and a present one as in
            // a room, and JsonUtility writes a null string as "" — which would put
            // this player in a room with no name.
            object frame = inRoom
                ? new PresenceFrameDto { type = PresenceFrameType, sessionId = sessionId, sessionKind = kindName }
                : new PresenceOutOfRoomFrameDto { type = PresenceFrameType };

            if (frames.TrySend(JsonUtility.ToJson(frame)))
            {
                return UniTask.FromResult(BackendResult.Success());
            }

            object body = inRoom
                ? new UpdatePresenceRequestDto { sessionId = sessionId, sessionKind = kindName }
                : new EmptyBodyDto();

            return client.CallAsync(
                HttpMethod.Put, Presence, body, BackendAuth.UserId, cancellation);
        }

        public UniTask<BackendResult> GoOfflineAsync(CancellationToken cancellation)
        {
            return client.CallAsync(
                HttpMethod.Delete, Presence, null, BackendAuth.UserId, cancellation);
        }

        /// <remarks>
        /// The server's names, spelled here rather than taken from the enum's
        /// ToString so that renaming a member cannot silently change the wire.
        /// </remarks>
        private static string KindName(RoomSessionKind kind)
        {
            switch (kind)
            {
                case RoomSessionKind.Lobby: return "LOBBY";
                case RoomSessionKind.Match: return "MATCH";
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
    }
}
