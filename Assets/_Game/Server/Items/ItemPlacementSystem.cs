using System;
using System.Collections.Generic;
using Game.Core.Items;
using UnityEngine;

namespace Game.Server.Items
{
    public interface IPlacementValidator
    {
        bool IsValid(string objectId, Pose pose);
    }

    public readonly struct ItemPlacement
    {
        public ItemPlacement(
            int playerIndex,
            string itemId,
            Pose pose,
            bool wasAutoPlaced)
        {
            PlayerIndex = playerIndex;
            ItemId = itemId;
            Pose = pose;
            WasAutoPlaced = wasAutoPlaced;
        }

        public int PlayerIndex { get; }
        public string ItemId { get; }
        public Pose Pose { get; }
        public bool WasAutoPlaced { get; }
    }

    public sealed class ItemPlacementSystem
    {
        private readonly string[] itemIds;
        private readonly ItemPlacement?[] placements;

        public ItemPlacementSystem(IReadOnlyList<PlayerItemAssignment> assignments)
        {
            if (assignments == null)
            {
                throw new ArgumentNullException(nameof(assignments));
            }

            if (assignments.Count == 0)
            {
                throw new ArgumentException("At least one assignment is required.", nameof(assignments));
            }

            itemIds = new string[assignments.Count];
            placements = new ItemPlacement?[assignments.Count];

            for (var playerIndex = 0; playerIndex < assignments.Count; playerIndex++)
            {
                var assignment = assignments[playerIndex];
                if (assignment.PlayerIndex != playerIndex ||
                    string.IsNullOrWhiteSpace(assignment.Item.ItemId))
                {
                    throw new ArgumentException(
                        "Assignments must be ordered by player index.",
                        nameof(assignments));
                }

                itemIds[playerIndex] = assignment.Item.ItemId;
            }
        }

        public int PlacedCount { get; private set; }
        public bool AllPlaced => PlacedCount == placements.Length;

        public void RecordPlacement(
            int playerIndex,
            Pose pose,
            bool wasAutoPlaced = false)
        {
            ValidatePlayerIndex(playerIndex);

            if (!placements[playerIndex].HasValue)
            {
                PlacedCount++;
            }

            placements[playerIndex] = new ItemPlacement(
                playerIndex,
                itemIds[playerIndex],
                pose,
                wasAutoPlaced);
        }

        public ItemPlacement CompleteTurn(int playerIndex, Vector3 lastPlayerPosition)
        {
            ValidatePlayerIndex(playerIndex);

            if (placements[playerIndex].HasValue)
            {
                return placements[playerIndex].Value;
            }

            var placement = new ItemPlacement(
                playerIndex,
                itemIds[playerIndex],
                new Pose(lastPlayerPosition, Quaternion.identity),
                true);
            placements[playerIndex] = placement;
            PlacedCount++;
            return placement;
        }

        public bool TryGetPlacement(int playerIndex, out ItemPlacement placement)
        {
            ValidatePlayerIndex(playerIndex);
            if (placements[playerIndex].HasValue)
            {
                placement = placements[playerIndex].Value;
                return true;
            }

            placement = default;
            return false;
        }

        private void ValidatePlayerIndex(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= placements.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(playerIndex));
            }
        }
    }
}
