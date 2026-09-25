using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Game.BotRuntime;
using Game.BotRuntime.Perception;
using Game.BotRuntime.Policy;
using Game.Client.Interactions;
using Game.Network.Match;
using Game.Network.Players;
using Game.Server.Items;
using UnityEngine;

namespace Game.Training
{
    /// <summary>
    /// 3a sandbox: runs the trained pick model on the real mansion props, without the match flow.
    /// There are no resets. One "episode" is one pickup cycle: the bot picks a goal prop, the cycle
    /// ends, the prop is put down in front of the bot and the next cycle starts where the bot stands.
    ///
    /// Goal rule (real-game goal 1): every carryable prop is a goal, except props this NPC moved in the
    /// last <see cref="recentMoveCooldownSeconds"/> seconds -- otherwise the prop just put down would
    /// always be the nearest match and the bot would lift the same prop forever.
    ///
    /// Every <see cref="summarySeconds"/> of simulation time it logs pickups, failures by kind,
    /// candidate counts and the cost of one observation, which is what this step is meant to measure.
    /// </summary>
    public sealed class MansionSandboxEnvironment : MonoBehaviour, IPickEnvironment
    {
        [Header("Links")]
        [SerializeField]
        private PickSelectAgent agent;

        [SerializeField]
        private PickBotExecutor executor;

        [SerializeField]
        private string npcId = "npc:1";

        [Header("Time")]
        [SerializeField, Min(1f)]
        private float cycleSeconds = 30f;

        [SerializeField, Min(0.1f)]
        private float decisionIntervalSeconds = 0.5f;

        [SerializeField, Min(1f)]
        private float summarySeconds = 60f;

        [Header("Goal")]
        [SerializeField, Min(0f)]
        private float recentMoveCooldownSeconds = 120f;

        [Header("Walking explore (Explore v2)")]
        [SerializeField, Min(10)]
        private int exploreWaypointCount = 150;

        [SerializeField, Min(1f)]
        private float exploreWaypointSpacing = 4f;

        [SerializeField]
        private int exploreSeed = 20260925;

        [Header("Placement (v2.1 model, optional)")]
        [SerializeField]
        [Tooltip("When set, a picked prop is carried to the spot this agent chooses instead of dropped in front.")]
        private PlaceSelectAgent placeAgent;

        [SerializeField]
        [Tooltip("Builds the four floor candidates around the real bot, with the same cues and scoring as training.")]
        private PlaceTrainingEnvironment placeEnvironment;

        [SerializeField, Min(1f)]
        private float carryTimeoutSeconds = 12f;

        [SerializeField, Min(0f)]
        private float placeReleaseUp = 0.3f;

        [Header("Put down")]
        [SerializeField]
        private float releaseForward = 1f;

        [SerializeField]
        private float releaseUp = 0.5f;

        private readonly List<CarryableItem> items = new();
        private readonly Dictionary<string, CarryableItem> byId = new(StringComparer.Ordinal);
        private readonly Dictionary<string, PickSizeClass> sizeById = new(StringComparer.Ordinal);
        private readonly Dictionary<string, double> movedAt = new(StringComparer.Ordinal);
        private readonly Dictionary<PickFailure, int> failures = new();
        private readonly Dictionary<string, int> holdRejections = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> stallBlockers = new(StringComparer.Ordinal);
        private readonly int[] outcomeCounts = new int[3];
        private readonly StringBuilder report = new();

        private NpcCarryAuthority authority;
        private double cycleStartedAt;
        private double nextDecisionAt;
        private double nextSummaryAt;
        private double simAtLastSummary;
        private double realAtLastSummary;
        private int cycleIndex;
        private int zeroCandidateCycles;
        private int releaseFallbacks;
        private int windowPickups;
        private bool firstObservationReported;
        private bool subscribed;

        // Placement phase state and stats (window).
        private bool placing;
        private readonly int[] windowActions = new int[PickObservationLayout.ActionCount];
        private int placeChosen;
        private int placedByPolicy;
        private int placeWalkFailures;
        private int placeNoCandidates;
        private int placeOracleMatches;
        private double placeChosenHide;
        private double placeMeanCandidateHide;
        private double placeRuleHide;
        private double placeOracleHide;
        private double placeWalkMetres;
        private readonly Dictionary<string, int> placeFailureReasons = new(StringComparer.Ordinal);

