namespace Game.Core.Rooms
{
    /// <summary>
    /// Why entering or opening a room did not succeed, in terms a player can be
    /// shown directly.
    /// </summary>
    public enum RoomEntryFailure
    {
        None = 0,

        /// <summary>No room exists for the entered code.</summary>
        NotFound,

        /// <summary>The room already holds its maximum players.</summary>
        Full,

        /// <summary>The room is no longer accepting players.</summary>
        Closed,

        /// <summary>The password was rejected.</summary>
        WrongPassword,

        /// <summary>Could not find an unused code for a new room.</summary>
        CodeUnavailable,

        /// <summary>Matchmaking could not be reached.</summary>
        ConnectionFailed,

        /// <summary>This client is already in a room.</summary>
        AlreadyInRoom,

        Unknown,
    }
}
