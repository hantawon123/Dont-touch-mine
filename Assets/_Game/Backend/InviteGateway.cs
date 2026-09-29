using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Backend;
using Game.Core.Ports;
using UnityEngine;

namespace Game.Backend
{
    /// <summary>
    /// <see cref="IInviteGateway"/> against the backend's invite endpoints.
    /// </summary>
    public sealed class InviteGateway : IInviteGateway
    {
        private const string Invites = "/api/v1/invites";

        private const string TimeFormat = "yyyyMMddHHmmss";

        private readonly BackendClient client;

        public InviteGateway(BackendClient client)
        {
            this.client = client;
        }

        public UniTask<BackendResult> SendAsync(
            string playerId, string roomCode, CancellationToken cancellation)
        {
            var body = new SendInviteRequestDto { userId = playerId, roomCode = roomCode };

            return client.CallAsync(
                HttpMethod.Post, Invites, body, BackendAuth.UserId, cancellation);
        }

        public async UniTask<BackendResult<IReadOnlyList<RoomInvitation>>> ListAsync(
            CancellationToken cancellation)
        {
            var answer = await client.CallAsync<InviteListResponseDto>(
                HttpMethod.Get, Invites, null, BackendAuth.UserId, cancellation);

            if (!answer.Ok)
            {
                return BackendResult<IReadOnlyList<RoomInvitation>>.Failed(answer.Failure);
            }

            var invitations = new List<RoomInvitation>();
            var rows = answer.Value.invites ?? Array.Empty<InviteSummaryDto>();
            for (var index = 0; index < rows.Length; index++)
            {
                var row = rows[index];
                if (row == null
                    || string.IsNullOrWhiteSpace(row.userId)
                    || string.IsNullOrWhiteSpace(row.nickname)
                    || string.IsNullOrWhiteSpace(row.roomCode))
                {
                    // One unreadable row does not discard the rest. A row with no
                    // room code is worse than useless — it would draw a button
                    // that cannot enter anything.
                    Debug.LogWarning("[Backend] Skipped an invite row that was missing a field.");
                    continue;
                }

                invitations.Add(new RoomInvitation(
                    row.userId, row.nickname, row.roomCode, InvitedAt(row.invitedAt)));
            }

            return BackendResult<IReadOnlyList<RoomInvitation>>.Success(invitations);
        }

        public UniTask<BackendResult> DeclineAsync(
            string playerId, CancellationToken cancellation)
        {
            return client.CallAsync(
                HttpMethod.Delete,
                Invites + "/" + Uri.EscapeDataString(playerId ?? string.Empty),
                null,
                BackendAuth.UserId,
                cancellation);
        }

        /// <remarks>
        /// Parsed as UTC explicitly, for the same reason every other server time
        /// is: read in this machine's zone it lands hours away and still looks
        /// like a plausible time.
        /// </remarks>
        private static DateTime InvitedAt(string value)
        {
            if (DateTime.TryParseExact(
                    value,
                    TimeFormat,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsed))
            {
                return parsed;
            }

            Debug.LogWarning($"[Backend] Unreadable invite time: {value}");
            return DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
        }
    }
}
