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

        /// <summary>
        /// Last saved looks, kept after the live roster is replaced. Friend
        /// portraits on Home read these when the player is no longer spawned.
        /// </summary>
        private static readonly Dictionary<string, AvatarAppearance> remembered =
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

        /// <summary>
        /// Keeps a look after the wearer leaves the live roster. The next
        /// <see cref="Replace"/> does not forget it, so an offline friend can
        /// still show the face they last saved.
        /// </summary>
        public static void Remember(string id, AvatarAppearance appearance)
        {
            Put(remembered, id, appearance);
        }

        public static bool TryGet(string id, out AvatarAppearance appearance)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                appearance = default;
                return false;
            }

            var key = id.Trim();
            return byId.TryGetValue(key, out appearance)
                || remembered.TryGetValue(key, out appearance);
        }

        public static void Clear()
        {
            byId.Clear();
            remembered.Clear();
            local = default;
            hasLocal = false;
        }

        private static void Put(string id, AvatarAppearance appearance)
        {
            Put(byId, id, appearance);
        }

        private static void Put(
            Dictionary<string, AvatarAppearance> store, string id, AvatarAppearance appearance)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            store[id.Trim()] = appearance;
        }
    }
}
