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
        public int Seat { get; }
    }
}
