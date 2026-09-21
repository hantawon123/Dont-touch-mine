using System;
using System.Collections.Generic;

namespace Game.Core.Players
{
    /// <summary>
    /// Latest worn looks, keyed by room player id and account id, plus the
    /// local player's selection. UI portraits read this rather than reaching
    /// into spawned avatars.
    /// </summary>
    public static class AvatarAppearanceBoard
    {
        private static readonly Dictionary<string, AvatarAppearance> byId =
            new(StringComparer.Ordinal);

        private static AvatarAppearance local;
        private static bool hasLocal;

        public static bool HasLocal => hasLocal;

        public static AvatarAppearance Local => local;

        public static void SetLocal(AvatarAppearance appearance)
        {
            local = appearance;
            hasLocal = true;
        }

        public static void Replace(IReadOnlyList<(string playerId, string userId, AvatarAppearance appearance)> entries)
        {
            byId.Clear();
            if (entries == null)
            {
                return;
            }

            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                Put(entry.playerId, entry.appearance);
                Put(entry.userId, entry.appearance);
            }
        }

        public static bool TryGet(string id, out AvatarAppearance appearance)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                appearance = default;
                return false;
            }

            return byId.TryGetValue(id.Trim(), out appearance);
        }

        public static void Clear()
        {
            byId.Clear();
            local = default;
            hasLocal = false;
        }

        private static void Put(string id, AvatarAppearance appearance)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            byId[id.Trim()] = appearance;
        }
    }
}
