using System;

namespace Game.Core.Match
{
    public readonly struct MatchParticipant
    {
        public MatchParticipant(string playerId, int seat)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new ArgumentException("Player id is required.", nameof(playerId));
            }

            if (seat < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seat));
            }

            PlayerId = playerId.Trim();
            Seat = seat;
        }

        public string PlayerId { get; }

        /// <summary>
        /// Stable zero-based index used by match arrays and network state.
        /// </summary>
        public int PlayerIndex => Seat;

        /// <summary>
        /// Lobby name for <see cref="PlayerIndex"/>. Kept for existing callers.
        /// </summary>
        public int Seat { get; }
    }
}
