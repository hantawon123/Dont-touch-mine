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
        /// <summary>맞은 쪽이 움찔하는 시간(초). 실제 플레이의 <c>PlayerAnimationDriver.HitSeconds</c> 와 같다.</summary>
        private const double HitReactionSeconds = 1d;

        /// <summary>
        /// 기절에 들어가 쓰러지는 시간(초). 실제 플레이의 <c>PlayerAnimationDriver.StunStartSeconds</c> 와 같다.
        /// 이 뒤로는 쓰러진 자세로 버틴다.
        /// </summary>
        private const double StunEntrySeconds = 2.2d;

        /// <summary>기절이 풀려 일어나는 시간(초). 실제 플레이의 <c>PlayerAnimationDriver.StunEndSeconds</c> 와 같다.</summary>
        private const double StunRecoverySeconds = 1.2d;

        private readonly Dictionary<int, double> lastHitAt = new();

        /// <summary>맞은 쪽 시각. <see cref="lastHitAt"/> 는 때린 쪽이라 피격 모션에 쓸 수 없다.</summary>
        private readonly Dictionary<int, double> lastHitTakenAt = new();
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
        private HighlightCandidate[] aiCandidates=Array.Empty<HighlightCandidate>();
        private readonly Dictionary<HighlightCandidate, Game.Core.Ports.HighlightDirectorPick> aiCaptions=new();
        private bool highlightSelectionFrozen;
        private bool hasExplicitHighlightCandidates;
        private bool replayUnavailable;
        // 마지막으로 본 숨기기 차례. 차례가 넘어가는 순간을 한 번만 잡아내려고 들고 있다.
        private int lastHidingTurnIndex = HidingTurns.NoTurn;

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

        /// <summary>
        /// 찾기 페이즈 끝의 무제한 달리기 구간 길이. 최종 경고 배너와 같은 구간을 쓰므로
        /// 규칙은 <see cref="MatchRulesSO.FinalWarningSeconds"/> 한 곳에만 산다.
        /// </summary>
        public float FinalSprintWindowSeconds => rules.FinalWarningSeconds;
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

            var phaseBeforeAdvance = state.CurrentPhase.CurrentValue;
            if (phaseBeforeAdvance == MatchPhase.Hiding)
            {
                CompleteExpiredHidingTurns(now, lastKnownPlayerPositions);
                SkipDepartedHidingTurns(now);
                DropCarryOnHidingTurnChange(now, lastKnownPlayerPositions);
            }

            CompleteMapObjectEjections(now);

            var changed = flow.AdvanceIfExpired(now);
            if (phaseBeforeAdvance == MatchPhase.Hiding &&
                state.CurrentPhase.CurrentValue != MatchPhase.Hiding)
            {
                DropHeldMapObjects(lastKnownPlayerPositions);
            }

            RaiseFinalWarningIfNeeded(now);
            // Empty selections still pass through the result-stage/readiness
            // schedule. Completing here bypassed result presentation entirely.

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
            var isHidingTurn = CanActDuringHidingTurn(playerIndex, now);
            var isWaitingToHide = !isSearching && !isHidingTurn &&
                                  CanActWhileWaitingToHide(playerIndex, now);
            if ((!isHidingTurn && !isSearching && !isWaitingToHide) ||
                outcome.GetHeldItemOwner(playerIndex) >= 0 ||
                heldMapObjectIdsByPlayer[playerIndex] != null)
            {
                return false;
            }

            // 차례를 기다리는 사람에게는 대기 구역 소품만 허락한다. 배정 물건까지 열면
            // 집 밖에서 남이 숨겨 둔 물건을 빼내 가는 길이 된다.
            if (!isWaitingToHide && outcome.TryHoldItem(playerIndex, objectId))
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
            // 하이라이트는 찾기 페이즈의 기록이다. 숨기기 중 놓기는 차례든 대기든 남기지 않는다.
            var isSearching = CanInteract(playerIndex, now);
            if (!CanPlaceHeldObject(playerIndex, pose, now) ||
                !TryGetHeldObjectId(playerIndex, out var objectId) ||
                !ReleaseHeldObjectAt(playerIndex, pose))
            {
                return false;
            }

            if (isSearching)
            {
                highlightRecorder.RecordItemInteraction(playerIndex, objectId, now);
            }

            lastPlacementAt[playerIndex] = now;

            return true;
        }

        public bool TryDropHeldObject(int playerIndex, Pose pose, double now)
        {
            var isSearching = CanInteract(playerIndex, now);
            if ((!CanActDuringHidingTurn(playerIndex, now) && !isSearching &&
                 !CanActWhileWaitingToHide(playerIndex, now)) ||
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
                    CanInteract(playerIndex, now) ||
                    CanActWhileWaitingToHide(playerIndex, now)) &&
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
            if ((!CanActDuringHidingTurn(playerIndex, now) && !isSearching &&
                 !CanActWhileWaitingToHide(playerIndex, now)) ||
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

        /// <summary>
        /// 놓기·던지기·배치가 왜 거부됐는지 사람이 읽을 수 있는 한 줄로 설명한다. 호스트 로그와 요청한
        /// 클라이언트의 경고에 붙여, 팀 테스트에서 "물건이 손에 붙어 안 떨어진다"의 원인을 바로 볼 수 있게 한다.
        /// </summary>
        /// <param name="requirePlacementValidity">배치(release)처럼 겹침·받침 검사가 필요한 요청이면 true.</param>
        public string DescribeReleaseBlock(int playerIndex, Pose pose, double now, bool requirePlacementValidity)
        {
            if (playerIndex < 0 || playerIndex >= Players.Players.Count) return "unknown player index";
            if (!Players.IsActive(playerIndex)) return "player is not active in this match";

            var phase = state.CurrentPhase.CurrentValue;
            if (phase == MatchPhase.Hiding)
            {
                // 차례가 아닌 사람도 대기 구역 소품은 내려놓는다. 막히는 건 인트로와, 자기 차례를 다 쓴 경우다.
                if (flow.IsPhaseIntro(now)) return "hiding phase: intro is still running";
                if (flow.GetCurrentHidingTurnIndex(now) == playerIndex)
                {
                    if (flow.GetHidingTurnRemainingSeconds(now) <= 0d) return "hiding phase: turn time is over";
                    if (completedHidingTurns[playerIndex]) return "hiding phase: turn already completed";
                }
            }
            else if (!IsSearchingAt(now))
            {
                return $"phase={phase}: interactions are closed";
            }
            else if (interactions.IsStunned(playerIndex, now))
            {
                return "player is stunned";
            }

            if (!TryGetHeldObjectId(playerIndex, out var objectId)) return "authority has no held object for this player";
            if (requirePlacementValidity && !placementValidator.IsValid(objectId, pose))
                return $"placement pose invalid on authority (overlap or no support) at {pose.position}";
            return "unknown";
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
                lastHitTakenAt[targetPlayerIndex] = now;
                return HitResult.Registered;
            }

            var hitResult = interactions.RegisterHit(targetPlayerIndex, now);
            if (hitResult != HitResult.Ignored)
            {
                lastHitAt[attackerPlayerIndex] = now;
                lastHitTakenAt[targetPlayerIndex] = now;
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
            aiCandidates=new HighlightCandidate[candidates.Count];
            for(int i=0;i<candidates.Count;i++) aiCandidates[i]=candidates[i];
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

        public bool IsReplayFrameDue(double now) =>
            CanRecordReplay(now) && highlightReplayBuffer.IsSampleDue(now);

        private bool CanRecordReplay(double now)
        {
            if (replayUnavailable) return false;
            var phase = state.CurrentPhase.CurrentValue;
            var searchingStartedAt = state.PhaseEndsAt.CurrentValue - flow.SearchingDurationSeconds;
            var canRecordSearching = phase == MatchPhase.Searching && state.PhaseEndsAt.CurrentValue > 0d &&
                                     now >= searchingStartedAt + HighlightRecordingDelaySeconds;
            // Result-stage teleports are presentation, never replay footage.
            return canRecordSearching && !result.HasValue;
        }

        public bool TryRecordReplayFrame(
            double now,
            IReadOnlyList<Pose> playerPoses,
            IReadOnlyList<WorldObjectState> replayObjects,
            IReadOnlyList<HighlightPlayerAction> playerActions = null)
        {
            if (!CanRecordReplay(now)) return false;

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

            if (!highlightReplayBuffer.IsSampleDue(now)) return false;

            var actions = new HighlightPlayerAction[playerPoses.Count];
            for (var i = 0; i < actions.Length; i++)
            {
                var action = playerActions == null
                    ? HighlightPlayerAction.None
                    : playerActions[i];
                if (interactions.IsStunned(i, now))
                {
                    action |= HighlightPlayerAction.Stunned;
                    var stunStartedAt = interactions.GetStunEndsAt(i) - rules.StunDurationSeconds;
                    if (now - stunStartedAt < StunEntrySeconds)
                        action |= HighlightPlayerAction.StunEntry;
                }
                // 기절로 쓰러진 뒤에는 움찔하지 않는다. 실제 플레이도 기절이 피격을 덮는다.
                else if (lastHitTakenAt.TryGetValue(i, out var hitTakenAt) &&
                         now - hitTakenAt < HitReactionSeconds)
                {
                    action |= HighlightPlayerAction.Hit;
                }
                else
                {
                    // 한 번도 기절한 적이 없으면 끝난 시각이 0이라 이 창에 들어오지 않는다.
                    var stunEndedAt = interactions.GetStunEndsAt(i);
                    if (stunEndedAt > 0d && now - stunEndedAt < StunRecoverySeconds)
                        action |= HighlightPlayerAction.StunRecovery;
                }

                if (lastHitAt.TryGetValue(i, out var hitAt) && now - hitAt < 0.5d)
                    action |= HighlightPlayerAction.Punching;
                if (TryGetHeldObjectId(i, out _)) action |= HighlightPlayerAction.Carrying;
                if (lastThrowAt.TryGetValue(i, out var thrownAt) && now - thrownAt < 0.5d)
                    action |= HighlightPlayerAction.Throwing;
                if (lastPlacementAt.TryGetValue(i, out var placedAt) && now - placedAt < 0.5d)
                    action |= HighlightPlayerAction.Placing;
                // 맞거나 기절하거나 물건을 다루면 감정 표현은 거기서 끊긴다(실제 플레이와 같다).
                if ((action & (HighlightPlayerAction.Stunned | HighlightPlayerAction.Hit |
                               HighlightPlayerAction.Punching | HighlightPlayerAction.Throwing |
                               HighlightPlayerAction.Placing)) != 0)
                {
                    action = action.WithoutEmote();
                }

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

        private bool HasCandidateFrames(HighlightCandidate candidate)
        {
            foreach(var segment in candidate.Segments)
                if(!highlightReplayBuffer.HasFramesWithBoundary(segment.StartedAt,segment.EndedAt)) return false;
            return candidate.Segments.Count>0;
        }

        public Game.Core.Ports.HighlightDirectorCandidate[] CaptureDirectorCandidates()
        {
            if(!result.HasValue || highlightSelectionFrozen) return Array.Empty<Game.Core.Ports.HighlightDirectorCandidate>();
            var playable=new List<HighlightCandidate>();
            foreach(var candidate in aiCandidates)
                if(playable.Count<10 && HasCandidateFrames(candidate)) playable.Add(candidate);
            aiCandidates=playable.ToArray();
            var request=new Game.Core.Ports.HighlightDirectorCandidate[aiCandidates.Length];
            for(int i=0;i<request.Length;i++)
            {
                var candidate=aiCandidates[i];
                request[i]=new Game.Core.Ports.HighlightDirectorCandidate {
                    id=i,eventType=candidate.Type.ToString(),seconds=candidate.PlaybackDurationSeconds,
                    segments=candidate.Segments.Count,remainingSeconds=Math.Max(0,result.Value.EndedAt-candidate.EventAt),
                    involvedPlayers=(candidate.ActorPlayerIndex>=0 ? 1 : 0)+(candidate.SecondaryPlayerIndex>=0 ? 1 : 0),
                    ruleScore=candidate.Score
                };
            }
            return request;
        }

        public bool TryApplyDirectorSelection(Game.Core.Ports.HighlightDirectorReply reply)
        {
            var pickCount=reply?.UsablePickCount(aiCandidates.Length) ?? 0;
            if(CurrentPhase!=MatchPhase.Highlight || highlightSelectionFrozen || pickCount==0) return false;
            var selected=new List<HighlightCandidate>();
            for(int i=0;i<pickCount;i++) selected.Add(aiCandidates[reply.picks[i].id]);
            highlights=new HighlightSequence(selected,rules,true);
            aiCaptions.Clear();
            for(int i=0;i<pickCount;i++) aiCaptions.Add(selected[i],reply.picks[i]);
            return true;
        }

        public bool TryCaptureHighlightReplay(out HighlightReplayData[] replay)
        {
            if (!result.HasValue)
            {
                replay = Array.Empty<HighlightReplayData>();
                return false;
            }

            highlightSelectionFrozen=true;
            var selected = highlights.Capture();
            var playableCandidates = new List<HighlightCandidate>(selected.Length);
            var playableReplay = new List<HighlightReplayData>(selected.Length);
            for (var index = 0; index < selected.Length; index++)
            {
                var clips = CaptureReplay(selected[index]);
                if (!HasPlayableFrames(clips)) continue;
                playableCandidates.Add(selected[index]);
                aiCaptions.TryGetValue(selected[index],out var caption);
                playableReplay.Add(new HighlightReplayData(selected[index],clips,caption?.title,caption?.summary));
            }

            // The shared schedule must describe the payload clients can actually play.
            // Otherwise an empty clip still consumes highlight time behind a black cover.
            highlights = new HighlightSequence(playableCandidates, rules, true);
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

        /// <summary>
        /// 숨기기 페이즈에서 자기 차례를 기다리는 사람이 대기 구역 소품을 만질 수 있는지.
        /// </summary>
        /// <remarks>
        /// 대기자는 집 밖에서 이미 걸어 다니고 때리기도 하므로 손만 묶어 둘 이유가 없다. 다만 열어 주는
        /// 것은 맵 소품뿐이고(<see cref="TryHoldObject"/>), 인트로가 도는 동안은 아무도 움직이지 않는다.
        /// </remarks>
        private bool CanActWhileWaitingToHide(int playerIndex, double now)
        {
            return Players.IsActive(playerIndex) &&
                   state.CurrentPhase.CurrentValue == MatchPhase.Hiding &&
                   !flow.IsPhaseIntro(now) &&
                   flow.GetCurrentHidingTurnIndex(now) != playerIndex;
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

            return ReleaseHeldMapObjectAt(playerIndex, pose, wasAutoPlaced);
        }

        /// <summary>
        /// 손에 든 맵 소품만 골라 그 자리에 둔다. 배정 물건과 달리 숨긴 자리로 기록하지 않는다.
        /// </summary>
        private bool ReleaseHeldMapObjectAt(int playerIndex, Pose pose, bool wasAutoPlaced)
        {
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
                var candidates=highlightRecorder.CaptureCandidates(endedAt,endReason,highlightReplayBuffer.Capture(0d,endedAt));
                highlights=new HighlightSequence(candidates,rules);
                aiCandidates=highlightRecorder.ExpandAiCandidates(candidates,endedAt);
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

        /// <summary>
        /// 차례가 넘어온 사람의 손을 비운다. 대기하는 동안 주운 소품이 그대로 따라가면 배정 물건을
        /// 쥐여줄 손이 없고(<see cref="TryInitializeAssignedItem"/>), 숨길 물건 대신 엉뚱한 소품을
        /// 들고 서 있게 된다. 아직 대기 구역 좌표인 이 틱에 떨궈야 소품이 제자리에 남는다.
        /// </summary>
        private void DropCarryOnHidingTurnChange(
            double now,
            IReadOnlyList<Vector3> lastKnownPlayerPositions)
        {
            var turn = flow.GetCurrentHidingTurnIndex(now);
            if (turn == lastHidingTurnIndex)
            {
                return;
            }

            lastHidingTurnIndex = turn;
            if (turn < 0 || !Players.IsActive(turn))
            {
                return;
            }

            ReleaseHeldMapObjectAt(
                turn,
                new Pose(lastKnownPlayerPositions[turn], Quaternion.identity),
                true);
        }

        /// <summary>
        /// 숨기기가 끝나는 순간 손에 남은 소품을 그 자리에 둔다. 차례를 마친 사람도 대기 구역에서
        /// 소품을 주울 수 있어, 이걸 놓지 않으면 찾기 스폰으로 옮겨질 때 소품이 함께 딸려온다.
        /// </summary>
        private void DropHeldMapObjects(IReadOnlyList<Vector3> lastKnownPlayerPositions)
        {
            for (var playerIndex = 0;
                 playerIndex < heldMapObjectIdsByPlayer.Length;
                 playerIndex++)
            {
                ReleaseHeldMapObjectAt(
                    playerIndex,
                    new Pose(lastKnownPlayerPositions[playerIndex], Quaternion.identity),
                    true);
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
