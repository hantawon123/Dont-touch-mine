using System;

namespace Game.Core.Home
{
    /// <summary>
    /// What sending a friend request settled on.
    /// </summary>
    public enum FriendRequestOutcome
    {
        /// <summary>Waiting for the other player to accept.</summary>
        Sent,

        /// <summary>
        /// Already friends as of this call, because the other player had
        /// requested first.
        /// </summary>
        /// <remarks>
        /// The server settles a mutual request on the spot rather than telling
        /// the sender to go accept an incoming one. Presentation shows a
        /// different message for this, and refreshes the friend list rather than
        /// the pending list.
        /// </remarks>
        BecameFriends
    }

    /// <summary>
    /// A friend request this player has received and not yet answered.
    /// </summary>
    public readonly struct FriendRequestSummary
    {
        public FriendRequestSummary(
            string playerId,
            string nickname,
            DateTime requestedAtUtc)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new ArgumentException("Player id is required.", nameof(playerId));
            }

            if (string.IsNullOrWhiteSpace(nickname))
            {
                throw new ArgumentException("Nickname is required.", nameof(nickname));
            }

            if (requestedAtUtc.Kind == DateTimeKind.Local)
            {
                // Caught here rather than at the display, where a time that is
                // nine hours off still looks like a plausible time.
                throw new ArgumentException(
                    "Requested time must be UTC.", nameof(requestedAtUtc));
            }

            PlayerId = playerId.Trim();
            Nickname = nickname.Trim();
            RequestedAtUtc = requestedAtUtc;
        }

        /// <summary>The sender. Accepting or declining names this value.</summary>
        public string PlayerId { get; }

        public string Nickname { get; }

        /// <summary>
        /// When it was sent, in UTC. Call <see cref="DateTime.ToLocalTime"/>
        /// before showing it.
        /// </summary>
        public DateTime RequestedAtUtc { get; }
    }
}
