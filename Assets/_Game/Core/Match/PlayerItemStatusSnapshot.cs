using System;

namespace Game.Core.Match
{
    public readonly struct PlayerItemStatusSnapshot
    {
        public PlayerItemStatusSnapshot(
            string itemId,
            bool isDestroyed)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                throw new ArgumentException("Item id is required.", nameof(itemId));
            }

            ItemId = itemId.Trim();
            IsDestroyed = isDestroyed;
        }

        public string ItemId { get; }
        public bool IsDestroyed { get; }
    }
}