        private static double Now => BotSimClock.Now;

        public bool IsReady { get; private set; }
        public PickBotExecutor Executor => executor;
        public float DecisionIntervalSeconds => decisionIntervalSeconds;
        public int EpisodeIndex => cycleIndex;

        public float TimeLeftRatio =>
            IsReady ? Mathf.Clamp01((float)(1.0 - (Now - cycleStartedAt) / cycleSeconds)) : 0f;

        public bool IsGoalMatch(string targetId, string kindKey)
        {
            // Do not reject a carried prop here: the finished collect is judged while the bot already holds it.
            // Candidates never include carried props anyway (the executor skips them when observing).
            if (targetId == null || !byId.TryGetValue(targetId, out var item) || item == null)
            {
                return false;
            }

            return !(movedAt.TryGetValue(targetId, out var at) && Now - at < recentMoveCooldownSeconds);
        }

        public bool TryGetSize(string targetId, out PickSizeClass size) =>
            sizeById.TryGetValue(targetId ?? string.Empty, out size);

        public void Configure(PickSelectAgent pickAgent, PickBotExecutor pickExecutor)
        {
            agent = pickAgent;
            executor = pickExecutor;
        }

        private void Awake()
        {
            if (executor == null)
            {
                executor = GetComponent<PickBotExecutor>() ?? gameObject.AddComponent<PickBotExecutor>();
            }

            // Same reason as the arena: wake the Academy before the agent registers.
            var _ = Unity.MLAgents.Academy.Instance;
        }

        private void Start()
        {
            if (agent == null)
            {
                Debug.LogError("[Mansion Sandbox] connect a PickSelectAgent.", this);
                return;
            }

            CollectItems();
            StartCoroutine(Boot());
        }

        private void OnDestroy()
        {
            if (subscribed && executor != null)
            {
                executor.CollectFinished -= OnCollectFinished;
            }
        }

        private void Update()
        {
            if (!IsReady || agent == null || !agent.enabled)
            {
                return;
            }

            if (Now >= nextSummaryAt)
            {
                LogSummary();
            }

            if (placing)
            {
                return; // the pick policy waits while the prop is carried to its spot
            }

            if (Now - cycleStartedAt >= cycleSeconds)
            {
                agent.NotifyTimeout();
                return;
            }

            if (Now >= nextDecisionAt)
            {
                nextDecisionAt = Now + decisionIntervalSeconds;
                agent.RequestDecision();
            }
        }

        private IEnumerator Boot()
        {
            BotMoveToTarget mover = null;
            while (mover == null)
            {
                mover = FindFirstObjectByType<BotMoveToTarget>();
                yield return null;
            }

            yield return null;
            if (!executor.TryBind(mover.gameObject))
            {
                yield break;
            }

            while (!executor.TryGetPose(out _))
            {
                yield return null;
            }

            executor.CollectFinished += OnCollectFinished;
            subscribed = true;

            // Explore v2 (code, not learned): after a full turn with no goal, walk to the nearest unvisited waypoint.
            // Waypoints only where the bot can actually walk from its start (not the garden or roofs).
            executor.TryGetPose(out var startPose);
            executor.EnableWalkingExplore(exploreWaypointCount, exploreWaypointSpacing, exploreSeed,
                reachableFrom: startPose.position);

            // 13 props visible on average but only 3 slots: fill them goal-matches first, then nearest.
            executor.SetCandidatePriority(s => IsGoalMatch(s.Handle.TargetId, s.Observation.KindKey));
            // Hide props the bot cannot reach (no complete path, or farther than the hold distance from walkable floor).
            executor.SetReachabilityFilter(true);

            IsReady = true;
            simAtLastSummary = Now;
            realAtLastSummary = Time.realtimeSinceStartupAsDouble;
            nextSummaryAt = Now + summarySeconds;
            executor.ResetObserveStats();
            Debug.Log($"[Mansion Sandbox] ready. props {items.Count}, goal rule: any prop not moved in the last {recentMoveCooldownSeconds:F0}s. slots: goal-match first, then nearest; unreachable props hidden.", this);
            ResetEpisode();
        }

