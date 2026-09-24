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

        private static double Now => BotSimClock.Now;

        public bool IsReady { get; private set; }
        public PickBotExecutor Executor => executor;
        public float DecisionIntervalSeconds => decisionIntervalSeconds;
        public int EpisodeIndex => cycleIndex;

        public float TimeLeftRatio =>
            IsReady ? Mathf.Clamp01((float)(1.0 - (Now - cycleStartedAt) / cycleSeconds)) : 0f;

        public bool IsGoalMatch(string targetId, string kindKey)
        {
            if (targetId == null || !byId.TryGetValue(targetId, out var item) || item == null || item.IsCarried)
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

            IsReady = true;
            simAtLastSummary = Now;
            realAtLastSummary = Time.realtimeSinceStartupAsDouble;
            nextSummaryAt = Now + summarySeconds;
            executor.ResetObserveStats();
            Debug.Log($"[Mansion Sandbox] ready. props {items.Count}, goal rule: any prop not moved in the last {recentMoveCooldownSeconds:F0}s.", this);
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
            PutDownHeld();
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
                var key = result.Reason.Length > 60 ? result.Reason.Substring(0, 60) : result.Reason;
                holdRejections[key] = holdRejections.TryGetValue(key, out var r) ? r + 1 : 1;
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

            report.Append("collect failures:");
            foreach (var pair in failures)
            {
                report.Append($" {pair.Key}={pair.Value}");
            }

            report.AppendLine();
            foreach (var pair in holdRejections)
            {
                report.AppendLine($"  rejected x{pair.Value}: {pair.Key}");
            }

            if (executor.ObserveCalls > 0)
            {
                report.AppendLine(
                    $"observe: {executor.ObserveCalls} calls, avg {executor.ObserveMillisecondsTotal / executor.ObserveCalls:F2} ms, " +
                    $"max {executor.ObserveMillisecondsMax:F2} ms, avg seen/remembered {executor.VisibleTotal / (double)executor.ObserveCalls:F1}, props {items.Count}");
            }

            var realWindow = realNow - realAtLastSummary;
            if (realWindow > 0.001)
            {
                report.AppendLine($"sim speed {simWindow / realWindow:F2}x");
            }

            Debug.Log(report.ToString(), this);

            windowPickups = 0;
            executor.ResetObserveStats();
            simAtLastSummary = simNow;
            realAtLastSummary = realNow;
            nextSummaryAt = simNow + summarySeconds;
        }
    }
}
