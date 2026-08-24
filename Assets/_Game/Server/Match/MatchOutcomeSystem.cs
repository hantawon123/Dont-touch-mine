using System;
using System.Collections.Generic;
using Game.Core.Items;

namespace Game.Server.Match
{
    public sealed class MatchOutcomeSystem
    {
        private readonly Dictionary<string, int> itemOwnerById;
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
            if (currentHolder >= 0 && currentHolder != playerIndex)
            {
                return false;
            }

            ReleaseHeldItem(playerIndex);
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

        private void ValidatePlayerIndex(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= heldItemOwnerByPlayer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(playerIndex));
            }
        }
    }
}
