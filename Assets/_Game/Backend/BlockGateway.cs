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
    /// <see cref="IBlockGateway"/> against the backend's block endpoints.
    /// </summary>
    public sealed class BlockGateway : IBlockGateway
    {
        private const string Blocks = "/api/v1/blocks";

        private const string TimeFormat = "yyyyMMddHHmmss";

        private readonly BackendClient client;

        public BlockGateway(BackendClient client)
        {
            this.client = client;
        }

        /// <remarks>
        /// PUT rather than POST because it is idempotent: blocking someone who
        /// is already blocked asks for a state that already holds, which is not
        /// an error.
        /// </remarks>
        public UniTask<BackendResult> BlockAsync(string playerId, CancellationToken cancellation)
        {
            return client.CallAsync(
                HttpMethod.Put,
                Blocks + "/" + Segment(playerId),
                null,
                BackendAuth.UserId,
                cancellation);
        }

        public UniTask<BackendResult> UnblockAsync(string playerId, CancellationToken cancellation)
        {
            return client.CallAsync(
                HttpMethod.Delete,
                Blocks + "/" + Segment(playerId),
                null,
                BackendAuth.UserId,
                cancellation);
        }

        public async UniTask<BackendResult<IReadOnlyList<BlockedPlayer>>> ListBlockedAsync(
            CancellationToken cancellation)
        {
            var answer = await client.CallAsync<BlockListResponseDto>(
                HttpMethod.Get, Blocks, null, BackendAuth.UserId, cancellation);

            if (!answer.Ok)
            {
                return BackendResult<IReadOnlyList<BlockedPlayer>>.Failed(answer.Failure);
            }

            var blocked = new List<BlockedPlayer>();
            var rows = answer.Value.blocked ?? Array.Empty<BlockedUserSummaryDto>();
            for (var index = 0; index < rows.Length; index++)
            {
                var row = rows[index];
                if (row == null
                    || string.IsNullOrWhiteSpace(row.userId)
                    || string.IsNullOrWhiteSpace(row.nickname))
                {
                    // One unreadable row does not discard the rest.
                    Debug.LogWarning("[Backend] Skipped a blocked row with no id or nickname.");
                    continue;
                }

                blocked.Add(new BlockedPlayer(row.userId, row.nickname, BlockedAt(row.blockedAt)));
            }

            return BackendResult<IReadOnlyList<BlockedPlayer>>.Success(blocked);
        }

        /// <inheritdoc cref="FriendGateway"/>
        /// <remarks>
        /// Parsed as UTC explicitly, for the same reason a request time is: read
        /// in this machine's zone it lands hours away and still looks plausible.
        /// </remarks>
        private static DateTime BlockedAt(string value)
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

            Debug.LogWarning($"[Backend] Unreadable block time: {value}");
            return DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
        }

        private static string Segment(string playerId)
        {
            return Uri.EscapeDataString(playerId ?? string.Empty);
        }
    }
}
