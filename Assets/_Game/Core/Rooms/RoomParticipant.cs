namespace Game.Core.Rooms
{
    /// <summary>
    /// One person in the room, as everyone in the room sees them.
    /// </summary>
    /// <remarks>
    /// Every value here is the same on every peer. Whether this is you is not,
    /// so it is absent on purpose: presentation compares <see cref="PlayerId"/>
    /// against the local player's id instead of asking the room.
    /// <para>
    /// No nickname yet. Leaving an empty one here would make an unfilled name
    /// look like a filled one, so until nicknames travel the network,
    /// presentation shows <see cref="PlayerId"/> in their place.
    /// </para>
    /// </remarks>
    public readonly struct RoomParticipant
    {
        /// <summary>Unique within this room. Not an account.</summary>
        public readonly string PlayerId;

        /// <summary>Seat number, 0 upwards, in the order people arrived.</summary>
        public readonly int Seat;

        /// <summary>Whether this person holds authority over the room.</summary>
        public readonly bool IsHost;

        public RoomParticipant(string playerId, int seat, bool isHost)
        {
            PlayerId = playerId;
            Seat = seat;
            IsHost = isHost;
        }
    }
}
