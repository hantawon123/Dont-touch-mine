using System.Collections.Generic;
using Game.Core.Items;
using Game.Core.Match;
using Game.Server.Items;
using Game.Server.Match;
using Game.Server.Players;
using Game.SOAP.Config;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class MatchSessionCoordinatorTests
    {
        private MatchRulesSO rules;
        private MatchState state;
        private MatchSessionCoordinator session;
        private Vector3[] lastKnownPositions;

        [SetUp]
        public void SetUp()
        {
            rules = ScriptableObject.CreateInstance<MatchRulesSO>();
            state = new MatchState();
            var flow = new MatchFlow(rules, state);
            var interactions = new PlayerInteractionSystem(rules);
            session = new MatchSessionCoordinator(
                rules,
                state,
                flow,
                interactions,
                new TestPlacementValidator(),
                CreateSpawnPoints(),
                CreateItemDefinitions(),
                new System.Random(1234),
                new[]
                {
                    new WorldObjectState("shelf", new Pose(Vector3.zero, Quaternion.identity))
                });
            lastKnownPositions = new Vector3[MatchRulesSO.PlayerCount];
            for (var playerIndex = 0; playerIndex < lastKnownPositions.Length; playerIndex++)
            {
                lastKnownPositions[playerIndex] = new Vector3(playerIndex, 0f, 0f);
            }
        }

        [TearDown]
        public void TearDown()
        {
            state.Dispose();
            Object.DestroyImmediate(rules);
        }

        [Test]
        public void AdvanceTime_FinalizesEachHidingTurnAndStartsSearching()
        {
            session.Start(10d);
            var manualPose = new Pose(new Vector3(9f, 1f, 2f), Quaternion.identity);

            Assert.That(session.TryRecordItemPlacement(1, manualPose, 20d), Is.False);
            Assert.That(session.TryRecordItemPlacement(0, manualPose, 20d), Is.True);

            session.AdvanceTime(40d, lastKnownPositions);
            Assert.That(session.TryGetItemPlacement(0, out var firstPlacement), Is.True);
            Assert.That(firstPlacement.Pose.position, Is.EqualTo(manualPose.position));
            Assert.That(firstPlacement.WasAutoPlaced, Is.False);

            session.AdvanceTime(190d, lastKnownPositions);
            Assert.That(session.AllItemsPlaced, Is.True);
            Assert.That(state.CurrentPhase.CurrentValue, Is.EqualTo(MatchPhase.Searching));
            Assert.That(session.TryGetItemPlacement(1, out var secondPlacement), Is.True);
            Assert.That(secondPlacement.Pose.position, Is.EqualTo(lastKnownPositions[1]));
            Assert.That(secondPlacement.WasAutoPlaced, Is.True);
        }

        [Test]
        public void FinalWarning_RaisesOnceWhenSearchingEntersLastThirtySeconds()
        {
            session.Start(10d);
            FinalWarningStartedEvent? warningEvent = null;
            var eventCount = 0;
            session.FinalWarningStarted += value =>
            {
                warningEvent = value;
                eventCount++;
            };

            session.AdvanceTime(519d, lastKnownPositions);
            Assert.That(warningEvent.HasValue, Is.False);

            session.AdvanceTime(520d, lastKnownPositions);
            session.AdvanceTime(530d, lastKnownPositions);

            Assert.That(eventCount, Is.EqualTo(1));
            Assert.That(warningEvent.HasValue, Is.True);
            Assert.That(warningEvent.Value.StartedAt, Is.EqualTo(520d));
            Assert.That(warningEvent.Value.EndsAt, Is.EqualTo(550d));
        }

        [Test]
        public void HudStateQueries_ReturnCurrentPublicMatchState()
        {
            session.Start(10d);

            Assert.That(session.GetRemainingSeconds(10d), Is.EqualTo(180d));
            Assert.That(session.GetCurrentHidingTurnIndex(40d), Is.EqualTo(1));

            session.AdvanceTime(190d, lastKnownPositions);
            Assert.That(session.GetRemainingSeconds(190d), Is.EqualTo(360d));
            Assert.That(session.IsFinalPeriod(519d), Is.False);
            Assert.That(session.IsFinalPeriod(520d), Is.True);

            Assert.That(
                session.RegisterHit(0, 1, Vector3.zero, 200d),
                Is.EqualTo(HitResult.Registered));
            Assert.That(session.GetHitCount(1), Is.EqualTo(1));
            Assert.That(session.GetRemainingDestructionUses(0), Is.EqualTo(5));

            var destroyedItemId = session.Assignments[1].Item.ItemId;
            Assert.That(session.TryHoldObject(0, destroyedItemId, 200d), Is.True);
            Assert.That(session.TryDestroyHeldPlayerItem(0, 200d), Is.True);

            Assert.That(session.DestroyedPlayerItemCount, Is.EqualTo(1));
            Assert.That(
                session.CaptureDestroyedPlayerItemIds(),
                Is.EqualTo(new[] { destroyedItemId }));
        }

        [Test]
        public void SpawnPoses_AreUniqueForHidingAndSearching()
        {
            session.Start(10d);
            var hidingPositions = new HashSet<Vector3>();
            var searchingPositions = new HashSet<Vector3>();

            for (var playerIndex = 0; playerIndex < MatchRulesSO.PlayerCount; playerIndex++)
            {
                var turnStartedAt = 10d + (playerIndex * rules.HidingTurnDurationSeconds);
                Assert.That(
                    session.TryGetCurrentHidingSpawnPose(
                        playerIndex,
                        turnStartedAt,
                        out var hidingSpawnPose),
                    Is.True);
                Assert.That(hidingPositions.Add(hidingSpawnPose.position), Is.True);

                var searchingSpawnPose = session.GetSearchingSpawnPose(playerIndex);
                Assert.That(searchingPositions.Add(searchingSpawnPose.position), Is.True);
            }

            Assert.That(session.TryGetCurrentHidingSpawnPose(1, 10d, out _), Is.False);
        }

        [Test]
        public void DestroyingAllPlayerItems_ConsumesUsesAndEndsSearchingEarly()
        {
            StartSearching();

            Assert.That(session.TryDestroyHeldPlayerItem(0, 200d), Is.False);
            Assert.That(session.GetRemainingDestructionUses(0), Is.EqualTo(5));

            for (var itemOwner = 0; itemOwner < MatchRulesSO.PlayerCount; itemOwner++)
            {
                var destroyer = (itemOwner + 1) % MatchRulesSO.PlayerCount;
                var itemId = session.Assignments[itemOwner].Item.ItemId;
                Assert.That(session.TryHoldObject(destroyer, itemId, 200d), Is.True);
                Assert.That(session.TryDestroyHeldPlayerItem(destroyer, 200d), Is.True);
            }

            Assert.That(session.AllPlayerItemsDestroyed, Is.True);
            Assert.That(state.CurrentPhase.CurrentValue, Is.EqualTo(MatchPhase.Highlight));
            Assert.That(state.PhaseEndsAt.CurrentValue, Is.EqualTo(230d));
            Assert.That(session.TryGetResult(out var result), Is.True);
            Assert.That(result.EndReason, Is.EqualTo(MatchEndReason.AllPlayerItemsDestroyed));
            Assert.That(result.EndedAt, Is.EqualTo(200d));
            Assert.That(result.WinnerPlayerIndices, Is.Empty);
        }

        [Test]
        public void WorldObjectPose_PersistsBetweenHidingTurns()
        {
            session.Start(10d);
            var firstPose = new Pose(new Vector3(1f, 2f, 3f), Quaternion.identity);
            var secondPose = new Pose(new Vector3(4f, 5f, 6f), Quaternion.identity);

            Assert.That(
                session.TryRecordWorldObjectPose(1, "shelf", firstPose, 20d),
                Is.False);
            Assert.That(
                session.TryRecordWorldObjectPose(0, "shelf", firstPose, 20d),
                Is.True);

            session.AdvanceTime(40d, lastKnownPositions);

            Assert.That(
                session.TryRecordWorldObjectPose(1, "shelf", secondPose, 45d),
                Is.True);
            Assert.That(session.TryGetWorldObjectState("shelf", out var state), Is.True);
            Assert.That(state.Pose.position, Is.EqualTo(secondPose.position));
        }

        [Test]
        public void PlacementValidator_RejectsInvalidPlayerAndMapObjectPoses()
        {
            session.Start(10d);
            var invalidPose = new Pose(new Vector3(-1f, 0f, 0f), Quaternion.identity);
            var validPose = new Pose(new Vector3(1f, 0f, 0f), Quaternion.identity);

            Assert.That(session.TryRecordItemPlacement(0, invalidPose, 20d), Is.False);
            Assert.That(session.TryGetItemPlacement(0, out _), Is.False);
            Assert.That(
                session.TryRecordWorldObjectPose(0, "shelf", invalidPose, 20d),
                Is.False);
            Assert.That(session.TryGetWorldObjectState("shelf", out var state), Is.True);
            Assert.That(state.Pose.position, Is.EqualTo(Vector3.zero));

            Assert.That(session.TryRecordItemPlacement(0, validPose, 20d), Is.True);
            Assert.That(
                session.TryRecordWorldObjectPose(0, "shelf", validPose, 20d),
                Is.True);
        }

        [Test]
        public void Shredder_EjectsMapObjectAfterHalfSecondWithoutDestroyingIt()
        {
            StartSearching();
            var ejectionPose = new Pose(new Vector3(3f, 0f, 4f), Quaternion.identity);

            Assert.That(
                session.TryUseShredderOnHeldMapObject(0, ejectionPose, 200d),
                Is.False);
            Assert.That(session.GetRemainingDestructionUses(0), Is.EqualTo(5));
            Assert.That(session.TryHoldObject(0, "shelf", 200d), Is.True);
            Assert.That(
                session.TryUseShredderOnHeldMapObject(0, ejectionPose, 200d),
                Is.True);
            Assert.That(session.GetRemainingDestructionUses(0), Is.EqualTo(4));
            Assert.That(
                session.TryUseShredderOnHeldMapObject(0, ejectionPose, 200.25d),
                Is.False);

            session.AdvanceTime(200.49d, lastKnownPositions);
            Assert.That(session.TryGetWorldObjectState("shelf", out var state), Is.True);
            Assert.That(state.Pose.position, Is.EqualTo(Vector3.zero));

            session.AdvanceTime(200.5d, lastKnownPositions);
            Assert.That(session.TryGetWorldObjectState("shelf", out state), Is.True);
            Assert.That(state.Pose.position, Is.EqualTo(ejectionPose.position));
            Assert.That(session.GetRemainingDestructionUses(0), Is.EqualTo(4));
            Assert.That(session.TryHoldObject(1, "shelf", 200.5d), Is.True);
        }

        [Test]
        public void PlayerAndMapObjects_ShareOneHeldObjectSlot()
        {
            StartSearching();
            var playerItem = session.Assignments[0].Item.ItemId;
            var releasedPose = new Pose(new Vector3(2f, 0f, 3f), Quaternion.identity);

            Assert.That(session.TryHoldObject(0, "shelf", 200d), Is.True);
            Assert.That(session.TryGetHeldObjectId(0, out var heldObjectId), Is.True);
            Assert.That(heldObjectId, Is.EqualTo("shelf"));
            Assert.That(session.TryHoldObject(0, playerItem, 200d), Is.False);
            Assert.That(session.TryHoldObject(1, "shelf", 200d), Is.False);

            Assert.That(session.TryReleaseHeldObject(0, releasedPose, 200d), Is.True);
            Assert.That(session.TryGetHeldObjectId(0, out _), Is.False);
            Assert.That(session.TryGetWorldObjectState("shelf", out var state), Is.True);
            Assert.That(state.Pose.position, Is.EqualTo(releasedPose.position));
            Assert.That(session.TryHoldObject(0, playerItem, 200d), Is.True);
        }

        [Test]
        public void SearchTimeout_PreservesMultipleWinnersAndPlaysHighlights()
        {
            StartSearching();
            var playerZeroItem = session.Assignments[0].Item.ItemId;
            var playerTwoItem = session.Assignments[2].Item.ItemId;
            var playerFiveItem = session.Assignments[5].Item.ItemId;

            Assert.That(session.TryHoldObject(0, playerZeroItem, 200d), Is.True);
            Assert.That(session.TryHoldObject(1, playerTwoItem, 200d), Is.True);
            Assert.That(session.TryHoldObject(5, playerFiveItem, 200d), Is.True);
            Assert.That(session.SetHighlightCandidates(new[] { "first", "second" }), Is.True);

            session.AdvanceTime(550d, lastKnownPositions);

            Assert.That(state.CurrentPhase.CurrentValue, Is.EqualTo(MatchPhase.Highlight));
            Assert.That(session.GetWinnerPlayerIndices(), Is.EqualTo(new[] { 0, 5 }));
            Assert.That(session.TryGetResult(out var result), Is.True);
            Assert.That(result.EndReason, Is.EqualTo(MatchEndReason.TimeExpired));
            Assert.That(result.EndedAt, Is.EqualTo(550d));
            Assert.That(result.WinnerPlayerIndices, Is.EqualTo(new[] { 0, 5 }));
            Assert.That(session.TryGetCurrentHighlight(out var highlightId), Is.True);
            Assert.That(highlightId, Is.EqualTo("first"));
            Assert.That(session.CompleteCurrentHighlight(), Is.True);
            Assert.That(session.CompleteCurrentHighlight(), Is.True);
            Assert.That(state.CurrentPhase.CurrentValue, Is.EqualTo(MatchPhase.Result));
        }

        [Test]
        public void ThirdHit_DropsHeldItemAndBlocksStunnedPlayerActions()
        {
            StartSearching();
            var heldItem = session.Assignments[1].Item.ItemId;
            var otherItem = session.Assignments[2].Item.ItemId;
            var dropPosition = new Vector3(7f, 0f, 8f);
            PlayerStunnedEvent? stunnedEvent = null;
            var eventCount = 0;
            session.PlayerStunned += value =>
            {
                stunnedEvent = value;
                eventCount++;
            };

            Assert.That(session.TryHoldObject(1, heldItem, 200d), Is.True);
            Assert.That(
                session.RegisterHit(0, 1, dropPosition, 200d),
                Is.EqualTo(HitResult.Registered));
            Assert.That(
                session.RegisterHit(0, 1, dropPosition, 200.1d),
                Is.EqualTo(HitResult.Registered));
            Assert.That(stunnedEvent.HasValue, Is.False);
            Assert.That(
                session.RegisterHit(0, 1, dropPosition, 200.2d),
                Is.EqualTo(HitResult.Stunned));

            Assert.That(eventCount, Is.EqualTo(1));
            Assert.That(stunnedEvent.HasValue, Is.True);
            Assert.That(stunnedEvent.Value.AttackerPlayerIndex, Is.Zero);
            Assert.That(stunnedEvent.Value.TargetPlayerIndex, Is.EqualTo(1));
            Assert.That(stunnedEvent.Value.DroppedObjectId, Is.EqualTo(heldItem));
            Assert.That(stunnedEvent.Value.StunnedAt, Is.EqualTo(200.2d));
            Assert.That(stunnedEvent.Value.StunEndsAt, Is.EqualTo(202.2d));
            Assert.That(session.IsPlayerStunned(1, 200.3d), Is.True);
            Assert.That(session.TryHoldObject(1, otherItem, 200.3d), Is.False);
            Assert.That(session.TryDestroyHeldPlayerItem(1, 200.3d), Is.False);
            Assert.That(
                session.TryUseShredderOnHeldMapObject(1, Pose.identity, 200.3d),
                Is.False);
            Assert.That(session.GetRemainingDestructionUses(1), Is.EqualTo(5));
            Assert.That(
                session.RegisterHit(1, 2, Vector3.zero, 200.3d),
                Is.EqualTo(HitResult.Ignored));

            Assert.That(session.TryGetItemPlacement(1, out var placement), Is.True);
            Assert.That(placement.Pose.position, Is.EqualTo(dropPosition));
            Assert.That(session.TryHoldObject(2, heldItem, 200.3d), Is.True);

            Assert.That(session.IsPlayerStunned(1, 202.2d), Is.False);
            Assert.That(session.TryHoldObject(1, otherItem, 202.2d), Is.True);
        }

        [Test]
        public void ThirdHit_DropsHeldMapObject()
        {
            StartSearching();
            var dropPosition = new Vector3(4f, 0f, 6f);
            PlayerStunnedEvent? stunnedEvent = null;
            session.PlayerStunned += value => stunnedEvent = value;

            Assert.That(session.TryHoldObject(1, "shelf", 200d), Is.True);
            Assert.That(
                session.RegisterHit(0, 1, dropPosition, 200d),
                Is.EqualTo(HitResult.Registered));
            Assert.That(
                session.RegisterHit(0, 1, dropPosition, 200.1d),
                Is.EqualTo(HitResult.Registered));
            Assert.That(
                session.RegisterHit(0, 1, dropPosition, 200.2d),
                Is.EqualTo(HitResult.Stunned));

            Assert.That(session.TryGetHeldObjectId(1, out _), Is.False);
            Assert.That(stunnedEvent.HasValue, Is.True);
            Assert.That(stunnedEvent.Value.DroppedObjectId, Is.EqualTo("shelf"));
            Assert.That(session.TryGetWorldObjectState("shelf", out var state), Is.True);
            Assert.That(state.Pose.position, Is.EqualTo(dropPosition));
            Assert.That(session.TryHoldObject(2, "shelf", 200.3d), Is.True);
        }

        [Test]
        public void DestroyHeldPlayerItem_RequiresAnOpponentItem()
        {
            StartSearching();
            var ownItem = session.Assignments[0].Item.ItemId;
            var opponentItem = session.Assignments[1].Item.ItemId;
            PlayerItemDestroyedEvent? destroyedEvent = null;
            session.PlayerItemDestroyed += value => destroyedEvent = value;

            Assert.That(session.TryHoldObject(0, ownItem, 200d), Is.True);
            Assert.That(session.TryDestroyHeldPlayerItem(0, 200d), Is.False);
            Assert.That(session.GetRemainingDestructionUses(0), Is.EqualTo(5));
            Assert.That(destroyedEvent.HasValue, Is.False);

            Assert.That(
                session.TryReleaseHeldObject(0, new Pose(Vector3.zero, Quaternion.identity), 200d),
                Is.True);
            Assert.That(session.TryHoldObject(0, opponentItem, 200d), Is.True);
            Assert.That(session.TryDestroyHeldPlayerItem(0, 200d), Is.True);
            Assert.That(session.GetRemainingDestructionUses(0), Is.EqualTo(4));
            Assert.That(session.TryHoldObject(2, opponentItem, 200d), Is.False);
            Assert.That(destroyedEvent.HasValue, Is.True);
            Assert.That(destroyedEvent.Value.DestroyerPlayerIndex, Is.Zero);
            Assert.That(destroyedEvent.Value.ItemId, Is.EqualTo(opponentItem));
            Assert.That(destroyedEvent.Value.DestroyedAt, Is.EqualTo(200d));
        }

        [Test]
        public void LateAdvanceTime_CapturesScheduledSearchEnd()
        {
            session.Start(10d);

            session.AdvanceTime(600d, lastKnownPositions);

            Assert.That(session.TryGetResult(out var result), Is.True);
            Assert.That(result.EndReason, Is.EqualTo(MatchEndReason.TimeExpired));
            Assert.That(result.EndedAt, Is.EqualTo(550d));
        }

        private void StartSearching()
        {
            session.Start(10d);
            session.AdvanceTime(190d, lastKnownPositions);
        }

        private static ItemDefinition[] CreateItemDefinitions()
        {
            return new[]
            {
                new ItemDefinition("bear", "toy"),
                new ItemDefinition("ball", "toy"),
                new ItemDefinition("apple", "food"),
                new ItemDefinition("bread", "food"),
                new ItemDefinition("hammer", "tool"),
                new ItemDefinition("wrench", "tool"),
                new ItemDefinition("cup", "kitchen"),
                new ItemDefinition("plate", "kitchen")
            };
        }

        private static Pose[] CreateSpawnPoints()
        {
            var spawnPoints = new Pose[8];
            for (var index = 0; index < spawnPoints.Length; index++)
            {
                spawnPoints[index] = new Pose(
                    new Vector3(index * 2f, 0f, index),
                    Quaternion.identity);
            }

            return spawnPoints;
        }

        private sealed class TestPlacementValidator : IPlacementValidator
        {
            public bool IsValid(string objectId, Pose pose)
            {
                return pose.position.x >= 0f;
            }
        }
    }
}
