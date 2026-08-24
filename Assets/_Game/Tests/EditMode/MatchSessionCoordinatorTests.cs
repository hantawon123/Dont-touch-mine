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
        public void DestroyingAllPlayerItems_ConsumesUsesAndEndsSearchingEarly()
        {
            StartSearching();

            Assert.That(session.TryDestroyPlayerItem(0, "unknown", 200d), Is.False);
            Assert.That(session.GetRemainingDestructionUses(0), Is.EqualTo(5));

            for (var playerIndex = 0; playerIndex < MatchRulesSO.PlayerCount; playerIndex++)
            {
                var itemId = session.Assignments[playerIndex].Item.ItemId;
                Assert.That(
                    session.TryDestroyPlayerItem(playerIndex, itemId, 200d),
                    Is.True);
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
        public void Shredder_EjectsMapObjectAfterHalfSecondWithoutDestroyingIt()
        {
            StartSearching();
            var ejectionPose = new Pose(new Vector3(3f, 0f, 4f), Quaternion.identity);

            Assert.That(
                session.TryUseShredderOnMapObject(0, "unknown", ejectionPose, 200d),
                Is.False);
            Assert.That(session.GetRemainingDestructionUses(0), Is.EqualTo(5));
            Assert.That(
                session.TryUseShredderOnMapObject(0, "shelf", ejectionPose, 200d),
                Is.True);
            Assert.That(session.GetRemainingDestructionUses(0), Is.EqualTo(4));
            Assert.That(
                session.TryUseShredderOnMapObject(0, "shelf", ejectionPose, 200.25d),
                Is.False);

            session.AdvanceTime(200.49d, lastKnownPositions);
            Assert.That(session.TryGetWorldObjectState("shelf", out var state), Is.True);
            Assert.That(state.Pose.position, Is.EqualTo(Vector3.zero));

            session.AdvanceTime(200.5d, lastKnownPositions);
            Assert.That(session.TryGetWorldObjectState("shelf", out state), Is.True);
            Assert.That(state.Pose.position, Is.EqualTo(ejectionPose.position));
            Assert.That(session.GetRemainingDestructionUses(0), Is.EqualTo(4));
        }

        [Test]
        public void SearchTimeout_PreservesMultipleWinnersAndPlaysHighlights()
        {
            StartSearching();
            var playerZeroItem = session.Assignments[0].Item.ItemId;
            var playerTwoItem = session.Assignments[2].Item.ItemId;
            var playerFiveItem = session.Assignments[5].Item.ItemId;

            Assert.That(session.TryHoldItem(0, playerZeroItem, 200d), Is.True);
            Assert.That(session.TryHoldItem(1, playerTwoItem, 200d), Is.True);
            Assert.That(session.TryHoldItem(5, playerFiveItem, 200d), Is.True);
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

            Assert.That(session.TryHoldItem(1, heldItem, 200d), Is.True);
            Assert.That(
                session.RegisterHit(0, 1, dropPosition, 200d),
                Is.EqualTo(HitResult.Registered));
            Assert.That(
                session.RegisterHit(0, 1, dropPosition, 200.1d),
                Is.EqualTo(HitResult.Registered));
            Assert.That(
                session.RegisterHit(0, 1, dropPosition, 200.2d),
                Is.EqualTo(HitResult.Stunned));

            Assert.That(session.IsPlayerStunned(1, 200.3d), Is.True);
            Assert.That(session.TryHoldItem(1, otherItem, 200.3d), Is.False);
            Assert.That(session.TryDestroyPlayerItem(1, otherItem, 200.3d), Is.False);
            Assert.That(
                session.TryUseShredderOnMapObject(1, "shelf", Pose.identity, 200.3d),
                Is.False);
            Assert.That(session.GetRemainingDestructionUses(1), Is.EqualTo(5));
            Assert.That(
                session.RegisterHit(1, 2, Vector3.zero, 200.3d),
                Is.EqualTo(HitResult.Ignored));

            Assert.That(session.TryGetItemPlacement(1, out var placement), Is.True);
            Assert.That(placement.Pose.position, Is.EqualTo(dropPosition));
            Assert.That(session.TryHoldItem(2, heldItem, 200.3d), Is.True);

            Assert.That(session.IsPlayerStunned(1, 202.2d), Is.False);
            Assert.That(session.TryHoldItem(1, otherItem, 202.2d), Is.True);
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
    }
}
