using Game.Core.Items;
using Game.Server.Items;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class ItemPlacementSystemTests
    {
        [Test]
        public void CompleteTurn_KeepsLatestManualPlacement()
        {
            var system = new ItemPlacementSystem(CreateAssignments());
            var latestPose = new Pose(new Vector3(4f, 5f, 6f), Quaternion.Euler(0f, 90f, 0f));

            system.RecordPlacement(0, new Pose(Vector3.one, Quaternion.identity));
            system.RecordPlacement(0, latestPose);
            var placement = system.CompleteTurn(0, Vector3.zero);

            Assert.That(placement.ItemId, Is.EqualTo("item-0"));
            Assert.That(placement.Pose.position, Is.EqualTo(latestPose.position));
            Assert.That(placement.Pose.rotation, Is.EqualTo(latestPose.rotation));
            Assert.That(placement.WasAutoPlaced, Is.False);
            Assert.That(system.PlacedCount, Is.EqualTo(1));
        }

        [Test]
        public void CompleteTurn_UsesLastPlayerPositionWhenItemWasNotPlaced()
        {
            var system = new ItemPlacementSystem(CreateAssignments());
            var lastPlayerPosition = new Vector3(7f, 8f, 9f);

            var placement = system.CompleteTurn(1, lastPlayerPosition);

            Assert.That(placement.ItemId, Is.EqualTo("item-1"));
            Assert.That(placement.Pose.position, Is.EqualTo(lastPlayerPosition));
            Assert.That(placement.Pose.rotation, Is.EqualTo(Quaternion.identity));
            Assert.That(placement.WasAutoPlaced, Is.True);
        }

        [Test]
        public void AllPlaced_BecomesTrueAfterAllSixTurnsComplete()
        {
            var system = new ItemPlacementSystem(CreateAssignments());

            for (var playerIndex = 0; playerIndex < 6; playerIndex++)
            {
                system.CompleteTurn(playerIndex, new Vector3(playerIndex, 0f, 0f));
            }

            Assert.That(system.PlacedCount, Is.EqualTo(6));
            Assert.That(system.AllPlaced, Is.True);
            Assert.That(system.TryGetPlacement(5, out var placement), Is.True);
            Assert.That(placement.PlayerIndex, Is.EqualTo(5));
        }

        private static PlayerItemAssignment[] CreateAssignments()
        {
            var assignments = new PlayerItemAssignment[6];
            for (var playerIndex = 0; playerIndex < assignments.Length; playerIndex++)
            {
                assignments[playerIndex] = new PlayerItemAssignment(
                    playerIndex,
                    new ItemDefinition($"item-{playerIndex}", "test"));
            }

            return assignments;
        }
    }
}
