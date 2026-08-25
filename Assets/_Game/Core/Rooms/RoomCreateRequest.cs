namespace Game.Core.Rooms
{
    /// <summary>
    /// What the host chose when opening a room.
    /// </summary>
    /// <remarks>
    /// Carries no room code: the code is issued by the layer that opens the
    /// room and comes back in <see cref="RoomEntryResult"/>.
    /// </remarks>
    public readonly struct RoomCreateRequest
    {
        /// <summary>Name shown in the room list. Duplicates are allowed.</summary>
        public readonly string DisplayName;

        public readonly string MapId;

        public readonly int MaxPlayers;

        /// <summary>Empty leaves the room open to anyone.</summary>
        public readonly string Password;

        public RoomCreateRequest(string displayName, string mapId, int maxPlayers, string password)
        {
            DisplayName = displayName;
            MapId = mapId;
            MaxPlayers = maxPlayers;
            Password = password;
        }

        public bool IsLocked => !string.IsNullOrEmpty(Password);
    }
}
