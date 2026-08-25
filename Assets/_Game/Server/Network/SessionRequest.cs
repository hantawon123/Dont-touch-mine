using Fusion;

namespace Game.Server.Network
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
        /// Sent to the host as a connection token and verified there. Empty
        /// means the room is open.
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
