using System;
using System.Collections.Generic;
using Game.Core.Items;
using Game.Core.Lobby;
using Game.Core.Match;
using Game.Core.Players;
using Game.Server.Items;
using Game.Server.Players;
using Game.SOAP.Config;
using UnityEngine;

namespace Game.Server.Match
{
    public enum MatchEndReason
    {
        TimeExpired,
        AllPlayerItemsDestroyed,
        LastPlayerStanding
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

    public readonly struct ObjectThrownEvent
    {
        public ObjectThrownEvent(
            int playerIndex,
            string objectId,
            Pose releasePose,
            Vector3 initialVelocity,
            double thrownAt)
        {
            PlayerIndex = playerIndex;
            ObjectId = objectId ?? throw new ArgumentNullException(nameof(objectId));
            ReleasePose = releasePose;
            InitialVelocity = initialVelocity;
            ThrownAt = thrownAt;
        }

        public int PlayerIndex { get; }
        public string ObjectId { get; }
        public Pose ReleasePose { get; }
        public Vector3 InitialVelocity { get; }
        public double ThrownAt { get; }
    }

    public readonly struct ObjectAutoReleasedEvent
    {
        public ObjectAutoReleasedEvent(string objectId, Pose pose)
        {
            ObjectId = objectId ?? throw new ArgumentNullException(nameof(objectId));
            Pose = pose;
        }

        public string ObjectId { get; }
        public Pose Pose { get; }
    }

    public readonly struct MapObjectEjectedEvent
    {
        public MapObjectEjectedEvent(string objectId, Pose pose)
        {
            ObjectId = objectId ?? throw new ArgumentNullException(nameof(objectId));
            Pose = pose;
        }

        public string ObjectId { get; }
        public Pose Pose { get; }
    }

    public sealed partial class MatchSessionCoordinator
    {
        private const double MapObjectEjectionDelaySeconds = 0.5d;
        private const double HighlightReplaySampleIntervalSeconds = 0.1d;
        private const double HighlightRecordingDelaySeconds = 1d;
        public const double HighlightPostRollSeconds = HighlightPresentationTiming.PostRollSeconds;
        private readonly Dictionary<int, double> lastHitAt = new();
        private readonly int[] totalHitsReceived, totalStuns;
        private readonly Dictionary<int, double> lastPlacementAt = new();
        private readonly Dictionary<int, double> lastThrowAt = new();

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
        private readonly bool[] completedHidingTurns;
        private readonly Dictionary<string, PendingMapObjectEjection> pendingMapObjectEjections =
            new(StringComparer.Ordinal);
        private readonly List<string> completedMapObjectEjections = new();
        private readonly string[] heldMapObjectIdsByPlayer;
        private readonly Dictionary<string, int> mapObjectHolderById =
            new(StringComparer.Ordinal);
        private HighlightSequence highlights;
        private MatchResult? result;
        private bool finalWarningStarted;
        private bool hasExplicitHighlightCandidates;
        private bool replayUnavailable;

        public MatchSessionCoordinator(
            MatchRulesSO rules,
            MatchState state,
            MatchFlow flow,
            PlayerInteractionSystem interactions,
            IReadOnlyList<string> participantIds,
            IPlacementValidator placementValidator,
            IReadOnlyList<Pose> spawnPoints,
            IReadOnlyList<ItemDefinition> itemDefinitions,
            System.Random random,
            IReadOnlyList<WorldObjectState> initialWorldObjects = null,
            IReadOnlyList<PlayerItemAssignment> specifiedAssignments = null,
            MatchRuleSettings? matchRules = null,
            IReadOnlyList<Pose> waitingSpawnPoints = null)
        {
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.flow = flow ?? throw new ArgumentNullException(nameof(flow));
            this.interactions = interactions ??
                throw new ArgumentNullException(nameof(interactions));
            Players = new MatchPlayerRoster(participantIds);
            var playerCount = Players.Players.Count;
            if (flow.PlayerCount != playerCount || interactions.PlayerCount != playerCount)
            {
                throw new ArgumentException(
                    "Match systems and participant count must match.",
                    nameof(participantIds));
            }

            totalHitsReceived = new int[playerCount];
            totalStuns = new int[playerCount];
            completedHidingTurns = new bool[playerCount];
            heldMapObjectIdsByPlayer = new string[playerCount];
            this.placementValidator = placementValidator ??
                throw new ArgumentNullException(nameof(placementValidator));

            var assignments = specifiedAssignments ?? ItemAssignmentSystem.Assign(
                itemDefinitions,
                playerCount,
                random,
                matchRules?.CategoryId);
            Assignments = assignments;
            var validatedSpawnPoints = ValidateSpawnPoints(spawnPoints, playerCount);
            hidingSpawnPoses = SelectSpawnPoses(validatedSpawnPoints, playerCount, random);
            // 탐색 시작 위치는 다시 섞되, 각 플레이어가 숨기던 자리·대기하던 자리와는 겹치지 않게 한다.
            // 그렇지 않으면 "시작했는데 그 자리 그대로"로 보인다.
            searchingSpawnPoses = SelectSearchingSpawnPoses(
                validatedSpawnPoints, playerCount, random, hidingSpawnPoses, waitingSpawnPoints);
            placements = new ItemPlacementSystem(assignments);
            var worldObjectStates = initialWorldObjects ?? Array.Empty<WorldObjectState>();
            worldObjects = new WorldObjectStateSystem(worldObjectStates);
            outcome = new MatchOutcomeSystem(assignments);
            highlightRecorder = new HighlightEventRecorder(rules, assignments);
            highlightReplayBuffer = new HighlightReplayBuffer(
                HighlightReplaySampleIntervalSeconds,
                flow.SearchingDurationSeconds + HighlightPostRollSeconds + 1d);
            ValidateUniqueObjectIds(assignments, worldObjectStates);
            highlights = new HighlightSequence(Array.Empty<HighlightCandidate>(), rules);
        }

