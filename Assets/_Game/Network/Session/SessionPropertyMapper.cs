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
            string mapId,
            MatchRuleSettings matchRules)
        {
            var properties = new Dictionary<string, SessionProperty>
            {
                [SessionPropertyKeys.MaxPlayers] = maxPlayers,
                [SessionPropertyKeys.DestructionLimit] = destructionLimit,
                [SessionPropertyKeys.MapId] = mapId.Trim(),
            };
            AddMatchRules(properties, matchRules);
            return properties;
        }

        /// <summary>
        /// The one property a match start changes. Sent on its own so a status
        /// update cannot rewrite the room's settings by accident.
        /// </summary>
        public static Dictionary<string, SessionProperty> BuildRoomStatus(bool playing)
        {
            return new Dictionary<string, SessionProperty>
            {
                [SessionPropertyKeys.Playing] = playing,
            };
        }

        public static MatchRuleSettings ReadMatchRules(
            SessionInfo info,
            MatchRuleSettings fallback)
        {
            return MatchRuleSettings.TryCreate(
                ReadInt(
                    info,
                    SessionPropertyKeys.HidingDurationSeconds,
                    fallback.HidingDurationSeconds),
                ReadInt(
                    info,
                    SessionPropertyKeys.SearchingDurationMinutes,
                    fallback.SearchingDurationMinutes),
                ReadInt(
                    info,
                    SessionPropertyKeys.SprintMultiplierPercent,
                    (int)(fallback.SprintMultiplier * 100f)) / 100f,
                ReadInt(info, SessionPropertyKeys.StunHitCount, fallback.StunHitCount),
                ReadString(info, SessionPropertyKeys.CategoryId, fallback.CategoryId),
                out var settings,
                out _)
                ? settings
                : fallback;
        }

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

        private static void AddMatchRules(
            IDictionary<string, SessionProperty> properties,
            MatchRuleSettings settings)
        {
            properties[SessionPropertyKeys.HidingDurationSeconds] =
                settings.HidingDurationSeconds;
            properties[SessionPropertyKeys.SearchingDurationMinutes] =
                settings.SearchingDurationMinutes;
            properties[SessionPropertyKeys.SprintMultiplierPercent] =
                (int)(settings.SprintMultiplier * 100f);
            properties[SessionPropertyKeys.StunHitCount] = settings.StunHitCount;
            properties[SessionPropertyKeys.CategoryId] = settings.CategoryId ?? string.Empty;
        }
    }
}
