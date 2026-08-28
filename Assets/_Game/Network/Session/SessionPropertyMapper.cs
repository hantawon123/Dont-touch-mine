using System.Collections.Generic;
using Fusion;
using Game.Core.Lobby;

namespace Game.Network.Session
{
    /// <summary>
    /// Maps engine-neutral room settings to Photon session properties and back.
    /// Session properties are visible in the lobby, so secrets never belong here.
    /// </summary>
    internal static class SessionPropertyMapper
    {
        public static Dictionary<string, SessionProperty> BuildForStart(
            in SessionRequest request,
            string sanitisedHostNickname)
        {
            if (request.Mode == GameMode.Client)
            {
                return null;
            }

            var properties = new Dictionary<string, SessionProperty>();

            if (!string.IsNullOrEmpty(request.DisplayName))
            {
                properties[SessionPropertyKeys.DisplayName] = request.DisplayName;
            }

            if (!string.IsNullOrEmpty(request.MapId))
            {
                properties[SessionPropertyKeys.MapId] = request.MapId;
            }

            if (request.MaxPlayers > 0)
            {
                properties[SessionPropertyKeys.MaxPlayers] = request.MaxPlayers;
            }

            properties[SessionPropertyKeys.DestructionLimit] =
                PlaySettingsDraft.DefaultDestructionLimit;

            if (!string.IsNullOrEmpty(sanitisedHostNickname))
            {
                properties[SessionPropertyKeys.HostNickname] = sanitisedHostNickname;
            }

            properties[SessionPropertyKeys.Locked] = !string.IsNullOrEmpty(request.Password);
            return properties;
        }

        public static Dictionary<string, SessionProperty> BuildLobbySettings(
            int maxPlayers,
            int destructionLimit,
            string mapId) =>
            new Dictionary<string, SessionProperty>
            {
                [SessionPropertyKeys.MaxPlayers] = maxPlayers,
                [SessionPropertyKeys.DestructionLimit] = destructionLimit,
                [SessionPropertyKeys.MapId] = mapId.Trim(),
            };

        public static int ReadInt(SessionInfo info, string key, int fallback)
        {
            var properties = info.Properties;
            return properties != null &&
                   properties.TryGetValue(key, out var property) &&
                   property.IsInt
                ? (int)property
                : fallback;
        }

        public static string ReadString(SessionInfo info, string key, string fallback)
        {
            var properties = info.Properties;
            return properties != null &&
                   properties.TryGetValue(key, out var property) &&
                   property.IsString
                ? (string)property
                : fallback;
        }
    }
}