        private void CollectItems()
        {
            items.Clear();
            byId.Clear();
            sizeById.Clear();
            var duplicates = 0;
            foreach (var item in FindObjectsByType<CarryableItem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var id = item.ObjectId;
                if (string.IsNullOrWhiteSpace(id) || byId.ContainsKey(id))
                {
                    duplicates++;
                    continue;
                }

                items.Add(item);
                byId.Add(id, item);
                sizeById.Add(id, PickObservationEncoder.ClassifySize(LargestExtent(item)));
            }

            items.Sort((a, b) => string.CompareOrdinal(a.ObjectId, b.ObjectId));
            if (duplicates > 0)
            {
                Debug.LogWarning($"[Mansion Sandbox] skipped {duplicates} props with an empty or duplicate id.", this);
            }
        }

        private static float LargestExtent(CarryableItem item)
        {
            var colliders = item.GetComponentsInChildren<Collider>();
            if (colliders.Length == 0)
            {
                return 0.5f;
            }

            var bounds = colliders[0].bounds;
            for (var i = 1; i < colliders.Length; i++)
            {
                bounds.Encapsulate(colliders[i].bounds);
            }

            return Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        }

        /// <summary>Called by the agent's OnEpisodeBegin: put down what the bot holds and start the next cycle.</summary>
        public void ResetEpisode()
        {
            if (!IsReady)
            {
                return;
            }

            cycleIndex++;
            if (!placing && TryBeginPlacement())
            {
                return; // FinishCycleReset() runs once the prop has been put down
            }

            PutDownHeld();
            FinishCycleReset();
        }

        private void FinishCycleReset()
        {
            placing = false;
            executor.ResetForEpisode();

            // The authority mirrors the server's physics truth: rebuild it from where props actually are now.
            var states = new List<WorldObjectState>(items.Count);
            foreach (var item in items)
            {
                if (item != null && !item.IsCarried)
                {
                    states.Add(new WorldObjectState(item.ObjectId, new Pose(item.transform.position, item.transform.rotation)));
                }
            }

            authority = new NpcCarryAuthority(states);
            executor.Configure(npcId, authority, byId);

            cycleStartedAt = Now;
            nextDecisionAt = Now + decisionIntervalSeconds;
            firstObservationReported = false;
            agent.OnEnvironmentReset();
        }

        /// <summary>
        /// Ask the placement policy where to put the held prop, then walk there. Returns false when placement is
        /// not connected or no candidates exist; the caller then drops the prop in front as before.
        /// </summary>
        private bool TryBeginPlacement()
        {
            var held = executor.Interactor != null ? executor.Interactor.CarriedItem : null;
            if (held == null || placeAgent == null || placeEnvironment == null || !placeEnvironment.IsReady ||
                !executor.TryGetPose(out var botPose))
            {
                return false;
            }

            sizeById.TryGetValue(held.ObjectId, out var size);
            if (!placeEnvironment.TryCreateEpisodeAt(botPose.position, botPose.rotation.eulerAngles.y, size, out var placement))
            {
                placeNoCandidates++;
                return false;
            }

            placing = true;
            if (!placeAgent.RequestPlacement(placement, slot => OnPlacementChosen(placement, slot)))
            {
                placing = false;
                return false;
            }

            return true;
        }

        private void OnPlacementChosen(PlaceEpisode placement, int slot)
        {
            // Compare the choice with what the hand-written rule would have picked on the same four spots.
            var rule = 0;
            var ruleScore = float.PositiveInfinity;
            var meanHide = 0f;
            for (var i = 0; i < placement.Count; i++)
            {
                var c = placement.Candidates[i];
                var score = c.Openness - (c.Covered ? 0.25f : 0f);
                if (score < ruleScore)
                {
                    ruleScore = score;
                    rule = i;
                }

                meanHide += placement.Hides[i];
            }

            var best = placement.BestSlot;
            placeChosen++;
            placeChosenHide += placement.Hides[slot];
            placeMeanCandidateHide += meanHide / placement.Count;
            placeRuleHide += placement.Hides[rule];
            placeOracleHide += placement.Hides[best];
            if (slot == best)
            {
                placeOracleMatches++;
            }

            var spot = placement.SpotPositions[slot];
            placeWalkMetres += placement.PathLengths[slot];
            if (!executor.TryBeginWalkTo(spot, carryTimeoutSeconds, (ok, failure, reason) => OnCarryFinished(spot, ok, failure, reason)))
            {
                OnCarryFinished(spot, false, PickFailure.Rejected, "executor busy");
            }
        }

