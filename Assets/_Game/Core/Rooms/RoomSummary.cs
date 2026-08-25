namespace Game.Core.Rooms
{
    /// <summary>
    /// One row of the room list, in terms presentation can render directly.
    /// </summary>
    /// <remarks>
    /// Carries no networking types and no address: <see cref="Id"/> is opaque
    /// and the room code is absent on purpose, since knowing a code is what
    /// lets someone enter a locked room.
    /// </remarks>
    public readonly struct RoomSummary
    {
        public readonly RoomId Id;

        /// <summary>Host-chosen name. May be duplicated across rooms.</summary>
        public readonly string DisplayName;

        public readonly string MapId;

        public readonly int PlayerCount;

        public readonly int MaxPlayers;

        /// <summary>Whether entering requires a password.</summary>
        public readonly bool IsLocked;

        /// <summary>Whether the room still accepts players.</summary>
        public readonly bool IsOpen;

        public RoomSummary(
            RoomId id,
            string displayName,
            string mapId,
            int playerCount,
            int maxPlayers,
            bool isLocked,
            bool isOpen)
        {
            Id = id;
            DisplayName = displayName;
            MapId = mapId;
            PlayerCount = playerCount;
            MaxPlayers = maxPlayers;
            IsLocked = isLocked;
            IsOpen = isOpen;
        }

        public bool IsFull => MaxPlayers > 0 && PlayerCount >= MaxPlayers;

        /// <summary>True when a player could enter right now.</summary>
        public bool CanEnter => IsOpen && !IsFull;
    }
}
