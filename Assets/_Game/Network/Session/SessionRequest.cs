using Fusion;

namespace Game.Network.Session
{
    /// <summary>
    /// Keys used for Photon session properties. These are visible to every
    /// client that browses the lobby, so nothing secret belongs here.
    /// </summary>
    public static class SessionPropertyKeys
    {
        /// <summary>Display name shown in the room list. May be duplicated.</summary>
        public const string DisplayName = "name";

        /// <summary>Identifier of the selected map.</summary>
        public const string MapId = "map";

        /// <summary>Host-configured admission limit (2-6).</summary>
        public const string MaxPlayers = "max";

        /// <summary>How many player-item destruction attempts each player gets.</summary>
        public const string DestructionLimit = "destroy";

        public const string HidingDurationSeconds = "hide";
        public const string SearchingDurationMinutes = "search";
        public const string SprintMultiplierPercent = "sprint";
        public const string StunHitCount = "stun";
        public const string CategoryId = "category";

        /// <summary>
        /// Name of whoever opened the room, for the listing to credit. Display
        /// only: it is not unique and nothing looks anyone up by it.
        /// </summary>
        public const string HostNickname = "host";

        /// <summary>
        /// When the room was opened, in seconds since the Unix epoch.
        /// </summary>
        /// <remarks>
        /// Written once by the host, because the room list is ordered newest
        /// first and a session listing carries no age of its own. Seconds rather
        /// than ticks: a session property holds an int, and seconds fit one for
        /// another decade.
        /// </remarks>
        public const string OpenedAt = "opened";

        /// <summary>
        /// Whether the room is playing rather than waiting.
        /// </summary>
        /// <remarks>
        /// The room list needs this and has nothing else to read it from: a
        /// session in a match looks exactly like one in its lobby from outside,
        /// and hiding the session instead would make a running match vanish from
        /// the list rather than show as unavailable.
        /// </remarks>
        public const string Playing = "playing";

        /// <summary>Whether the room requires a password.</summary>
        public const string Locked = "locked";
    }

    /// <summary>
    /// Everything needed to open or enter one session, in one value so the
    /// service signature stays stable as the lobby grows.
    /// </summary>
    /// <remarks>
    /// The room code doubles as the Photon session name: it is the only key
    /// Photon can look a session up by, so entering a code needs no lookup step.
    /// </remarks>
    public readonly struct SessionRequest
    {
        public readonly GameMode Mode;

        /// <summary>Room code, used verbatim as the Photon session name.</summary>
        public readonly string RoomCode;

        public readonly string DisplayName;

        public readonly string MapId;

        public readonly int MaxPlayers;

        /// <summary>
        /// The room's password. Opening a room, it is what the room will require
        /// of joiners; entering one, it is what this peer presents. Empty leaves
        /// a new room open to anyone.
        /// </summary>
        public readonly string Password;

        /// <summary>
        /// Whether Photon may create the session when it does not exist. Must be
        /// false when entering a code, otherwise a typo silently opens an empty
        /// room instead of reporting that the code is wrong.
        /// </summary>
        public readonly bool AllowCreate;

        private SessionRequest(
            GameMode mode,
            string roomCode,
            string displayName,
            string mapId,
            int maxPlayers,
            string password,
            bool allowCreate)
        {
            Mode = mode;
            RoomCode = roomCode;
            DisplayName = displayName;
            MapId = mapId;
            MaxPlayers = maxPlayers;
            Password = password;
            AllowCreate = allowCreate;
        }

        /// <summary>Opens a new room as the authority.</summary>
        public static SessionRequest Create(
            string roomCode,
            string displayName,
            string mapId,
            int maxPlayers,
            string password)
        {
            return new SessionRequest(
                GameMode.Host, roomCode, displayName, mapId, maxPlayers, password, true);
        }

        /// <summary>Enters an existing room, failing if the code does not exist.</summary>
        public static SessionRequest Join(string roomCode, string password)
        {
            return new SessionRequest(
                GameMode.Client, roomCode, null, null, 0, password, false);
        }
    }
}
