using System;
using System.Collections.Generic;
using Game.Core.Items;
using Game.Core.Match;
using Game.Server.Items;
using Game.Server.Players;
using Game.SOAP.Config;
using UnityEngine;

namespace Game.Server.Match
{
    public sealed class MatchSessionCoordinator
    {
        private readonly MatchRulesSO rules;
        private readonly MatchState state;
        private readonly MatchFlow flow;
        private readonly PlayerInteractionSystem interactions;
        private readonly ItemPlacementSystem placements;
        private readonly MatchOutcomeSystem outcome;
        private readonly bool[] completedHidingTurns = new bool[MatchRulesSO.PlayerCount];
        private HighlightSequence highlights;

        public MatchSessionCoordinator(
            MatchRulesSO rules,
            MatchState state,
            MatchFlow flow,
            PlayerInteractionSystem interactions,
            IReadOnlyList<ItemDefinition> itemDefinitions,
            System.Random random)
        {
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.flow = flow ?? throw new ArgumentNullException(nameof(flow));
            this.interactions = interactions ??
                throw new ArgumentNullException(nameof(interactions));

            var assignments = ItemAssignmentSystem.Assign(
                itemDefinitions,
                MatchRulesSO.PlayerCount,
                random);
            Assignments = assignments;
            placements = new ItemPlacementSystem(assignments);
            outcome = new MatchOutcomeSystem(assignments);
            highlights = new HighlightSequence(Array.Empty<string>(), rules);
        }

        public IReadOnlyList<PlayerItemAssignment> Assignments { get; }
        public bool AllItemsPlaced => placements.AllPlaced;
        public bool AllPlayerItemsDestroyed => outcome.AllPlayerItemsDestroyed;

        public bool Start(double now)
        {
            return flow.Start(now);
        }

        public bool AdvanceTime(double now, IReadOnlyList<Vector3> lastKnownPlayerPositions)
        {
            flow.GetRemainingSeconds(now);

            if (lastKnownPlayerPositions == null ||
                lastKnownPlayerPositions.Count != MatchRulesSO.PlayerCount)
            {
                throw new ArgumentException(
                    $"Exactly {MatchRulesSO.PlayerCount} player positions are required.",
                    nameof(lastKnownPlayerPositions));
            }

            if (state.CurrentPhase.CurrentValue == MatchPhase.Hiding)
            {
                CompleteExpiredHidingTurns(now, lastKnownPlayerPositions);
            }

            var changed = flow.AdvanceIfExpired(now);
            if (state.CurrentPhase.CurrentValue == MatchPhase.Highlight && highlights.IsComplete)
            {
                changed |= flow.CompleteHighlight();
            }

            return changed;
        }

        public bool TryRecordItemPlacement(int playerIndex, Pose pose, double now)
        {
            if (flow.GetCurrentHidingTurnIndex(now) != playerIndex ||
                flow.GetHidingTurnRemainingSeconds(now) <= 0d ||
                completedHidingTurns[playerIndex])
            {
                return false;
            }

            placements.RecordPlacement(playerIndex, pose);
            return true;
        }

        public bool TryGetItemPlacement(int playerIndex, out ItemPlacement placement)
        {
            return placements.TryGetPlacement(playerIndex, out placement);
        }

        public bool TryHoldItem(int playerIndex, string itemId, double now)
        {
            return IsSearchingAt(now) && outcome.TryHoldItem(playerIndex, itemId);
        }

        public bool TryReleaseHeldItem(int playerIndex, double now)
        {
            return IsSearchingAt(now) && outcome.ReleaseHeldItem(playerIndex);
        }

        public bool TryDestroyMapObject(int playerIndex, double now)
        {
            return IsSearchingAt(now) && interactions.TryUseDestruction(playerIndex);
        }

        public bool TryDestroyPlayerItem(int playerIndex, string itemId, double now)
        {
            if (!IsSearchingAt(now) ||
                interactions.GetRemainingDestructionUses(playerIndex) == 0 ||
                !outcome.DestroyItem(itemId))
            {
                return false;
            }

            interactions.TryUseDestruction(playerIndex);
            if (outcome.AllPlayerItemsDestroyed)
            {
                flow.CompleteSearchingEarly(now);
            }

            return true;
        }

        public HitResult RegisterHit(int playerIndex, double now)
        {
            return IsSearchingAt(now)
                ? interactions.RegisterHit(playerIndex, now)
                : HitResult.Ignored;
        }

        public int GetRemainingDestructionUses(int playerIndex)
        {
            return interactions.GetRemainingDestructionUses(playerIndex);
        }

        public int[] GetWinnerPlayerIndices()
        {
            return outcome.GetWinnerPlayerIndices();
        }

        public bool SetHighlightCandidates(IReadOnlyList<string> candidateIds)
        {
            var phase = state.CurrentPhase.CurrentValue;
            if (phase == MatchPhase.Highlight || phase == MatchPhase.Result)
            {
                return false;
            }

            highlights = new HighlightSequence(candidateIds, rules);
            return true;
        }

        public bool TryGetCurrentHighlight(out string highlightId)
        {
            if (state.CurrentPhase.CurrentValue != MatchPhase.Highlight)
            {
                highlightId = null;
                return false;
            }

            return highlights.TryGetCurrent(out highlightId);
        }

        public bool CompleteCurrentHighlight()
        {
            if (state.CurrentPhase.CurrentValue != MatchPhase.Highlight ||
                !highlights.CompleteCurrent())
            {
                return false;
            }

            if (highlights.IsComplete)
            {
                flow.CompleteHighlight();
            }

            return true;
        }

        private bool IsSearchingAt(double now)
        {
            return state.CurrentPhase.CurrentValue == MatchPhase.Searching &&
                   flow.GetRemainingSeconds(now) > 0d;
        }

        private void CompleteExpiredHidingTurns(
            double now,
            IReadOnlyList<Vector3> lastKnownPlayerPositions)
        {
            var hidingStartedAt =
                state.PhaseEndsAt.CurrentValue - rules.HidingDurationSeconds;
            var elapsedSeconds = Math.Max(0d, now - hidingStartedAt);
            var expiredTurnCount = Math.Min(
                MatchRulesSO.PlayerCount,
                (int)(elapsedSeconds / rules.HidingTurnDurationSeconds));

            for (var playerIndex = 0; playerIndex < expiredTurnCount; playerIndex++)
            {
                if (completedHidingTurns[playerIndex])
                {
                    continue;
                }

                placements.CompleteTurn(playerIndex, lastKnownPlayerPositions[playerIndex]);
                completedHidingTurns[playerIndex] = true;
            }
        }
    }
}
