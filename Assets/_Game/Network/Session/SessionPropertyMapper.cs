using System;
using System.Collections.Generic;
using Fusion;
using Game.Core.Lobby;
using UnityEngine;

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
            AddMatchRules(properties, MatchRuleSettings.Default);
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

        public static MatchRuleSettings ReadMatchRules(
            SessionInfo info,
            MatchRuleSettings fallback)
        {
            if (info.Properties != null && info.Properties.ContainsKey(SessionPropertyKeys.MatchRules))
                return ReadPackedMatchRules(ReadString(info, SessionPropertyKeys.MatchRules, null), fallback);

            // Read older rooms, but never add the five legacy keys to a new room.
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
            properties[SessionPropertyKeys.MatchRules] = JsonUtility.ToJson(new RulesPayload
            {
                version = 1,
                hiding = settings.HidingDurationSeconds,
                searching = settings.SearchingDurationMinutes,
                sprint = settings.SprintMultiplier,
                stun = settings.StunHitCount,
                category = settings.CategoryId
            });
        }

        internal static MatchRuleSettings ReadPackedMatchRules(string json, MatchRuleSettings fallback)
        {
            if (string.IsNullOrEmpty(json)) return fallback;
            try
            {
                var payload = JsonUtility.FromJson<RulesPayload>(json);
                return payload.version == 1 && MatchRuleSettings.TryCreate(
                    payload.hiding, payload.searching, payload.sprint, payload.stun,
                    payload.category, out var rules, out _) ? rules : fallback;
            }
            catch (ArgumentException)
            {
                return fallback;
            }
        }

        [Serializable]
        private struct RulesPayload
        {
            public int version;
            public int hiding;
            public int searching;
            public float sprint;
            public int stun;
            public string category;
        }
    }
}