        private void OnCarryFinished(Vector3 spot, bool arrived, PickFailure failure, string reason)
        {
            var held = executor.Interactor != null ? executor.Interactor.CarriedItem : null;
            if (arrived && held != null && executor.TryGetPose(out var botPose))
            {
                var release = new Pose(spot + Vector3.up * placeReleaseUp, Quaternion.identity);
                if (authority.TryRelease(npcId, botPose, release, out var releaseReason))
                {
                    executor.Interactor.ApplyConfirmedRelease(held, release, Vector3.zero);
                    PickBotExecutor.SetCarving(held, true);
                    movedAt[held.ObjectId] = Now;
                    placedByPolicy++;
                    FinishCycleReset();
                    return;
                }

                reason = "release rejected: " + releaseReason;
            }

            placeWalkFailures++;
            var key = reason.Length > 70 ? reason.Substring(0, 70) : reason;
            placeFailureReasons[key] = placeFailureReasons.TryGetValue(key, out var n) ? n + 1 : 1;
            PutDownHeld();
            FinishCycleReset();
        }

        private void PutDownHeld()
        {
            var held = executor.Interactor != null ? executor.Interactor.CarriedItem : null;
            if (held == null || authority == null || !executor.TryGetPose(out var botPose))
            {
                return;
            }

            var front = new Pose(
                botPose.position + botPose.rotation * Vector3.forward * releaseForward + Vector3.up * releaseUp,
                Quaternion.identity);
            var release = front;
            if (!authority.TryRelease(npcId, botPose, front, out _))
            {
                // Put it down at the bot's feet instead; this pose is always within the release distance.
                release = new Pose(botPose.position + Vector3.up * releaseUp, Quaternion.identity);
                releaseFallbacks++;
                if (!authority.TryRelease(npcId, botPose, release, out var reason))
                {
                    Debug.LogWarning($"[Mansion Sandbox] could not put down '{held.ObjectId}': {reason}", this);
                }
            }

            executor.Interactor.ApplyConfirmedRelease(held, release, Vector3.zero);
            PickBotExecutor.SetCarving(held, true);
            movedAt[held.ObjectId] = Now;
        }

        public void ReportFirstCandidateCount(int count)
        {
            if (firstObservationReported)
            {
                return;
            }

            firstObservationReported = true;
            if (count == 0)
            {
                zeroCandidateCycles++;
            }
        }

        public void RecordEpisode(PickEpisodeOutcome outcome, float seconds, int[] actionCounts)
        {
            outcomeCounts[(int)outcome]++;
            if (outcome == PickEpisodeOutcome.Success)
            {
                windowPickups++;
                executor.NoteGoalProgress();
            }

            if (actionCounts != null)
            {
                for (var i = 0; i < windowActions.Length && i < actionCounts.Length; i++)
                {
                    windowActions[i] += actionCounts[i];
                }
            }
        }

        private void OnCollectFinished(PickCollectResult result)
        {
            if (result.Success)
            {
                return;
            }

            failures[result.Failure] = failures.TryGetValue(result.Failure, out var n) ? n + 1 : 1;
            if (result.Failure == PickFailure.Rejected)
            {
                // Group by reason, not by prop id.
                var key = result.Reason.Contains("interaction distance") ? "outside the interaction distance" : result.Reason;
                holdRejections[key] = holdRejections.TryGetValue(key, out var r) ? r + 1 : 1;
            }
            else if (result.Failure == PickFailure.Stalled)
            {
                const string marker = "blocked by ";
                var at = result.Reason.IndexOf(marker, StringComparison.Ordinal);
                var key = at >= 0 ? result.Reason.Substring(at + marker.Length) : "unknown";
                stallBlockers[key] = stallBlockers.TryGetValue(key, out var n2) ? n2 + 1 : 1;
            }
        }