        public IReadOnlyList<PlayerItemAssignment> Assignments { get; }
        public MatchPlayerRoster Players { get; }
        public MatchPhase CurrentPhase => state.CurrentPhase.CurrentValue;
        public bool AllItemsPlaced => placements.AllPlaced;
        public int DestroyedPlayerItemCount => outcome.DestroyedItemCount;
        public bool AllPlayerItemsDestroyed => outcome.AllPlayerItemsDestroyed;

        public event Action<PlayerItemDestroyedEvent> PlayerItemDestroyed;
        public event Action<FinalWarningStartedEvent> FinalWarningStarted;
        public event Action<PlayerStunnedEvent> PlayerStunned;
        public event Action<ObjectThrownEvent> ObjectThrown;
        public event Action<ObjectAutoReleasedEvent> ObjectAutoReleased;
        public event Action<MapObjectEjectedEvent> MapObjectEjected;
        public event Action<MatchResult> MatchEnded;

        public MatchStateSnapshot CaptureStateSnapshot()
        {
            return state.CaptureSnapshot();
        }

        public bool Start(double now)
        {
            if (!flow.Start(now)) return false;
            return true;
        }

        public double GetRemainingSeconds(double now)
        {
            return flow.GetRemainingSeconds(now);
        }

        private readonly HashSet<int> introReadyPlayers = new();
        private MatchPhase readyPhase;
        public bool IsWaitingForIntroReady => flow.IsWaitingForIntroReady;
        public void EnablePhaseIntros(bool waitForReady = false) => flow.EnablePhaseIntros(waitForReady);

        public bool ConfirmPhaseIntroReady(int playerIndex, MatchPhase phase)
        {
            if (!IsWaitingForIntroReady || phase != CurrentPhase || playerIndex < 0 ||
                playerIndex >= Players.Players.Count || !Players.IsActive(playerIndex)) return false;
            if (readyPhase != phase) { introReadyPlayers.Clear(); readyPhase = phase; }
            return introReadyPlayers.Add(playerIndex);
        }

        public bool TryStartPhaseIntro(double now)
        {
            if (!IsWaitingForIntroReady || readyPhase != CurrentPhase) return false;
            for (var i = 0; i < Players.Players.Count; i++)
                if (Players.IsActive(i) && !introReadyPlayers.Contains(i)) return false;
            return Players.ActivePlayerCount > 0 && flow.SchedulePhaseIntro(now);
        }
        public bool IsPhaseIntro(double now) => flow.IsPhaseIntro(now);

        public int GetCurrentHidingTurnIndex(double now)
        {
            return flow.GetCurrentHidingTurnIndex(now);
        }

        public bool IsFinalPeriod(double now)
        {
            return flow.IsFinalPeriod(now);
        }

        // Prepare the first hider while time is stopped, without opening gameplay actions.
        public bool TryGetIntroHidingSpawnPose(double now, out Pose spawnPose)
        {
            spawnPose = default;
            if (CurrentPhase != MatchPhase.Hiding || !flow.IsPhaseIntro(now) ||
                !Players.IsActive(0)) return false;
            spawnPose = hidingSpawnPoses[0];
            return true;
        }

