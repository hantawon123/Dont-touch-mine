using System;
using System.Collections.Generic;
using Game.Core.Lobby;
using Game.Core.Match;

namespace Game.Client.Match
{
    public readonly struct DestroyedItemHudSlot
    {
        public DestroyedItemHudSlot(
            string itemId,
            bool showPreview,
            bool isOwn,
            bool grayscale)
        {
            ItemId = itemId;
            ShowPreview = showPreview;
            IsOwn = isOwn;
            Grayscale = grayscale;
        }

        public string ItemId { get; }
        public bool ShowPreview { get; }
        public bool IsOwn { get; }
        public bool Grayscale { get; }
    }

    /// <summary>
    /// One circle per player. The local assignment stays leftmost. Other
    /// circles start empty and fill in destruction order.
    /// </summary>
    public static class DestroyedItemsHudLayout
    {
        public static DestroyedItemHudSlot[] Build(
            int playerCount,
            IReadOnlyList<PlayerItemStatusSnapshot> statuses,
            string localItemId,
            IReadOnlyList<string> destroyedItemIdsInOrder)
        {
            var count = playerCount < 0
                ? 0
                : Math.Min(playerCount, RoomSettings.MaxPlayerCount);
            if (count == 0)
            {
                return Array.Empty<DestroyedItemHudSlot>();
            }

            statuses ??= Array.Empty<PlayerItemStatusSnapshot>();
            var destroyedById = new Dictionary<string, bool>(
                statuses.Count,
                StringComparer.Ordinal);
            for (var index = 0; index < statuses.Count; index++)
            {
                destroyedById[statuses[index].ItemId] = statuses[index].IsDestroyed;
            }

            var order = CollectDestroyedOrder(destroyedItemIdsInOrder, statuses, destroyedById);
            var slots = new List<DestroyedItemHudSlot>(count);
            var local = string.IsNullOrWhiteSpace(localItemId) ? null : localItemId.Trim();
            if (local != null)
            {
                var ownDestroyed = destroyedById.TryGetValue(local, out var destroyed) &&
                                   destroyed;
                slots.Add(new DestroyedItemHudSlot(
                    local,
                    showPreview: true,
                    isOwn: true,
                    grayscale: ownDestroyed));
            }

            for (var index = 0; index < order.Count && slots.Count < count; index++)
            {
                if (local != null &&
                    string.Equals(order[index], local, StringComparison.Ordinal))
                {
                    continue;
                }

                slots.Add(new DestroyedItemHudSlot(
                    order[index],
                    showPreview: true,
                    isOwn: false,
                    grayscale: false));
            }

            while (slots.Count < count)
            {
                slots.Add(new DestroyedItemHudSlot(
                    null,
                    showPreview: false,
                    isOwn: false,
                    grayscale: false));
            }

            return slots.ToArray();
        }

        private static List<string> CollectDestroyedOrder(
            IReadOnlyList<string> destroyedItemIdsInOrder,
            IReadOnlyList<PlayerItemStatusSnapshot> statuses,
            Dictionary<string, bool> destroyedById)
        {
            var order = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (destroyedItemIdsInOrder != null)
            {
                for (var index = 0; index < destroyedItemIdsInOrder.Count; index++)
                {
                    var itemId = destroyedItemIdsInOrder[index];
                    if (string.IsNullOrWhiteSpace(itemId))
                    {
                        continue;
                    }

                    itemId = itemId.Trim();
                    if (!seen.Add(itemId))
                    {
                        continue;
                    }

                    if (destroyedById.TryGetValue(itemId, out var destroyed) && !destroyed)
                    {
                        continue;
                    }

                    destroyedById[itemId] = true;
                    order.Add(itemId);
                }
            }

            for (var index = 0; index < statuses.Count; index++)
            {
                var status = statuses[index];
                if (status.IsDestroyed && seen.Add(status.ItemId))
                {
                    order.Add(status.ItemId);
                }
            }

            return order;
        }
    }
}
