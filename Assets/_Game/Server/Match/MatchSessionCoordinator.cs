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

    public readonly struct PlayerItemDestroyedEvent
    {
        public PlayerItemDestroyedEvent(
            int destroyerPlayerIndex,
            string itemId,
            double destroyedAt)
        {
            DestroyerPlayerIndex = destroyerPlayerIndex;
            ItemId = itemId ?? throw new ArgumentNullException(nameof(itemId));
            DestroyedAt = destroyedAt;
        }

        public int DestroyerPlayerIndex { get; }
        public string ItemId { get; }
        public double DestroyedAt { get; }
    }

    public readonly struct FinalWarningStartedEvent
    {
        public FinalWarningStartedEvent(double startedAt, double endsAt)
        {
            StartedAt = startedAt;
            EndsAt = endsAt;
        }

        public double StartedAt { get; }
        public double EndsAt { get; }
    }

    public readonly struct PlayerStunnedEvent
    {
        public PlayerStunnedEvent(
            int attackerPlayerIndex,
            int targetPlayerIndex,
            string droppedObjectId,
            double stunnedAt,
            double stunEndsAt)
        {
            AttackerPlayerIndex = attackerPlayerIndex;
            TargetPlayerIndex = targetPlayerIndex;
            DroppedObjectId = droppedObjectId;
            StunnedAt = stunnedAt;
            StunEndsAt = stunEndsAt;
        }

        public int AttackerPlayerIndex { get; }
        public int TargetPlayerIndex { get; }
        public string DroppedObjectId { get; }
        public double StunnedAt { get; }
        public double StunEndsAt { get; }
    }

    public sealed class MatchSessionCoordinator
    {
        private const double MapObjectEjectionDelaySeconds = 0.5d;
        private const double HighlightReplaySampleIntervalSeconds = 0.1d;

        private readonly MatchRulesSO rules;
        private readonly MatchState state;
        private readonly MatchFlow flow;
        private readonly PlayerInteractionSystem interactions;
        private readonly IPlacementValidator placementValidator;
        private readonly ItemPlacementSystem placements;
        private readonly WorldObjectStateSystem worldObjects;
        private readonly MatchOutcomeSystem outcome;
        private readonly HighlightEventRecorder highlightRecorder;
        private readonly HighlightReplayBuffer highlightReplayBuffer;
        private readonly Pose[] hidingSpawnPoses;
        private readonly Pose[] searchingSpawnPoses;
        private readonly bool[] completedHidingTurns = new bool[MatchRulesSO.PlayerCount];
        private readonly Dictionary<string, PendingMapObjectEjection> pendingMapObjectEjections =
            new(StringComparer.Ordinal);
        private readonly List<string> completedMapObjectEjections = new();
        private readonly string[] heldMapObjectIdsByPlayer = new string[MatchRulesSO.PlayerCount];
        private readonly Dictionary<string, int> mapObjectHolderById =
            new(StringComparer.Ordinal);
        private HighlightSequence highlights;
        private MatchResult? result;
        private bool finalWarningStarted;
        private bool hasExplicitHighlightCandidates;

        public MatchSessionCoordinator(
            MatchRulesSO rules,
            MatchState state,
            MatchFlow flow,
            PlayerInteractionSystem interactions,
            IPlacementValidator placementValidator,
            IReadOnlyList<Pose> spawnPoints,
            IReadOnlyList<ItemDefinition> itemDefinitions,
            System.Random random,
            IReadOnlyList<WorldObjectState> initialWorldObjects = null)
        {
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.flow = flow ?? throw new ArgumentNullException(nameof(flow));
            this.interactions = interactions ??
                throw new ArgumentNullException(nameof(interactions));
            this.placementValidator = placementValidator ??
                throw new ArgumentNullException(nameof(placementValidator));

            var assignments = ItemAssignmentSystem.Assign(
                itemDefinitions,
                MatchRulesSO.PlayerCount,
                random);
            Assignments = assignments;
            var validatedSpawnPoints = ValidateSpawnPoints(spawnPoints);
            hidingSpawnPoses = SelectSpawnPoses(validatedSpawnPoints, random);
            searchingSpawnPoses = SelectSpawnPoses(validatedSpawnPoints, random);
            placements = new ItemPlacementSystem(assignments);
            var worldObjectStates = initialWorldObjects ?? Array.Empty<WorldObjectState>();
            worldObjects = new WorldObjectStateSystem(worldObjectStates);
            outcome = new MatchOutcomeSystem(assignments);
            highlightRecorder = new HighlightEventRecorder(rules, assignments);
            highlightReplayBuffer = new HighlightReplayBuffer(
                HighlightReplaySampleIntervalSeconds,
                rules.SearchingDurationSeconds);
            ValidateUniqueObjectIds(assignments, worldObjectStates);
            highlights = new HighlightSequence(Array.Empty<HighlightCandidate>(), rules);
        }

        public IReadOnlyList<PlayerItemAssignment> Assignments { get; }
        public bool AllItemsPlaced => placements.AllPlaced;
        public int DestroyedPlayerItemCount => outcome.DestroyedItemCount;
        public bool AllPlayerItemsDestroyed => outcome.AllPlayerItemsDestroyed;

        public event Action<PlayerItemDestroyedEvent> PlayerItemDestroyed;
        public event Action<FinalWarningStartedEvent> FinalWarningStarted;
        public event Action<PlayerStunnedEvent> PlayerStunned;
        public event Action<MatchResult> MatchEnded;

        public bool Start(double now)
        {
            return flow.Start(now);
        }

        public double GetRemainingSeconds(double now)
        {
            return flow.GetRemainingSeconds(now);
        }

        public int GetCurrentHidingTurnIndex(double now)
        {
            return flow.GetCurrentHidingTurnIndex(now);
        }

        public bool IsFinalPeriod(double now)
        {
            return flow.IsFinalPeriod(now);
        }

        public bool TryGetCurrentHidingSpawnPose(
            int playerIndex,
            double now,
            out Pose spawnPose)
        {
            if (flow.GetCurrentHidingTurnIndex(now) != playerIndex)
            {
                spawnPose = default;
                return false;
            }

            spawnPose = hidingSpawnPoses[playerIndex];
            return true;
        }

        public Pose GetSearchingSpawnPose(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= searchingSpawnPoses.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(playerIndex));
            }

            return searchingSpawnPoses[playerIndex];
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

            StartHighlightRecordingIfNeeded(now);
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
            RaiseFinalWarningIfNeeded(now);
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
                completedHidingTurns[playerIndex] ||
                !placementValidator.IsValid(Assignments[playerIndex].Item.ItemId, pose))
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

            return worldObjects.TryGetState(objectId, out var worldObject) &&
                   placementValidator.IsValid(worldObject.ObjectId, pose) &&
                   worldObjects.TrySetPose(worldObject.ObjectId, pose);
        }

        public bool TryGetWorldObjectState(string objectId, out WorldObjectState worldObjectState)
        {
            return worldObjects.TryGetState(objectId, out worldObjectState);
        }

        public WorldObjectState[] CaptureWorldObjectSnapshot()
        {
            return worldObjects.CaptureSnapshot();
        }

        public bool TryHoldObject(int playerIndex, string objectId, double now)
        {
            if (!CanInteract(playerIndex, now) ||
                outcome.GetHeldItemOwner(playerIndex) >= 0 ||
                heldMapObjectIdsByPlayer[playerIndex] != null)
            {
                return false;
            }

            if (outcome.TryHoldItem(playerIndex, objectId))
            {
                highlightRecorder.RecordItemInteraction(playerIndex, objectId, now);
                return true;
            }

            if (!worldObjects.TryGetState(objectId, out var worldObject) ||
                pendingMapObjectEjections.ContainsKey(worldObject.ObjectId) ||
                mapObjectHolderById.ContainsKey(worldObject.ObjectId))
            {
                return false;
            }

            heldMapObjectIdsByPlayer[playerIndex] = worldObject.ObjectId;
            mapObjectHolderById.Add(worldObject.ObjectId, playerIndex);
            return true;
        }

        public bool TryReleaseHeldObject(int playerIndex, Pose pose, double now)
        {
            if (!CanInteract(playerIndex, now) ||
                !TryGetHeldObjectId(playerIndex, out var objectId) ||
                !ReleaseHeldObjectAt(playerIndex, pose))
            {
                return false;
            }

            highlightRecorder.RecordItemInteraction(playerIndex, objectId, now);
            return true;
        }

        public bool TryGetHeldObjectId(int playerIndex, out string objectId)
        {
            var heldItemOwner = outcome.GetHeldItemOwner(playerIndex);
            if (heldItemOwner >= 0)
            {
                objectId = Assignments[heldItemOwner].Item.ItemId;
                return true;
            }

            objectId = heldMapObjectIdsByPlayer[playerIndex];
            return objectId != null;
        }

        public bool TryUseShredderOnHeldMapObject(
            int playerIndex,
            Pose ejectionPose,
            double now)
        {
            var objectId = heldMapObjectIdsByPlayer[playerIndex];
            if (!CanInteract(playerIndex, now) ||
                interactions.GetRemainingDestructionUses(playerIndex) == 0 ||
                objectId == null ||
                pendingMapObjectEjections.ContainsKey(objectId))
            {
                return false;
            }

            heldMapObjectIdsByPlayer[playerIndex] = null;
            mapObjectHolderById.Remove(objectId);
            interactions.TryUseDestruction(playerIndex);
            pendingMapObjectEjections.Add(
                objectId,
                new PendingMapObjectEjection(
                    now + MapObjectEjectionDelaySeconds,
                    ejectionPose));
            return true;
        }

        public bool TryDestroyHeldPlayerItem(int playerIndex, double now)
        {
            if (!CanInteract(playerIndex, now) ||
                interactions.GetRemainingDestructionUses(playerIndex) == 0)
            {
                return false;
            }

            var heldItemOwner = outcome.GetHeldItemOwner(playerIndex);
            var heldItemId = heldItemOwner >= 0
                ? Assignments[heldItemOwner].Item.ItemId
                : null;
            if (heldItemOwner < 0 ||
                heldItemOwner == playerIndex ||
                !outcome.DestroyItem(heldItemId))
            {
                return false;
            }

            interactions.TryUseDestruction(playerIndex);
            highlightRecorder.RecordItemDestroyed(playerIndex, heldItemId, now);
            PlayerItemDestroyed?.Invoke(
                new PlayerItemDestroyedEvent(
                    playerIndex,
                    heldItemId,
                    now));
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

            TryGetHeldObjectId(targetPlayerIndex, out var droppedObjectId);
            ReleaseHeldObjectAt(
                targetPlayerIndex,
                new Pose(targetPosition, Quaternion.identity));
            if (droppedObjectId != null)
            {
                highlightRecorder.RecordItemInteraction(
                    targetPlayerIndex,
                    droppedObjectId,
                    now);
            }

            highlightRecorder.RecordPlayerStunned(targetPlayerIndex, now);
            PlayerStunned?.Invoke(
                new PlayerStunnedEvent(
                    attackerPlayerIndex,
                    targetPlayerIndex,
                    droppedObjectId,
                    now,
                    now + rules.StunDurationSeconds));

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

        public int GetHitCount(int playerIndex)
        {
            return interactions.GetHitCount(playerIndex);
        }

        public string[] CaptureDestroyedPlayerItemIds()
        {
            return outcome.CaptureDestroyedItemIds();
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

        public bool SetHighlightCandidates(IReadOnlyList<HighlightCandidate> candidates)
        {
            var phase = state.CurrentPhase.CurrentValue;
            if (phase == MatchPhase.Highlight || phase == MatchPhase.Result)
            {
                return false;
            }

            highlights = new HighlightSequence(candidates, rules);
            hasExplicitHighlightCandidates = true;
            return true;
        }

        public bool TryGetCurrentHighlight(out HighlightCandidate highlight)
        {
            if (state.CurrentPhase.CurrentValue != MatchPhase.Highlight)
            {
                highlight = default;
                return false;
            }

            return highlights.TryGetCurrent(out highlight);
        }

        public bool TryRecordReplayFrame(
            double now,
            IReadOnlyList<Pose> playerPoses,
            IReadOnlyList<WorldObjectState> replayObjects)
        {
            if (state.CurrentPhase.CurrentValue != MatchPhase.Searching)
            {
                return false;
            }

            if (playerPoses == null || playerPoses.Count != MatchRulesSO.PlayerCount)
            {
                throw new ArgumentException(
                    $"Exactly {MatchRulesSO.PlayerCount} player poses are required.",
                    nameof(playerPoses));
            }

            return highlightReplayBuffer.TryRecord(now, playerPoses, replayObjects);
        }

        public bool TryCaptureCurrentHighlightReplay(out HighlightReplayClip[] clips)
        {
            if (!TryGetCurrentHighlight(out var highlight))
            {
                clips = Array.Empty<HighlightReplayClip>();
                return false;
            }

            clips = new HighlightReplayClip[highlight.Segments.Count];
            for (var index = 0; index < highlight.Segments.Count; index++)
            {
                var segment = highlight.Segments[index];
                clips[index] = new HighlightReplayClip(
                    segment,
                    highlightReplayBuffer.Capture(segment.StartedAt, segment.EndedAt));
            }

            return true;
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

        private void RaiseFinalWarningIfNeeded(double now)
        {
            if (finalWarningStarted || !flow.IsFinalPeriod(now))
            {
                return;
            }

            finalWarningStarted = true;
            var endsAt = state.PhaseEndsAt.CurrentValue;
            FinalWarningStarted?.Invoke(
                new FinalWarningStartedEvent(
                    endsAt - rules.FinalWarningSeconds,
                    endsAt));
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

        private bool ReleaseHeldObjectAt(int playerIndex, Pose pose)
        {
            var heldItemOwner = outcome.GetHeldItemOwner(playerIndex);
            if (heldItemOwner >= 0)
            {
                outcome.ReleaseHeldItem(playerIndex);
                placements.RecordPlacement(heldItemOwner, pose);
                return true;
            }

            var objectId = heldMapObjectIdsByPlayer[playerIndex];
            if (objectId == null)
            {
                return false;
            }

            heldMapObjectIdsByPlayer[playerIndex] = null;
            mapObjectHolderById.Remove(objectId);
            worldObjects.TrySetPose(objectId, pose);
            return true;
        }

        private static void ValidateUniqueObjectIds(
            IReadOnlyList<PlayerItemAssignment> assignments,
            IReadOnlyList<WorldObjectState> worldObjectStates)
        {
            var objectIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var assignment in assignments)
            {
                objectIds.Add(assignment.Item.ItemId);
            }

            foreach (var worldObjectState in worldObjectStates)
            {
                if (!objectIds.Add(worldObjectState.ObjectId))
                {
                    throw new ArgumentException(
                        $"Object id must be unique across player and map objects: " +
                        worldObjectState.ObjectId,
                        nameof(worldObjectStates));
                }
            }
        }

        private static Pose[] ValidateSpawnPoints(IReadOnlyList<Pose> spawnPoints)
        {
            if (spawnPoints == null)
            {
                throw new ArgumentNullException(nameof(spawnPoints));
            }

            if (spawnPoints.Count < MatchRulesSO.PlayerCount)
            {
                throw new ArgumentException(
                    $"At least {MatchRulesSO.PlayerCount} spawn points are required.",
                    nameof(spawnPoints));
            }

            var uniquePositions = new HashSet<Vector3>();
            var validatedSpawnPoints = new Pose[spawnPoints.Count];
            for (var index = 0; index < spawnPoints.Count; index++)
            {
                var spawnPoint = spawnPoints[index];
                if (!uniquePositions.Add(spawnPoint.position))
                {
                    throw new ArgumentException(
                        $"Spawn point positions must be unique: {spawnPoint.position}",
                        nameof(spawnPoints));
                }

                validatedSpawnPoints[index] = spawnPoint;
            }

            return validatedSpawnPoints;
        }

        private static Pose[] SelectSpawnPoses(Pose[] spawnPoints, System.Random random)
        {
            var candidates = (Pose[])spawnPoints.Clone();
            for (var index = 0; index < MatchRulesSO.PlayerCount; index++)
            {
                var selectedIndex = random.Next(index, candidates.Length);
                (candidates[index], candidates[selectedIndex]) =
                    (candidates[selectedIndex], candidates[index]);
            }

            var selectedSpawnPoses = new Pose[MatchRulesSO.PlayerCount];
            Array.Copy(candidates, selectedSpawnPoses, MatchRulesSO.PlayerCount);
            return selectedSpawnPoses;
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
            if (result.HasValue)
            {
                return;
            }

            var capturedResult = new MatchResult(
                endReason,
                endedAt,
                outcome.GetWinnerPlayerIndices());
            if (!hasExplicitHighlightCandidates)
            {
                highlights = new HighlightSequence(
                    highlightRecorder.CaptureCandidates(endedAt),
                    rules);
            }
            result = capturedResult;
            MatchEnded?.Invoke(capturedResult);
        }

        private void StartHighlightRecordingIfNeeded(double now)
        {
            switch (state.CurrentPhase.CurrentValue)
            {
                case MatchPhase.Hiding when now >= state.PhaseEndsAt.CurrentValue:
                    highlightRecorder.StartSearching(state.PhaseEndsAt.CurrentValue);
                    break;
                case MatchPhase.Searching:
                    highlightRecorder.StartSearching(
                        state.PhaseEndsAt.CurrentValue - rules.SearchingDurationSeconds);
                    break;
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
