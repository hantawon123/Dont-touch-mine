using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Backend;

namespace Game.Core.Ports
{
    /// <summary>One person this player has blocked.</summary>
    public readonly struct BlockedPlayer
    {
        public BlockedPlayer(string playerId, string nickname, DateTime blockedAtUtc)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new ArgumentException("Player id is required.", nameof(playerId));
            }

            if (string.IsNullOrWhiteSpace(nickname))
            {
                throw new ArgumentException("Nickname is required.", nameof(nickname));
            }

            if (blockedAtUtc.Kind == DateTimeKind.Local)
            {
                throw new ArgumentException("Blocked time must be UTC.", nameof(blockedAtUtc));
            }

            PlayerId = playerId.Trim();
            Nickname = nickname.Trim();
            BlockedAtUtc = blockedAtUtc;
        }

        public string PlayerId { get; }

        /// <summary>
        /// Their nickname now, not the one they had when they were blocked.
        /// </summary>
        public string Nickname { get; }

        public DateTime BlockedAtUtc { get; }
    }

    /// <summary>
    /// Blocking, and taking a block back.
    /// </summary>
    /// <remarks>
    /// A block applies both ways: neither person can find or reach the other
    /// afterwards. It is stored one way, though — a block this player set is the
    /// only one this player can lift, and there is no way to learn who has
    /// blocked them.
    /// <para>
    /// Blocking also ends the friendship and drops any request between the two,
    /// so a caller re-reads the friend list afterwards rather than assuming only
    /// the block changed.
    /// </para>
    /// <para>
    /// It applies silently. To the person blocked, the blocker stops existing:
    /// searches do not find them and requests answer
    /// <see cref="BackendFailure.TargetNotFound"/>, which is the same answer a
    /// deleted account gives. That is deliberate, and presentation must not
    /// translate it into a message about having been blocked.
    /// </para>
    /// </remarks>
    public interface IBlockGateway
    {
        /// <summary>
        /// Blocks someone. Blocking again succeeds and changes nothing.
        /// </summary>
        UniTask<BackendResult> BlockAsync(string playerId, CancellationToken cancellation);

        /// <summary>
        /// Lifts a block. Lifting one that was never set succeeds, because the
        /// state asked for is already the state.
        /// </summary>
        UniTask<BackendResult> UnblockAsync(string playerId, CancellationToken cancellation);

        /// <summary>
        /// Everyone this player has blocked. Never the other direction.
        /// </summary>
        UniTask<BackendResult<IReadOnlyList<BlockedPlayer>>> ListBlockedAsync(
            CancellationToken cancellation);
    }
}
