using Game.Core.Items;
using Game.Core.Match;
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
                new System.Random(1234));
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
            Assert.That(session.TryGetCurrentHighlight(out var highlightId), Is.True);
            Assert.That(highlightId, Is.EqualTo("first"));
            Assert.That(session.CompleteCurrentHighlight(), Is.True);
            Assert.That(session.CompleteCurrentHighlight(), Is.True);
            Assert.That(state.CurrentPhase.CurrentValue, Is.EqualTo(MatchPhase.Result));
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