        private void LogSummary()
        {
            var simNow = Now;
            var realNow = Time.realtimeSinceStartupAsDouble;
            var simWindow = Math.Max(0.001, simNow - simAtLastSummary);

            report.Clear();
            report.AppendLine($"[Mansion Sandbox] ===== {simNow:F0}s (cycle {cycleIndex}) =====");
            report.AppendLine($"cycles: picked {outcomeCounts[(int)PickEpisodeOutcome.Success]}, wrong {outcomeCounts[(int)PickEpisodeOutcome.WrongItem]}, timeout {outcomeCounts[(int)PickEpisodeOutcome.Timeout]} (total)");
            report.AppendLine($"pickups this window {windowPickups} ({windowPickups * 60.0 / simWindow:F1}/min), cycles starting with 0 candidates {zeroCandidateCycles}, put-down fallbacks {releaseFallbacks}");

            report.Append("collect failures this window:");
            foreach (var pair in failures)
            {
                report.Append($" {pair.Key}={pair.Value}");
            }

            report.AppendLine();
            foreach (var pair in holdRejections)
            {
                report.AppendLine($"  rejected x{pair.Value}: {pair.Key}");
            }

            var blockers = new List<KeyValuePair<string, int>>(stallBlockers);
            blockers.Sort((a, b) => b.Value.CompareTo(a.Value));
            for (var i = 0; i < blockers.Count && i < 6; i++)
            {
                report.AppendLine($"  stalled x{blockers[i].Value}: {blockers[i].Key}");
            }

            if (executor.ObserveCalls > 0)
            {
                report.AppendLine(
                    $"observe: {executor.ObserveCalls} calls, avg {executor.ObserveMillisecondsTotal / executor.ObserveCalls:F2} ms, " +
                    $"max {executor.ObserveMillisecondsMax:F2} ms, avg seen/remembered {executor.VisibleTotal / (double)executor.ObserveCalls:F1}, props {items.Count}");
                report.AppendLine($"failed-target cooldown hid a prop {executor.CooldownSuppressions} times, unreachable filter hid {executor.UnreachableSuppressions} times (observe-level, not a policy override)");
                report.AppendLine($"turned to face the target after arrival {executor.FaceTurns} times");
            }

            if (executor.WaypointCount > 0)
            {
                report.AppendLine($"explore: wanted to walk {executor.ExploreWalkWanted} (no waypoint {executor.ExploreNoWaypoint}), walks {executor.ExploreWalks} (failed {executor.ExploreWalkFailures}), waypoints ever visited {executor.WaypointsEverVisited}/{executor.WaypointCount} ({100.0 * executor.WaypointsEverVisited / executor.WaypointCount:F0}%)");
            }

            report.AppendLine($"pick policy actions this window: Continue={windowActions[0]} Explore={windowActions[1]} Collect0={windowActions[2]} Collect1={windowActions[3]} Collect2={windowActions[4]} (completed cycles only)");

            // Hide stats are summed when a spot is chosen, so divide by choices, not by completed placements.
            var decided = placeChosen;
            if (placeAgent != null)
            {
                report.AppendLine($"placement ({placeAgent.PolicyLabel}): chosen {placeChosen}, placed at chosen spot {placedByPolicy}, walk/release failures {placeWalkFailures}, no candidates {placeNoCandidates}, planned walk {(decided > 0 ? placeWalkMetres / decided : 0):F1} m avg");
                if (decided > 0)
                {
                    report.AppendLine($"  hide of chosen spot {placeChosenHide / decided:F3} | rule would pick {placeRuleHide / decided:F3} | mean of candidates {placeMeanCandidateHide / decided:F3} | oracle {placeOracleHide / decided:F3} | oracle match {100.0 * placeOracleMatches / decided:F0}%");
                }

                foreach (var pair in placeFailureReasons)
                {
                    report.AppendLine($"  place failed x{pair.Value}: {pair.Key}");
                }
            }

            var realWindow = realNow - realAtLastSummary;
            if (realWindow > 0.001)
            {
                report.AppendLine($"sim speed {simWindow / realWindow:F2}x");
            }

            Debug.Log(report.ToString(), this);

            windowPickups = 0;
            failures.Clear();
            holdRejections.Clear();
            stallBlockers.Clear();
            placedByPolicy = placeWalkFailures = placeNoCandidates = placeOracleMatches = placeChosen = 0;
            System.Array.Clear(windowActions, 0, windowActions.Length);
            placeChosenHide = placeMeanCandidateHide = placeRuleHide = placeOracleHide = placeWalkMetres = 0;
            placeFailureReasons.Clear();
            executor.ResetObserveStats();
            simAtLastSummary = simNow;
            realAtLastSummary = realNow;
            nextSummaryAt = simNow + summarySeconds;
        }
    }
}
