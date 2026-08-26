namespace Game.Core.Rooms
{
    /// <summary>
    /// One person in the room, as everyone in the room sees them.
    /// </summary>
    /// <remarks>
    /// Every value here is the same on every peer. Whether this is you is not,
    /// so it is absent on purpose: presentation compares <see cref="PlayerId"/>
    /// against the local player's id instead of asking the room.
    /// </remarks>
    public readonly struct RoomParticipant
    {
        /// <summary>
        /// Unique within this room. Not an account, and not a name: this is what
        /// code compares, never what a person reads.
        /// </summary>
        public readonly string PlayerId;

        /// <summary>
        /// What this person chose to be called. For display only, so it is never
        /// compared, deduplicated, or used to find anyone.
        /// </summary>
        /// <remarks>
        /// Empty when the network has not carried a name yet, which happens for
        /// the moment between a character appearing and its owner's name
        /// arriving. Presentation falls back to <see cref="PlayerId"/> then,
        /// rather than showing a blank row.
        /// </remarks>
        public readonly string Nickname;

        /// <summary>Seat number, 0 upwards, in the order people arrived.</summary>
        public readonly int Seat;

        /// <summary>Whether this person holds authority over the room.</summary>
        public readonly bool IsHost;

        public RoomParticipant(string playerId, int seat, bool isHost, string nickname = null)
        {
            PlayerId = playerId;
            Seat = seat;
            IsHost = isHost;

            // Normalised here so every consumer can treat it as "empty or a real
            // name" without repeating the check.
            Nickname = string.IsNullOrWhiteSpace(nickname) ? string.Empty : nickname.Trim();
        }
    }
}
