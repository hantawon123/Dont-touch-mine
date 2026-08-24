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
    public enum MatchEndReason
    {
        TimeExpired,
        AllPlayerItemsDestroyed
    }

    public readonly struct MatchResult
    {
        public MatchResult(
            MatchEndReason endReason,
            double endedAt,
            int[] winnerPlayerIndices)
        {
            EndReason = endReason;
            EndedAt = endedAt;
            WinnerPlayerIndices = Array.AsReadOnly(
                winnerPlayerIndices ?? throw new ArgumentNullException(nameof(winnerPlayerIndices)));
        }

        public MatchEndReason EndReason { get; }
        public double EndedAt { get; }
        public IReadOnlyList<int> WinnerPlayerIndices { get; }
    }

    public sealed class MatchSessionCoordinator
    {
        private const double MapObjectEjectionDelaySeconds = 0.5d;

        private readonly MatchRulesSO rules;
        private readonly MatchState state;
        private readonly MatchFlow flow;
        private readonly PlayerInteractionSystem interactions;
        private readonly ItemPlacementSystem placements;
        private readonly WorldObjectStateSystem worldObjects;
        private readonly MatchOutcomeSystem outcome;
        private readonly bool[] completedHidingTurns = new bool[MatchRulesSO.PlayerCount];
        private readonly Dictionary<string, PendingMapObjectEjection> pendingMapObjectEjections =
            new(StringComparer.Ordinal);
        private readonly List<string> completedMapObjectEjections = new();
        private HighlightSequence highlights;
        private MatchResult? result;

        public MatchSessionCoordinator(
            MatchRulesSO rules,
            MatchState state,
            MatchFlow flow,
            PlayerInteractionSystem interactions,
            IReadOnlyList<ItemDefinition> itemDefinitions,
            System.Random random,
            IReadOnlyList<WorldObjectState> initialWorldObjects = null)
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
            worldObjects = new WorldObjectStateSystem(
                initialWorldObjects ?? Array.Empty<WorldObjectState>());
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

            if (TryGetExpiredSearchingEnd(now, out var searchingEndedAt))
            {
                CaptureResult(MatchEndReason.TimeExpired, searchingEndedAt);
            }

            if (state.CurrentPhase.CurrentValue == MatchPhase.Hiding)
            {
                CompleteExpiredHidingTurns(now, lastKnownPlayerPositions);
            }

            CompleteMapObjectEjections(now);

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

        public bool TryRecordWorldObjectPose(
            int playerIndex,
            string objectId,
            Pose pose,
            double now)
        {
            if (flow.GetCurrentHidingTurnIndex(now) != playerIndex ||
                flow.GetHidingTurnRemainingSeconds(now) <= 0d ||
                completedHidingTurns[playerIndex])
            {
                return false;
            }

            return worldObjects.TrySetPose(objectId, pose);
        }

        public bool TryGetWorldObjectState(string objectId, out WorldObjectState worldObjectState)
        {
            return worldObjects.TryGetState(objectId, out worldObjectState);
        }

        public WorldObjectState[] CaptureWorldObjectSnapshot()
        {
            return worldObjects.CaptureSnapshot();
        }

        public bool TryHoldItem(int playerIndex, string itemId, double now)
        {
            return CanInteract(playerIndex, now) && outcome.TryHoldItem(playerIndex, itemId);
        }

        public bool TryReleaseHeldItem(int playerIndex, double now)
        {
            return CanInteract(playerIndex, now) && outcome.ReleaseHeldItem(playerIndex);
        }

        public bool TryUseShredderOnMapObject(
            int playerIndex,
            string objectId,
            Pose ejectionPose,
            double now)
        {
            if (!CanInteract(playerIndex, now) ||
                interactions.GetRemainingDestructionUses(playerIndex) == 0 ||
                !worldObjects.TryGetState(objectId, out var worldObject) ||
                pendingMapObjectEjections.ContainsKey(worldObject.ObjectId))
            {
                return false;
            }

            interactions.TryUseDestruction(playerIndex);
            pendingMapObjectEjections.Add(
                worldObject.ObjectId,
                new PendingMapObjectEjection(
                    now + MapObjectEjectionDelaySeconds,
                    ejectionPose));
            return true;
        }

        public bool TryDestroyPlayerItem(int playerIndex, string itemId, double now)
        {
            if (!CanInteract(playerIndex, now) ||
                interactions.GetRemainingDestructionUses(playerIndex) == 0 ||
                !outcome.DestroyItem(itemId))
            {
                return false;
            }

            interactions.TryUseDestruction(playerIndex);
            if (outcome.AllPlayerItemsDestroyed)
            {
                CaptureResult(MatchEndReason.AllPlayerItemsDestroyed, now);
                flow.CompleteSearchingEarly(now);
            }

            return true;
        }

        public HitResult RegisterHit(
            int attackerPlayerIndex,
            int targetPlayerIndex,
            Vector3 targetPosition,
            double now)
        {
            if (!CanInteract(attackerPlayerIndex, now) ||
                attackerPlayerIndex == targetPlayerIndex)
            {
                return HitResult.Ignored;
            }

            var hitResult = interactions.RegisterHit(targetPlayerIndex, now);
            if (hitResult != HitResult.Stunned)
            {
                return hitResult;
            }

            var heldItemOwner = outcome.GetHeldItemOwner(targetPlayerIndex);
            if (heldItemOwner >= 0)
            {
                outcome.ReleaseHeldItem(targetPlayerIndex);
                placements.RecordPlacement(
                    heldItemOwner,
                    new Pose(targetPosition, Quaternion.identity));
            }

            return hitResult;
        }

        public bool IsPlayerStunned(int playerIndex, double now)
        {
            return interactions.IsStunned(playerIndex, now);
        }

        public int GetRemainingDestructionUses(int playerIndex)
        {
            return interactions.GetRemainingDestructionUses(playerIndex);
        }

        public int[] GetWinnerPlayerIndices()
        {
            return outcome.GetWinnerPlayerIndices();
        }

        public bool TryGetResult(out MatchResult matchResult)
        {
            if (result.HasValue)
            {
                matchResult = result.Value;
                return true;
            }

            matchResult = default;
            return false;
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

        private bool CanInteract(int playerIndex, double now)
        {
            return IsSearchingAt(now) && !interactions.IsStunned(playerIndex, now);
        }

        private bool TryGetExpiredSearchingEnd(double now, out double searchingEndedAt)
        {
            switch (state.CurrentPhase.CurrentValue)
            {
                case MatchPhase.Hiding:
                    searchingEndedAt =
                        state.PhaseEndsAt.CurrentValue + rules.SearchingDurationSeconds;
                    return now >= searchingEndedAt;
                case MatchPhase.Searching:
                    searchingEndedAt = state.PhaseEndsAt.CurrentValue;
                    return now >= searchingEndedAt;
                default:
                    searchingEndedAt = 0d;
                    return false;
            }
        }

        private void CaptureResult(MatchEndReason endReason, double endedAt)
        {
            if (!result.HasValue)
            {
                result = new MatchResult(
                    endReason,
                    endedAt,
                    outcome.GetWinnerPlayerIndices());
            }
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

        private void CompleteMapObjectEjections(double now)
        {
            completedMapObjectEjections.Clear();
            foreach (var pair in pendingMapObjectEjections)
            {
                if (now < pair.Value.EjectsAt)
                {
                    continue;
                }

                worldObjects.TrySetPose(pair.Key, pair.Value.Pose);
                completedMapObjectEjections.Add(pair.Key);
            }

            foreach (var objectId in completedMapObjectEjections)
            {
                pendingMapObjectEjections.Remove(objectId);
            }
        }

        private readonly struct PendingMapObjectEjection
        {
            public PendingMapObjectEjection(double ejectsAt, Pose pose)
            {
                EjectsAt = ejectsAt;
                Pose = pose;
            }

            public double EjectsAt { get; }
            public Pose Pose { get; }
        }
    }
}
