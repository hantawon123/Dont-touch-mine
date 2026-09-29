using System;
using System.Collections.Generic;
using Game.Core.Items;
using Game.Core.Match;

namespace Game.Server.Match
{
    public sealed class MatchOutcomeSystem
    {
        private readonly Dictionary<string, int> itemOwnerById;
        private readonly string[] itemIdByOwner;
        private readonly int[] heldItemOwnerByPlayer;
        private readonly int[] holderByItemOwner;
        private readonly bool[] destroyedItems;

        public MatchOutcomeSystem(IReadOnlyList<PlayerItemAssignment> assignments)
        {
            if (assignments == null)
            {
                throw new ArgumentNullException(nameof(assignments));
            }

            if (assignments.Count == 0)
            {
                throw new ArgumentException("At least one assignment is required.", nameof(assignments));
            }

            itemOwnerById = new Dictionary<string, int>(assignments.Count, StringComparer.Ordinal);
            itemIdByOwner = new string[assignments.Count];
            heldItemOwnerByPlayer = new int[assignments.Count];
            holderByItemOwner = new int[assignments.Count];
            destroyedItems = new bool[assignments.Count];
            Array.Fill(heldItemOwnerByPlayer, -1);
            Array.Fill(holderByItemOwner, -1);

            for (var playerIndex = 0; playerIndex < assignments.Count; playerIndex++)
            {
                var assignment = assignments[playerIndex];
                if (assignment.PlayerIndex != playerIndex ||
                    string.IsNullOrWhiteSpace(assignment.Item.ItemId) ||
                    !itemOwnerById.TryAdd(assignment.Item.ItemId, playerIndex))
                {
                    throw new ArgumentException(
                        "Assignments must be unique and ordered by player index.",
                        nameof(assignments));
                }

                itemIdByOwner[playerIndex] = assignment.Item.ItemId;
            }
        }

        public int DestroyedItemCount { get; private set; }
        public bool AllPlayerItemsDestroyed => DestroyedItemCount == destroyedItems.Length;

        public bool TryHoldItem(int playerIndex, string itemId)
        {
            ValidatePlayerIndex(playerIndex);
            if (string.IsNullOrWhiteSpace(itemId) ||
                !itemOwnerById.TryGetValue(itemId, out var itemOwner) ||
                destroyedItems[itemOwner])
            {
                return false;
            }

            var currentHolder = holderByItemOwner[itemOwner];
            if (heldItemOwnerByPlayer[playerIndex] >= 0 || currentHolder >= 0)
            {
                return false;
            }

            heldItemOwnerByPlayer[playerIndex] = itemOwner;
            holderByItemOwner[itemOwner] = playerIndex;
            return true;
        }

        public bool ReleaseHeldItem(int playerIndex)
        {
            ValidatePlayerIndex(playerIndex);
            var itemOwner = heldItemOwnerByPlayer[playerIndex];
            if (itemOwner < 0)
            {
                return false;
            }

            heldItemOwnerByPlayer[playerIndex] = -1;
            holderByItemOwner[itemOwner] = -1;
            return true;
        }

        internal int GetHeldItemOwner(int playerIndex)
        {
            ValidatePlayerIndex(playerIndex);
            return heldItemOwnerByPlayer[playerIndex];
        }

        public bool DestroyItem(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId) ||
                !itemOwnerById.TryGetValue(itemId, out var itemOwner) ||
                destroyedItems[itemOwner])
            {
                return false;
            }

            var holder = holderByItemOwner[itemOwner];
            if (holder >= 0)
            {
                heldItemOwnerByPlayer[holder] = -1;
                holderByItemOwner[itemOwner] = -1;
            }

            destroyedItems[itemOwner] = true;
            DestroyedItemCount++;
            return true;
        }

        public int[] GetWinnerPlayerIndices()
        {
            var winners = new List<int>();
            for (var playerIndex = 0; playerIndex < heldItemOwnerByPlayer.Length; playerIndex++)
            {
                if (heldItemOwnerByPlayer[playerIndex] == playerIndex)
                {
                    winners.Add(playerIndex);
                }
            }

            return winners.ToArray();
        }

        public PlayerItemStatusSnapshot[] CapturePlayerItemStatuses()
        {
            var status = new PlayerItemStatusSnapshot[itemIdByOwner.Length];
            for (var itemOwner = 0; itemOwner < itemIdByOwner.Length; itemOwner++)
            {
                status[itemOwner] = new PlayerItemStatusSnapshot(
                    itemIdByOwner[itemOwner],
                    destroyedItems[itemOwner]);
            }

            return status;
        }

        public string[] CaptureDestroyedItemIds()
        {
            var itemIds = new string[DestroyedItemCount];
            var resultIndex = 0;
            for (var itemOwner = 0; itemOwner < destroyedItems.Length; itemOwner++)
            {
                if (destroyedItems[itemOwner])
                {
                    itemIds[resultIndex++] = itemIdByOwner[itemOwner];
                }
            }

            return itemIds;
        }

        private void ValidatePlayerIndex(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= heldItemOwnerByPlayer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(playerIndex));
            }
        }
    }
}
