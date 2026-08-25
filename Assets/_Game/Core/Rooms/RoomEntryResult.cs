namespace Game.Core.Rooms
{
    /// <summary>
    /// Outcome of opening or entering a room.
    /// </summary>
    public readonly struct RoomEntryResult
    {
        public readonly bool Ok;

        public readonly RoomEntryFailure Failure;

        /// <summary>
        /// The room code, set only when the caller is entitled to know it: after
        /// opening a room, so the host can share it, or after entering with a
        /// code the player already typed. Entering a room picked from the list
        /// leaves this empty, because knowing a code is what admits someone to a
        /// locked room.
        /// </summary>
        public readonly string RoomCode;

        private RoomEntryResult(bool ok, RoomEntryFailure failure, string roomCode)
        {
            Ok = ok;
            Failure = failure;
            RoomCode = roomCode;
        }

        public static RoomEntryResult Entered() =>
            new RoomEntryResult(true, RoomEntryFailure.None, null);

        public static RoomEntryResult Opened(string roomCode) =>
            new RoomEntryResult(true, RoomEntryFailure.None, roomCode);

        public static RoomEntryResult Failed(RoomEntryFailure failure) =>
            new RoomEntryResult(false, failure, null);
    }
}