        public bool TryGetCurrentHidingSpawnPose(
            int playerIndex,
            double now,
            out Pose spawnPose)
        {
            if (!Players.IsActive(playerIndex) ||
                flow.GetCurrentHidingTurnIndex(now) != playerIndex)
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

            if (IsWaitingForIntroReady) return false;

            if (lastKnownPlayerPositions == null ||
                lastKnownPlayerPositions.Count != Players.Players.Count)
            {
                throw new ArgumentException(
                    $"Exactly {Players.Players.Count} player positions are required.",
                    nameof(lastKnownPlayerPositions));
            }

            if (!replayUnavailable) StartHighlightRecordingIfNeeded(now);
            if (TryGetExpiredSearchingEnd(now, out var searchingEndedAt))
            {
                CaptureResult(MatchEndReason.TimeExpired, searchingEndedAt);
            }

            if (state.CurrentPhase.CurrentValue == MatchPhase.Hiding)
            {
                CompleteExpiredHidingTurns(now, lastKnownPlayerPositions);
                SkipDepartedHidingTurns(now);
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
            if (!CanActDuringHidingTurn(playerIndex, now) ||
                !placementValidator.IsValid(Assignments[playerIndex].Item.ItemId, pose))
            {
                return false;
            }

            if (outcome.GetHeldItemOwner(playerIndex) == playerIndex)
            {
                outcome.ReleaseHeldItem(playerIndex);
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
            if (!CanActDuringHidingTurn(playerIndex, now))
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

        public bool TryGetObjectPose(string objectId, out Pose pose)
        {
            if (!string.IsNullOrWhiteSpace(objectId))
            {
                var normalizedId = objectId.Trim();
                for (var playerIndex = 0;
                     playerIndex < Assignments.Count;
                     playerIndex++)
                {
                    if (string.Equals(
                            Assignments[playerIndex].Item.ItemId,
                            normalizedId,
                            StringComparison.Ordinal) &&
                        placements.TryGetPlacement(playerIndex, out var placement))
                    {
                        pose = placement.Pose;
                        return true;
                    }
                }

                if (worldObjects.TryGetState(normalizedId, out var worldObject))
                {
                    pose = worldObject.Pose;
                    return true;
                }
            }

            pose = default;
            return false;
        }

        public bool TryConfirmReleasedObjectPose(string objectId, Pose pose)
        {
            if (string.IsNullOrWhiteSpace(objectId) ||
                !IsFinite(pose.position) ||
                !IsFinite(pose.rotation))
            {
                return false;
            }

            var normalizedId = objectId.Trim();
            for (var playerIndex = 0;
                 playerIndex < Assignments.Count;
                 playerIndex++)
            {
                if (!string.Equals(
                        Assignments[playerIndex].Item.ItemId,
                        normalizedId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (!placements.TryGetPlacement(playerIndex, out var placement))
                {
                    return false;
                }

                placements.RecordPlacement(
                    playerIndex,
                    pose,
                    placement.WasAutoPlaced);
                return true;
            }

            return worldObjects.TrySetPose(normalizedId, pose);
        }

        public WorldObjectState[] CaptureWorldObjectSnapshot()
        {
            return worldObjects.CaptureSnapshot();
        }

        public bool TryHoldObject(int playerIndex, string objectId, double now)
        {
            var isSearching = CanInteract(playerIndex, now);
            if ((!CanActDuringHidingTurn(playerIndex, now) && !isSearching) ||
                outcome.GetHeldItemOwner(playerIndex) >= 0 ||
                heldMapObjectIdsByPlayer[playerIndex] != null)
            {
                return false;
            }

            if (outcome.TryHoldItem(playerIndex, objectId))
            {
                highlightRecorder.RecordItemPickup(playerIndex, objectId, now);

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

        public bool TryInitializeAssignedItem(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= Assignments.Count)
            {
                return false;
            }

            var itemId = Assignments[playerIndex].Item.ItemId;
            if (!outcome.TryHoldItem(playerIndex, itemId)) return false;
            if (CurrentPhase == MatchPhase.Hiding)
            {
                var turnStartedAt = state.PhaseEndsAt.CurrentValue - flow.HidingDurationSeconds +
                    playerIndex * flow.HidingTurnDurationSeconds;
                highlightRecorder.RecordItemPickup(playerIndex, itemId, Math.Max(0d, turnStartedAt));
            }
            return true;
        }

        public bool TryReleaseHeldObject(int playerIndex, Pose pose, double now)
        {
            var isHiding = CanActDuringHidingTurn(playerIndex, now);
            if (!CanPlaceHeldObject(playerIndex, pose, now) ||
                !TryGetHeldObjectId(playerIndex, out var objectId) ||
                !ReleaseHeldObjectAt(playerIndex, pose))
            {
                return false;
            }

            if (!isHiding)
            {
                highlightRecorder.RecordItemInteraction(playerIndex, objectId, now);
            }

            lastPlacementAt[playerIndex] = now;

            return true;
        }

        public bool TryDropHeldObject(int playerIndex, Pose pose, double now)
        {
            var isSearching = CanInteract(playerIndex, now);
            if ((!CanActDuringHidingTurn(playerIndex, now) && !isSearching) ||
                !IsFinite(pose.position) ||
                !IsFinite(pose.rotation) ||
                !TryGetHeldObjectId(playerIndex, out var objectId) ||
                !ReleaseHeldObjectAt(playerIndex, pose))
            {
                return false;
            }

            if (isSearching)
            {
                highlightRecorder.RecordItemInteraction(playerIndex, objectId, now);
            }

            return true;
        }

        public bool CanPlaceHeldObject(int playerIndex, Pose pose, double now)
        {
            return (CanActDuringHidingTurn(playerIndex, now) ||
                    CanInteract(playerIndex, now)) &&
                   TryGetHeldObjectId(playerIndex, out var objectId) &&
                   placementValidator.IsValid(objectId, pose);
        }

        public bool TryThrowHeldObject(
            int playerIndex,
            Pose releasePose,
            Vector3 initialVelocity,
            double now)
        {
            var isSearching = CanInteract(playerIndex, now);
            if ((!CanActDuringHidingTurn(playerIndex, now) && !isSearching) ||
                !IsFinite(releasePose.position) ||
                !IsFinite(releasePose.rotation) ||
                !IsFinite(initialVelocity) ||
                initialVelocity.sqrMagnitude <= 0f ||
                !TryGetHeldObjectId(playerIndex, out var objectId) ||
                !ReleaseHeldObjectAt(playerIndex, releasePose))
            {
                return false;
            }

            if (isSearching)
            {
                highlightRecorder.RecordItemInteraction(playerIndex, objectId, now);
            }

            ObjectThrown?.Invoke(
                new ObjectThrownEvent(
                    playerIndex,
                    objectId,
                    releasePose,
                    initialVelocity,
                    now));
            lastThrowAt[playerIndex] = now;
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
            if (!CanFight(attackerPlayerIndex, now) ||
                !Players.IsActive(targetPlayerIndex) ||
                attackerPlayerIndex == targetPlayerIndex)
            {
                return HitResult.Ignored;
            }

            // Hiding waiters may punch exactly as they do in the lobby, but
            // lobby punches do not build stun stacks or disable anyone.
            if (state.CurrentPhase.CurrentValue == MatchPhase.Hiding)
            {
                totalHitsReceived[targetPlayerIndex]++;
                return HitResult.Registered;
            }

            var hitResult = interactions.RegisterHit(targetPlayerIndex, now);
            if (hitResult != HitResult.Ignored)
            {
                lastHitAt[attackerPlayerIndex] = now;
                totalHitsReceived[targetPlayerIndex]++;
            }
            if (hitResult == HitResult.Stunned) totalStuns[targetPlayerIndex]++;
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

            highlightRecorder.RecordPlayerStunned(attackerPlayerIndex, targetPlayerIndex, now);
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

        // Match totals are independent of the stun stack, which resets after each stun.
        public (int HitsReceived, int Stuns) GetCombatTotals(int playerIndex) =>
            playerIndex >= 0 && playerIndex < totalHitsReceived.Length
                ? (totalHitsReceived[playerIndex], totalStuns[playerIndex]) : default;

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
            var candidates = outcome.GetWinnerPlayerIndices();
            var winners = new List<int>(candidates.Length);
            foreach (var playerIndex in candidates)
            {
                if (Players.IsActive(playerIndex))
                {
                    winners.Add(playerIndex);
                }
            }

            return winners.ToArray();
        }

        public PlayerItemStatusSnapshot[] CapturePlayerItemStatuses()
        {
            return outcome.CapturePlayerItemStatuses();
        }

        public bool TryHandlePlayerLeft(int playerIndex, Pose lastKnownPose, double now)
        {
            flow.GetRemainingSeconds(now);
            var phase = state.CurrentPhase.CurrentValue;
            // The match outcome and recorded replay are final. Only membership
            // changes when somebody leaves during presentation.
            if (phase == MatchPhase.Highlight || phase == MatchPhase.Result)
                return Players.TryDeactivate(playerIndex);
            if (phase != MatchPhase.Hiding && phase != MatchPhase.Searching)
            {
                return false;
            }

            if (!Players.TryDeactivate(playerIndex))
            {
                return false;
            }

            if (phase == MatchPhase.Hiding && !completedHidingTurns[playerIndex])
            {
                CompleteHidingTurn(playerIndex, lastKnownPose.position);
            }
            else
            {
                ReleaseHeldObjectAt(playerIndex, lastKnownPose, true);
            }
            SkipDepartedHidingTurns(now);

            return true;
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
            IReadOnlyList<WorldObjectState> replayObjects,
            IReadOnlyList<HighlightPlayerAction> playerActions = null)
        {
            if (replayUnavailable) return false;
            var phase = state.CurrentPhase.CurrentValue;
            var searchingStartedAt = state.PhaseEndsAt.CurrentValue - flow.SearchingDurationSeconds;
            var canRecordSearching = phase == MatchPhase.Searching && state.PhaseEndsAt.CurrentValue > 0d &&
                                     now >= searchingStartedAt + HighlightRecordingDelaySeconds;
            var canRecordPostRoll = phase == MatchPhase.Highlight && result.HasValue &&
                                    now <= result.Value.EndedAt + HighlightPostRollSeconds +
                                    HighlightReplaySampleIntervalSeconds;
            if (!canRecordSearching && !canRecordPostRoll)
            {
                return false;
            }

            if (playerPoses == null || playerPoses.Count != Players.Players.Count)
            {
                throw new ArgumentException(
                    $"Exactly {Players.Players.Count} player poses are required.",
                    nameof(playerPoses));
            }

            if (playerActions != null && playerActions.Count != playerPoses.Count)
            {
                throw new ArgumentException(
                    "Replay actions must match player poses.",
                    nameof(playerActions));
            }

            var actions = new HighlightPlayerAction[playerPoses.Count];
            for (var i = 0; i < actions.Length; i++)
            {
                var action = playerActions == null
                    ? HighlightPlayerAction.None
                    : playerActions[i];
                if (interactions.IsStunned(i, now)) action |= HighlightPlayerAction.Stunned;
                if (lastHitAt.TryGetValue(i, out var hitAt) && now - hitAt < 0.5d)
                    action |= HighlightPlayerAction.Punching;
                if (TryGetHeldObjectId(i, out _)) action |= HighlightPlayerAction.Carrying;
                if (lastThrowAt.TryGetValue(i, out var thrownAt) && now - thrownAt < 0.5d)
                    action |= HighlightPlayerAction.Throwing;
                if (lastPlacementAt.TryGetValue(i, out var placedAt) && now - placedAt < 0.5d)
                    action |= HighlightPlayerAction.Placing;
                actions[i] = action;
            }
            return highlightReplayBuffer.TryRecord(now, playerPoses, replayObjects, actions);
        }

        public bool TryCaptureCurrentHighlightReplay(out HighlightReplayClip[] clips)
        {
            if (!TryGetCurrentHighlight(out var highlight))
            {
                clips = Array.Empty<HighlightReplayClip>();
                return false;
            }

            clips = CaptureReplay(highlight);
            return true;
        }

        public bool TryCaptureHighlightReplay(out HighlightReplayData[] replay)
        {
            if (!result.HasValue)
            {
                replay = Array.Empty<HighlightReplayData>();
                return false;
            }

            var selected = highlights.Capture();
            var playableCandidates = new List<HighlightCandidate>(selected.Length);
            var playableReplay = new List<HighlightReplayData>(selected.Length);
            for (var index = 0; index < selected.Length; index++)
            {
                var clips = CaptureReplay(selected[index]);
                if (!HasPlayableFrames(clips)) continue;
                playableCandidates.Add(selected[index]);
                playableReplay.Add(new HighlightReplayData(selected[index], clips));
            }

            // The shared schedule must describe the payload clients can actually play.
            // Otherwise an empty clip still consumes highlight time behind a black cover.
            highlights = new HighlightSequence(playableCandidates, rules);
            replay = playableReplay.ToArray();
            return true;
        }

        public bool WaitForHighlightPlayback()
        {
            if (CurrentPhase != MatchPhase.Highlight) return false;
            state.EnterPhase(MatchPhase.Highlight, 0d);
            return true;
        }

        public bool ScheduleHighlightPlayback(double startsAt)
        {
            if (CurrentPhase != MatchPhase.Highlight) return false;
            if (!double.IsFinite(startsAt) || startsAt < 0d)
                throw new ArgumentOutOfRangeException(nameof(startsAt));
            state.EnterPhase(
                MatchPhase.Highlight,
                startsAt + highlights.ScheduledDurationSeconds);
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

        private HighlightReplayClip[] CaptureReplay(HighlightCandidate highlight)
        {
            var clips = new HighlightReplayClip[highlight.Segments.Count];
            for (var index = 0; index < highlight.Segments.Count; index++)
            {
                var segment = highlight.Segments[index];
                clips[index] = new HighlightReplayClip(
                    segment,
                    CaptureReplayFrames(segment));
            }

            return clips;
        }

        private HighlightReplayFrame[] CaptureReplayFrames(HighlightSegment segment)
        {
            var captured = highlightReplayBuffer.CaptureWithBoundary(
                segment.StartedAt,
                segment.EndedAt);
            var maxFrameCount = Math.Max(
                2,
                (int)Math.Ceiling(
                    segment.PlaybackDurationSeconds /
                    HighlightReplaySampleIntervalSeconds) + 1);
            if (captured.Length <= maxFrameCount)
            {
                return captured;
            }

            var sampled = new HighlightReplayFrame[maxFrameCount];
            for (var index = 0; index < sampled.Length; index++)
            {
                var sourceIndex = index * (captured.Length - 1) /
                                  (sampled.Length - 1);
                sampled[index] = captured[sourceIndex];
            }

            return sampled;
        }

        private static bool HasPlayableFrames(IReadOnlyList<HighlightReplayClip> clips)
        {
            if (clips.Count == 0) return false;
            for (var index = 0; index < clips.Count; index++)
                if (clips[index].Frames.Count == 0)
                    return false;
            return true;
        }

        private bool IsSearchingAt(double now)
        {
            return !flow.IsPhaseIntro(now) && state.CurrentPhase.CurrentValue == MatchPhase.Searching &&
                   flow.GetRemainingSeconds(now) > 0d;
        }

        private bool CanInteract(int playerIndex, double now)
        {
            return Players.IsActive(playerIndex) &&
                   IsSearchingAt(now) &&
                   !interactions.IsStunned(playerIndex, now);
        }

        private bool CanFight(int playerIndex, double now)
        {
            var phase = state.CurrentPhase.CurrentValue;
            return !flow.IsPhaseIntro(now) && Players.IsActive(playerIndex) &&
                   (phase == MatchPhase.Hiding &&
                    flow.GetRemainingSeconds(now) > 0d ||
                    IsSearchingAt(now)) &&
                   !interactions.IsStunned(playerIndex, now);
        }

        private bool CanActDuringHidingTurn(int playerIndex, double now)
        {
            return Players.IsActive(playerIndex) &&
                   state.CurrentPhase.CurrentValue == MatchPhase.Hiding &&
                   flow.GetCurrentHidingTurnIndex(now) == playerIndex &&
                   flow.GetHidingTurnRemainingSeconds(now) > 0d &&
                   !completedHidingTurns[playerIndex];
        }

        public bool TryCompleteHidingTurn(int playerIndex, double now)
        {
            if (playerIndex < 0 || playerIndex >= Assignments.Count ||
                !CanActDuringHidingTurn(playerIndex, now) ||
                outcome.GetHeldItemOwner(playerIndex) == playerIndex ||
                !placements.TryGetPlacement(playerIndex, out var placement)) return false;
            // Placement was already accepted by authority. A subsequent physics
            // update (settling, rolling or bouncing) must not revoke completion.
            CompleteHidingTurn(playerIndex, placement.Pose.position);
            return flow.SkipCurrentHidingTurn(now);
        }

        private void CompleteHidingTurn(int playerIndex, Vector3 lastPlayerPosition)
        {
            var pose = new Pose(lastPlayerPosition, Quaternion.identity);
            ReleaseHeldObjectAt(playerIndex, pose, true);

            var wasPlaced = placements.TryGetPlacement(playerIndex, out _);
            var placement = placements.CompleteTurn(playerIndex, lastPlayerPosition);
            if (!wasPlaced)
            {
                ObjectAutoReleased?.Invoke(
                    new ObjectAutoReleasedEvent(placement.ItemId, placement.Pose));
            }

            completedHidingTurns[playerIndex] = true;
        }

        private bool ReleaseHeldObjectAt(
            int playerIndex,
            Pose pose,
            bool wasAutoPlaced = false)
        {
            var heldItemOwner = outcome.GetHeldItemOwner(playerIndex);
            if (heldItemOwner >= 0)
            {
                var heldObjectId = Assignments[heldItemOwner].Item.ItemId;
                outcome.ReleaseHeldItem(playerIndex);
                placements.RecordPlacement(heldItemOwner, pose, wasAutoPlaced);
                if (wasAutoPlaced)
                {
                    ObjectAutoReleased?.Invoke(
                        new ObjectAutoReleasedEvent(heldObjectId, pose));
                }

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
            if (wasAutoPlaced)
            {
                ObjectAutoReleased?.Invoke(
                    new ObjectAutoReleasedEvent(objectId, pose));
            }

            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z) &&
                   float.IsFinite(value.w);
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

        private static Pose[] ValidateSpawnPoints(
            IReadOnlyList<Pose> spawnPoints,
            int playerCount)
        {
            if (spawnPoints == null)
            {
                throw new ArgumentNullException(nameof(spawnPoints));
            }

            if (spawnPoints.Count < playerCount)
            {
                throw new ArgumentException(
                    $"At least {playerCount} spawn points are required.",
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

        private static Pose[] SelectSpawnPoses(
            Pose[] spawnPoints,
            int playerCount,
            System.Random random)
        {
            var candidates = (Pose[])spawnPoints.Clone();
            for (var index = 0; index < playerCount; index++)
            {
                var selectedIndex = random.Next(index, candidates.Length);
                (candidates[index], candidates[selectedIndex]) =
                    (candidates[selectedIndex], candidates[index]);
            }

            var selectedSpawnPoses = new Pose[playerCount];
            Array.Copy(candidates, selectedSpawnPoses, playerCount);
            return selectedSpawnPoses;
        }

        /// <summary>
        /// 탐색 시작 위치: 스폰 지점을 무작위로 섞은 뒤 플레이어마다 "피할 자리"(숨기기 스폰, 대기 스폰)와
        /// 다른 지점을 고른다. 마지막 플레이어에게 피할 자리만 남으면 앞 플레이어와 자리를 바꿔 해결하고,
        /// 지점이 모자라 어쩔 수 없을 때만 겹침을 허용한다.
        /// </summary>
        internal static Pose[] SelectSearchingSpawnPoses(
            Pose[] spawnPoints,
            int playerCount,
            System.Random random,
            IReadOnlyList<Pose> hidingPoses,
            IReadOnlyList<Pose> waitingPoses)
        {
            var order = new int[spawnPoints.Length];
            for (var index = 0; index < order.Length; index++)
            {
                order[index] = index;
            }

            for (var index = 0; index < order.Length; index++)
            {
                var swap = random.Next(index, order.Length);
                (order[index], order[swap]) = (order[swap], order[index]);
            }

            var used = new bool[spawnPoints.Length];
            var selected = new Pose[playerCount];
            for (var player = 0; player < playerCount; player++)
            {
                var chosen = -1;
                var fallback = -1;
                foreach (var candidate in order)
                {
                    if (used[candidate])
                    {
                        continue;
                    }

                    if (fallback < 0)
                    {
                        fallback = candidate;
                    }

                    if (!MustAvoid(spawnPoints[candidate], player, hidingPoses, waitingPoses))
                    {
                        chosen = candidate;
                        break;
                    }
                }

                if (chosen >= 0)
                {
                    used[chosen] = true;
                    selected[player] = spawnPoints[chosen];
                    continue;
                }

                // 남은 지점이 전부 피할 자리: 앞 플레이어와 맞바꿔 둘 다 조건을 지키는 조합을 찾는다.
                // 그런 조합이 없으면(지점이 모자람) 겹침을 허용한다.
                var pose = spawnPoints[fallback];
                for (var other = 0; other < player; other++)
                {
                    if (!MustAvoid(selected[other], player, hidingPoses, waitingPoses) &&
                        !MustAvoid(pose, other, hidingPoses, waitingPoses))
                    {
                        (selected[other], pose) = (pose, selected[other]);
                        break;
                    }
                }

                used[fallback] = true;
                selected[player] = pose;
            }

            return selected;
        }

        private static bool MustAvoid(
            Pose candidate,
            int player,
            IReadOnlyList<Pose> hidingPoses,
            IReadOnlyList<Pose> waitingPoses)
        {
            const float sameSpotDistanceSquared = 0.01f * 0.01f;
            return (hidingPoses != null && player < hidingPoses.Count &&
                    (hidingPoses[player].position - candidate.position).sqrMagnitude < sameSpotDistanceSquared) ||
                   (waitingPoses != null && player < waitingPoses.Count &&
                    (waitingPoses[player].position - candidate.position).sqrMagnitude < sameSpotDistanceSquared);
        }

        private bool TryGetExpiredSearchingEnd(double now, out double searchingEndedAt)
        {
            switch (state.CurrentPhase.CurrentValue)
            {
                case MatchPhase.Hiding:
                    if (flow.WaitsForIntroReady) { searchingEndedAt = 0d; return false; }
                    searchingEndedAt =
                        state.PhaseEndsAt.CurrentValue + flow.SearchingDurationSeconds;
                    return now >= searchingEndedAt;
                case MatchPhase.Searching:
                    searchingEndedAt = state.PhaseEndsAt.CurrentValue;
                    return now >= searchingEndedAt;
                default:
                    searchingEndedAt = 0d;
                    return false;
            }
        }

        private void CaptureResult(
            MatchEndReason endReason,
            double endedAt,
            int[] winnerPlayerIndices = null,
            bool captureHighlights = true)
        {
            if (result.HasValue)
            {
                return;
            }

            var capturedResult = new MatchResult(
                endReason,
                endedAt,
                winnerPlayerIndices ?? GetWinnerPlayerIndices());
            if (captureHighlights && !hasExplicitHighlightCandidates)
            {
                highlights = new HighlightSequence(
                    highlightRecorder.CaptureCandidates(endedAt, endReason,
                        highlightReplayBuffer.Capture(0d, endedAt)),
                    rules);
            }
            result = capturedResult;
            flow.SetHighlightPresentationDuration(highlights.TotalDurationSeconds +
                highlights.Count * HighlightPresentationTiming.OverheadSeconds +
                HighlightPresentationTiming.PostRollSeconds + HighlightPresentationTiming.DeliveryGraceSeconds);
            MatchEnded?.Invoke(capturedResult);
        }

        private void SkipDepartedHidingTurns(double now)
        {
            // Bounded by the frozen line-up, including consecutive departed players.
            for (var skipped = 0; skipped < Players.Players.Count; skipped++)
            {
                var turn = flow.GetCurrentHidingTurnIndex(now);
                if (turn < 0 || Players.IsActive(turn) || !flow.SkipCurrentHidingTurn(now)) return;
            }
        }

        private void StartHighlightRecordingIfNeeded(double now)
        {
            double searchingStartedAt;
            switch (state.CurrentPhase.CurrentValue)
            {
                case MatchPhase.Hiding when !flow.WaitsForIntroReady && now >= state.PhaseEndsAt.CurrentValue:
                    searchingStartedAt = state.PhaseEndsAt.CurrentValue + flow.PhaseIntroDurationSeconds;
                    break;
                case MatchPhase.Searching when state.PhaseEndsAt.CurrentValue > 0d:
                    searchingStartedAt = state.PhaseEndsAt.CurrentValue - flow.SearchingDurationSeconds;
                    break;
                default:
                    return;
            }

            highlightRecorder.StartSearching(searchingStartedAt);
            highlightRecorder.StartRecording(searchingStartedAt + HighlightRecordingDelaySeconds);
        }

        private void CompleteExpiredHidingTurns(
            double now,
            IReadOnlyList<Vector3> lastKnownPlayerPositions)
        {
            var hidingStartedAt =
                state.PhaseEndsAt.CurrentValue - flow.HidingDurationSeconds;
            var elapsedSeconds = Math.Max(0d, now - hidingStartedAt);
            var expiredTurnCount = Math.Min(
                Players.Players.Count,
                (int)(elapsedSeconds / flow.HidingTurnDurationSeconds));

            for (var playerIndex = 0; playerIndex < expiredTurnCount; playerIndex++)
            {
                if (completedHidingTurns[playerIndex])
                {
                    continue;
                }

                CompleteHidingTurn(playerIndex, lastKnownPlayerPositions[playerIndex]);
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
                MapObjectEjected?.Invoke(
                    new MapObjectEjectedEvent(pair.Key, pair.Value.Pose));
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
