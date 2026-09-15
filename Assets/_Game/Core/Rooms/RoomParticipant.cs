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

        /// <summary>
        /// The backend account this person signed in as. Empty when they did not
        /// sign in, or when the network has not carried it yet.
        /// </summary>
        /// <remarks>
        /// The one value here that outlives the room. <see cref="PlayerId"/> is
        /// handed out per room and reused, so anything that has to follow a
        /// person across matches — the play log above all — keys on this instead.
        /// Never shown: the client guide forbids putting another player's account
        /// id on screen. Identification only, so replicating it reveals nothing
        /// that the REST responses do not already.
        /// </remarks>
        public readonly string UserId;

        /// <summary>
        /// Whether this person silenced their own microphone. Replicated with
        /// the rest of the row so every peer can dim the same portrait.
        /// </summary>
        public readonly bool IsMuted;

        /// <summary>
        /// Whether this person's microphone is sending voice right now.
        /// Replicated so every peer can light the same nameplate, including
        /// when this machine has its own speaker off.
        /// </summary>
        public readonly bool IsTalking;

        public RoomParticipant(
            string playerId,
            int seat,
            bool isHost,
            string nickname = null,
            string userId = null,
            bool isMuted = false,
            bool isTalking = false)
        {
            PlayerId = playerId;
            Seat = seat;
            IsHost = isHost;

            // Normalised here so every consumer can treat it as "empty or a real
            // name" without repeating the check.
            Nickname = string.IsNullOrWhiteSpace(nickname) ? string.Empty : nickname.Trim();
            UserId = string.IsNullOrWhiteSpace(userId) ? string.Empty : userId.Trim();
            IsMuted = isMuted;
            IsTalking = isTalking;
        }
    }
}
