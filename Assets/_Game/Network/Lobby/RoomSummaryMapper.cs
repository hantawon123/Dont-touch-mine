using Fusion;
using Game.Core.Lobby;
using Game.Core.Rooms;
using Game.Network.Session;

namespace Game.Network.Lobby
{
    /// <summary>
    /// Turns Photon session listings into the engine-neutral summaries that
    /// presentation consumes.
    /// </summary>
    internal static class RoomSummaryMapper
    {
        private const string UnnamedRoom = "Unnamed room";

        public static bool TryToSummary(SessionInfo info, out RoomSummary summary)
        {
            var displayName = ReadString(info, SessionPropertyKeys.DisplayName, UnnamedRoom);
            var mapId = ReadString(info, SessionPropertyKeys.MapId, null);
            var hostNickname = ReadString(info, SessionPropertyKeys.HostNickname, null);
            var maxPlayers = ReadInt(
                info,
                SessionPropertyKeys.MaxPlayers,
                info.MaxPlayers);

            if (string.IsNullOrWhiteSpace(info.Name)
                || string.IsNullOrWhiteSpace(mapId)
                || !info.IsVisible
                || !info.IsOpen
                || maxPlayers < RoomSettings.MinPlayerCount
                || maxPlayers > RoomSettings.MaxPlayerCount
                || info.PlayerCount < 1
                || info.PlayerCount > maxPlayers)
            {
                summary = default;
                return false;
            }

            summary = new RoomSummary(
                new RoomId(info.Name),
                displayName,
                mapId,
                info.PlayerCount,
                maxPlayers,
                ReadBool(info, SessionPropertyKeys.Locked),
                info.IsOpen,
                RoomStatus.Waiting,
                hostNickname);
            return true;
        }

        /// <summary>
        /// Falls back to a placeholder rather than the session name: the session
        /// name is the room code, and showing it would hand out the one thing
        /// that lets someone into a locked room.
        /// </summary>
        private static string ReadString(SessionInfo info, string key, string fallback)
        {
            var properties = info.Properties;

            if (properties != null
                && properties.TryGetValue(key, out var property)
                && property.IsString)
            {
                var value = (string)property;
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }

            return fallback;
        }

        private static bool ReadBool(SessionInfo info, string key)
        {
            var properties = info.Properties;

            return properties != null
                   && properties.TryGetValue(key, out var property)
                   && property.Isbool
                   && (bool)property;
        }

        private static int ReadInt(SessionInfo info, string key, int fallback)
        {
            var properties = info.Properties;

            return properties != null
                   && properties.TryGetValue(key, out var property)
                   && property.IsInt
                ? (int)property
                : fallback;
        }
    }
}
