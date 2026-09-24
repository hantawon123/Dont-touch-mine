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
using Unity.MLAgents.Policies;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Training
{
    public enum PickEpisodeOutcome
    {
        Success,
        WrongItem,
        Timeout,
    }

    /// <summary>
    /// Training_Pick 씬의 학습 환경. 에피소드마다 목표 종류를 뽑고 물건 3개를 봇 앞 부채꼴 안에
    /// 무작위로 놓는다. 절반의 에피소드에서는 목표 물건이 가장 가까운 물건이 아니게 배치해
    /// "가까운 것 집기"만으로는 풀 수 없게 한다. 판단 주기마다 Agent에 결정을 요청하고,
    /// 제한 시간이 지나면 시간 초과를 알린다.
    /// </summary>
    public sealed class PickEpisodeEnvironment : MonoBehaviour, IPickEnvironment
    {
        [Header("연결")]
        [SerializeField]
        private PickSelectAgent agent;

        [SerializeField]
        private PickBotExecutor executor;

        [SerializeField]
        private string npcId = "npc:1";

        [Header("시간")]
        [SerializeField, Min(1f)]
        private float episodeSeconds = 30f;

        [SerializeField, Min(0.1f)]
        private float decisionIntervalSeconds = 0.5f;

        [Header("배치")]
        [SerializeField, Min(0.5f)]
        private float minDistance = 2.5f;

        [SerializeField, Min(1f)]
        private float maxDistance = 7f;

        [SerializeField, Range(5f, 180f)]
        [Tooltip("배치 부채꼴의 반각. 1단계 50, 2단계(전 방향) 180.")]
        private float halfAngleDegrees = 50f;

        [SerializeField, Min(0.3f)]
        private float minSpacing = 1.2f;

        [SerializeField, Range(0f, 1f)]
        private float hardCaseRatio = 0.5f;

        [SerializeField]
        [Tooltip("0이면 매 실행 무작위. 평가 배치를 재현하려면 고정 시드를 쓴다.")]
        private int placementSeed;

        [SerializeField]
        [Tooltip("시작 위치에서 보이는 자리만 고른다(난이도 1~2단계). 벽 뒤 탐색이 필요한 3단계에서 끈다.")]
        private bool requireVisibleFromStart = true;

        [SerializeField]
        [Tooltip("켜면 시작 방향 시야각 안만 허용(1단계). 끄면 제자리에서 돌면 보이는 자리까지 허용(2단계). 벽 가림 검사는 그대로.")]
        private bool requireInStartFieldOfView = true;

        [SerializeField]
        [Tooltip("봇 눈 높이. 실행부의 BotEye 위치와 같게 둔다.")]
        private Vector3 eyeLocalPosition = new(0f, 1.5f, 0f);

        [SerializeField, Min(1f)]
        private float sightDistance = 12f;

        [SerializeField, Range(10f, 180f)]
        private float sightHalfAngleDegrees = 60f;

        [Header("기록")]
        [SerializeField, Min(1)]
        private int logEveryEpisodes = 20;

        [Header("속도 시험")]
        [SerializeField, Min(0f)]
        [Tooltip("0이면 건드리지 않음. 트레이너 없이 Heuristic으로 배율을 시험할 때 Time.timeScale을 이 값으로 둔다. 트레이너가 붙으면 트레이너의 --time-scale이 우선한다.")]
        private float editorTimeScale;

        [Header("평가 모드")]
        [SerializeField, Min(0)]
        [Tooltip("0이면 무제한(학습용). N이면 정확히 N 에피소드만 돌고 최종 표를 찍은 뒤 멈춘다. placementSeed를 고정해 같은 배치를 재현한다.")]
        private int evaluationEpisodes;

        private readonly List<CarryableItem> items = new();
        private readonly Dictionary<string, CarryableItem> byId = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> kindById = new(StringComparer.Ordinal);
        private readonly Dictionary<string, PickSizeClass> sizeById = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Pose> initialPoseById = new(StringComparer.Ordinal);
        private readonly List<string> kinds = new();
        private readonly int[] outcomeCounts = new int[3];
        private readonly int[] totalActions = new int[PickObservationLayout.ActionCount];
        private readonly RaycastHit[] placementHits = new RaycastHit[32];
        private ulong placementFingerprint = 1469598103934665603UL;
        private readonly StringBuilder report = new();

        private System.Random rng;
        private NpcCarryAuthority authority;
        private Pose botStartPose;
        private double episodeStartedAt;
        private double nextDecisionAt;
        private float successSecondsTotal;
        private double simAtLastSummary;
        private double realAtLastSummary;
        private int episodeIndex;
        private int loggedEpisodes;
        private int zeroCandidateStarts;
        private int goalOutsideStartViewEpisodes;
        private bool firstObservationReported;
        private bool evaluationFinished;

        public bool IsReady { get; private set; }
        public string GoalKind { get; private set; } = string.Empty;
        public PickBotExecutor Executor => executor;
        public float DecisionIntervalSeconds => decisionIntervalSeconds;
        public int EpisodeIndex => episodeIndex;

        private static double Now => BotSimClock.Now;

        public float TimeLeftRatio =>
            IsReady ? Mathf.Clamp01((float)(1.0 - (Now - episodeStartedAt) / episodeSeconds)) : 0f;

        public bool IsGoalMatch(string targetId, string kindKey) => IsGoalKind(kindKey);

        private bool IsGoalKind(string kindKey) =>
            !string.IsNullOrEmpty(GoalKind) && string.Equals(kindKey, GoalKind, StringComparison.Ordinal);

        public bool TryGetSize(string id, out PickSizeClass size) => sizeById.TryGetValue(id ?? string.Empty, out size);

        private void Awake()
        {
            if (executor == null)
            {
                executor = GetComponent<PickBotExecutor>() ?? gameObject.AddComponent<PickBotExecutor>();
            }

            // Agent는 끄지 않는다. 빌드에서 Agent를 나중에 켜면 ML-Agents Academy가 그 안에서 늦게 만들어져
            // 트레이너와의 첫 교환이 어긋난다(2026-09-24 빌드 시간 초과 원인). 대신 Academy를 지금 깨워
            // 에디터와 같은 순서(연결 → Agent 등록)를 강제하고, 준비 전에는 결정을 요청하지 않는다.
            var _ = Unity.MLAgents.Academy.Instance;

            rng = placementSeed == 0 ? new System.Random() : new System.Random(placementSeed);

            if (editorTimeScale > 0f)
            {
                Time.timeScale = editorTimeScale;
                Debug.Log($"[Pick Env] Time.timeScale = {editorTimeScale:F1} (speed test).", this);
            }
        }

        private void OnDestroy()
        {
            if (editorTimeScale > 0f)
            {
                Time.timeScale = 1f;
            }
        }

        private void Start()
        {
            if (agent == null)
            {
                Debug.LogError("[Pick Env] PickSelectAgent를 연결하세요.", this);
                return;
            }

            CollectItems();
            StartCoroutine(Boot());
        }

        private void Update()
        {
            if (!IsReady || evaluationFinished || agent == null || !agent.enabled)
            {
                return;
            }

            if (Now - episodeStartedAt >= episodeSeconds)
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

            // The start pose must come from the scene, not from the bot's current pose: by the time the
            // bot is bound it may already have taken a few steps, and even a few centimetres shift every
            // sampled position, so two runs with the same seed would not get the same problem sheet.
            var bootstrap = FindFirstObjectByType<BotKccTestBootstrap>();
            if (bootstrap != null && bootstrap.SpawnPoint != null)
            {
                var spawn = bootstrap.SpawnPoint;
                botStartPose = new Pose(spawn.position, Quaternion.Euler(0f, spawn.eulerAngles.y, 0f));
            }
            else
            {
                executor.TryGetPose(out botStartPose);
                Debug.LogWarning("[Pick Env] spawn point not found; using the bot's current pose as the start pose (not reproducible).", this);
            }

            IsReady = true;
            simAtLastSummary = Now;
            realAtLastSummary = Time.realtimeSinceStartupAsDouble;
            Debug.Log($"[Pick Env] 준비 완료. 물건 {items.Count}개, 종류 {string.Join(",", kinds)}, 시작 위치 {botStartPose.position}.", this);

            // Agent의 첫 OnEpisodeBegin은 준비 전에 지나갔으므로 첫 에피소드를 여기서 시작한다.
            ResetEpisode();
        }

        private void CollectItems()
        {
            items.Clear();
            byId.Clear();
            kindById.Clear();
            sizeById.Clear();
            initialPoseById.Clear();
            kinds.Clear();

            foreach (var item in FindObjectsByType<CarryableItem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var id = item.ObjectId;
                if (string.IsNullOrWhiteSpace(id) || byId.ContainsKey(id))
                {
                    Debug.LogWarning($"[Pick Env] 물건 '{item.name}'의 ID가 비었거나 중복입니다. 제외합니다.", item);
                    continue;
                }

                var kind = item.GetComponentInParent<BotSightKind>();
                var kindKey = kind != null ? kind.KindKey : string.Empty;
                if (string.IsNullOrEmpty(kindKey))
                {
                    Debug.LogWarning($"[Pick Env] 물건 '{item.name}'에 BotSightKind가 없어 목표 종류 후보에서 제외합니다.", item);
                }
                else if (!kinds.Contains(kindKey))
                {
                    kinds.Add(kindKey);
                }

                items.Add(item);
                byId.Add(id, item);
                kindById.Add(id, kindKey);
                sizeById.Add(id, PickObservationEncoder.ClassifySize(LargestExtent(item)));
                initialPoseById.Add(id, new Pose(item.transform.position, item.transform.rotation));
            }

            // FindObjectsByType does not guarantee order. Sort so the same seed draws the same goal kind
            // and assigns the same slots on every run.
            items.Sort((a, b) => string.CompareOrdinal(a.ObjectId, b.ObjectId));
            kinds.Sort(StringComparer.Ordinal);

            if (kinds.Count == 0)
            {
                Debug.LogError("[Pick Env] 종류 스티커가 붙은 물건이 없습니다. Pick_Box/Pick_Vase/Pick_Radio를 씬에 놓으세요.", this);
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

        /// <summary>Agent.OnEpisodeBegin에서 부른다. 준비 전에는 아무 일도 하지 않는다.</summary>
        public void ResetEpisode()
        {
            if (!IsReady)
            {
                return;
            }

            episodeIndex++;
            executor.ResetForEpisode();
            executor.ClearFailedTargets(); // props move every episode here, so an old failure says nothing

            GoalKind = kinds.Count > 0 ? kinds[rng.Next(kinds.Count)] : string.Empty;
            var poses = SamplePoses(items.Count);
            AssignPoses(poses);
            CountGoalOutsideStartView();
            MixFingerprint();

            var states = new List<WorldObjectState>(items.Count);
            foreach (var item in items)
            {
                states.Add(new WorldObjectState(item.ObjectId, new Pose(item.transform.position, item.transform.rotation)));
            }

            authority = new NpcCarryAuthority(states);
            executor.Configure(npcId, authority, byId);

            if (!executor.TryTeleport(botStartPose))
            {
                Debug.LogWarning("[Pick Env] 봇 시작 위치 이동 요청이 거절되었습니다.", this);
            }

            episodeStartedAt = Now;
            nextDecisionAt = Now + decisionIntervalSeconds;
            firstObservationReported = false;
            agent.OnEnvironmentReset();
        }

        private List<Pose> SamplePoses(int count)
        {
            var result = new List<Pose>(count);
            var forward = botStartPose.rotation * Vector3.forward;
            var attempts = 0;
            while (result.Count < count && attempts < 200)
            {
                attempts++;
                var angle = (float)(rng.NextDouble() * 2.0 - 1.0) * halfAngleDegrees;
                var distance = Mathf.Lerp(minDistance, maxDistance, (float)rng.NextDouble());
                var direction = Quaternion.Euler(0f, angle, 0f) * forward;
                var candidate = botStartPose.position + direction * distance;

                if (!NavMesh.SamplePosition(candidate, out var hit, 1f, NavMesh.AllAreas))
                {
                    continue;
                }

                if (requireVisibleFromStart && !IsVisibleFromStart(hit.position + Vector3.up * 0.25f))
                {
                    continue;
                }

                var tooClose = false;
                foreach (var placed in result)
                {
                    if ((placed.position - hit.position).sqrMagnitude < minSpacing * minSpacing)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (tooClose)
                {
                    continue;
                }

                result.Add(new Pose(hit.position, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f)));
            }

            if (result.Count < count)
            {
                Debug.LogWarning($"[Pick Env] 배치 표본 부족({result.Count}/{count}). 나머지는 처음 위치를 씁니다.", this);
            }

            return result;
        }

        /// <summary>
        /// 목표 물건을 어느 자리에 둘지 정한다. hardCaseRatio 비율로 "가장 가까운 자리가 아닌 곳"에 둔다.
        /// 각 물건의 높이는 씬에 처음 놓였던 값을 유지한다(바닥 위 안착 높이).
        /// 표본이 물건 수보다 적으면 무작위 배치를 포기하고 처음 위치를 그대로 쓴다.
        /// </summary>
        private void AssignPoses(List<Pose> poses)
        {
            if (poses.Count < items.Count)
            {
                foreach (var item in items)
                {
                    item.OnSettled(initialPoseById[item.ObjectId], keepDynamic: false);
                }

                Physics.SyncTransforms();
                return;
            }

            // 봇에서 가까운 순으로 자리 번호를 정렬한다.
            var order = new List<int>(poses.Count);
            for (var i = 0; i < poses.Count; i++)
            {
                order.Add(i);
            }

            order.Sort((a, b) =>
                (poses[a].position - botStartPose.position).sqrMagnitude
                    .CompareTo((poses[b].position - botStartPose.position).sqrMagnitude));

            var goalIndex = items.FindIndex(item => IsGoalKind(kindById[item.ObjectId]));
            var free = new List<int>(order);
            var slotByItem = new int[items.Count];

            if (goalIndex >= 0)
            {
                var hard = order.Count > 1 && rng.NextDouble() < hardCaseRatio;
                var slotForGoal = hard ? order[1 + rng.Next(order.Count - 1)] : order[rng.Next(order.Count)];
                slotByItem[goalIndex] = slotForGoal;
                free.Remove(slotForGoal);
            }

            // 나머지 물건은 남은 자리를 무작위 순서로 받는다.
            for (var i = 0; i < items.Count; i++)
            {
                if (i == goalIndex)
                {
                    continue;
                }

                var pick = rng.Next(free.Count);
                slotByItem[i] = free[pick];
                free.RemoveAt(pick);
            }

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var initial = initialPoseById[item.ObjectId];
                var p = poses[slotByItem[i]];
                var target = new Pose(new Vector3(p.position.x, initial.position.y, p.position.z), p.rotation);
                item.OnSettled(target, keepDynamic: false);
            }

            Physics.SyncTransforms();
        }

        /// <summary>
        /// 시작 위치의 눈에서 이 점이 보이는가: 시야각·거리 안이고, 광선이 다른 물건 아닌 것에 먼저 막히지 않는다.
        /// 물건끼리 살짝 겹치는 것은 허용한다(앞 물건이 뒤 물건을 가리는 상황은 정상 후보 구성).
        /// </summary>
        private bool IsVisibleFromStart(Vector3 point)
        {
            var eye = botStartPose.position + botStartPose.rotation * eyeLocalPosition;
            var offset = point - eye;
            var distance = offset.magnitude;
            if (distance <= 0.01f || distance > sightDistance)
            {
                return false;
            }

            var forward = Vector3.ProjectOnPlane(botStartPose.rotation * Vector3.forward, Vector3.up);
            var flat = Vector3.ProjectOnPlane(offset, Vector3.up);
            if (flat.sqrMagnitude <= 0.0001f ||
                (requireInStartFieldOfView && Vector3.Angle(forward, flat) > sightHalfAngleDegrees))
            {
                return false;
            }

            // Only static geometry may occlude. The bot's body (still standing where the previous episode
            // ended) and the props themselves are ignored, otherwise the accepted positions -- and every
            // later random draw -- would depend on the policy that ran before, and two policies would not
            // receive the same problem sheet for the same seed.
            var carryableLayer = LayerMask.NameToLayer("Carryable");
            var botRoot = executor != null ? executor.BotRoot : null;
            var count = Physics.RaycastNonAlloc(eye, offset / distance, placementHits, distance - 0.15f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var hitTransform = placementHits[i].collider.transform;
                if (hitTransform.gameObject.layer == carryableLayer ||
                    (botRoot != null && (hitTransform == botRoot || hitTransform.IsChildOf(botRoot))))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        /// <summary>
        /// 배치 지문: 에피소드마다 목표 종류와 물건 위치(cm 단위)를 섞어 누적한다(FNV-1a).
        /// 같은 시드로 돌린 정책끼리 지문이 같으면 같은 문제지를 받은 것이다.
        /// </summary>
        private void MixFingerprint()
        {
            void Mix(long value)
            {
                unchecked
                {
                    placementFingerprint ^= (ulong)value;
                    placementFingerprint *= 1099511628211UL;
                }
            }

            Mix(kinds.IndexOf(GoalKind));
            foreach (var item in items)
            {
                var p = item.transform.position;
                Mix(Mathf.RoundToInt(p.x * 100f));
                Mix(Mathf.RoundToInt(p.z * 100f));
            }
        }

        /// <summary>
        /// 평가가 스스로 검증되게 한다: 목표 물건이 시작 방향 시야각 밖에 놓였는지 센다.
        /// 2단계(전 방향) 시험인데 이 수가 0에 가까우면 지형 때문에 배치가 앞쪽에 몰린 것이고 그 평가는 무효다.
        /// </summary>
        private void CountGoalOutsideStartView()
        {
            var forward = Vector3.ProjectOnPlane(botStartPose.rotation * Vector3.forward, Vector3.up);
            foreach (var item in items)
            {
                if (!IsGoalKind(kindById[item.ObjectId]))
                {
                    continue;
                }

                var flat = Vector3.ProjectOnPlane(item.transform.position - botStartPose.position, Vector3.up);
                if (flat.sqrMagnitude > 0.0001f && Vector3.Angle(forward, flat) > sightHalfAngleDegrees)
                {
                    goalOutsideStartViewEpisodes++;
                }

                return;
            }
        }

        /// <summary>Agent가 에피소드의 첫 관측에서 후보 수를 알려 준다. 0개 시작은 배치 문제의 신호다.</summary>
        public void ReportFirstCandidateCount(int count)
        {
            if (firstObservationReported)
            {
                return;
            }

            firstObservationReported = true;
            if (count == 0)
            {
                zeroCandidateStarts++;
            }
        }

        /// <summary>
        /// 고정 배치 N회 평가를 마치고 최종 표를 찍는다. 이후 결정 요청을 멈춘다.
        /// 같은 placementSeed·같은 씬이면 모든 정책이 같은 배치 순서를 받는다.
        /// </summary>
        private void FinishEvaluation()
        {
            evaluationFinished = true;
            var successes = outcomeCounts[(int)PickEpisodeOutcome.Success];
            var wrong = outcomeCounts[(int)PickEpisodeOutcome.WrongItem];
            var timeouts = outcomeCounts[(int)PickEpisodeOutcome.Timeout];

            report.Clear();
            report.AppendLine("[Pick Eval] ===== 최종 평가 =====");
            report.AppendLine($"정책: {DescribePolicy()}");
            report.AppendLine($"배치 시드 {placementSeed}, 에피소드 {loggedEpisodes}, 관측 {PickObservationLayout.Version}");
            report.AppendLine($"배치 반각 {halfAngleDegrees:F0}°, 거리 {minDistance:F1}~{maxDistance:F1} m, 시작 시야각 제한 {(requireInStartFieldOfView ? "켬" : "끔")}");
            report.AppendLine($"행동 합계 Continue={totalActions[0]} Explore={totalActions[1]} Collect0={totalActions[2]} Collect1={totalActions[3]} Collect2={totalActions[4]}");
            report.AppendLine($"성공 {successes} ({100f * successes / Mathf.Max(1, loggedEpisodes):F1}%), 오답 {wrong}, 시간 초과 {timeouts}");
            if (successes > 0)
            {
                report.AppendLine($"성공 평균 시간 {successSecondsTotal / successes:F2}s");
            }

            report.AppendLine($"시작 시 후보 0개 에피소드 {zeroCandidateStarts}");
            report.AppendLine($"목표가 시작 시야 밖에 놓인 에피소드 {goalOutsideStartViewEpisodes} (2단계 시험이면 대략 절반 이상이어야 유효)");
            report.AppendLine($"배치 지문 {placementFingerprint:X16} (같은 시드·조건의 정책끼리 같아야 같은 문제지)");
            Debug.Log(report.ToString(), this);

            executor.ResetForEpisode();
            agent.enabled = false;
        }

        private string DescribePolicy()
        {
            var behavior = agent.GetComponent<BehaviorParameters>();
            if (behavior == null)
            {
                return "unknown";
            }

            var model = behavior.Model != null ? behavior.Model.name : "(none)";
            return behavior.BehaviorType == BehaviorType.HeuristicOnly
                ? $"Heuristic {agent.HeuristicModeName}"
                : $"{behavior.BehaviorType} model={model}";
        }

        public void RecordEpisode(PickEpisodeOutcome outcome, float seconds, int[] actionCounts)
        {
            if (evaluationFinished)
            {
                return;
            }

            outcomeCounts[(int)outcome]++;
            loggedEpisodes++;
            if (actionCounts != null)
            {
                for (var i = 0; i < totalActions.Length && i < actionCounts.Length; i++)
                {
                    totalActions[i] += actionCounts[i];
                }
            }
            if (outcome == PickEpisodeOutcome.Success)
            {
                successSecondsTotal += seconds;
            }

            if (evaluationEpisodes > 0 && loggedEpisodes >= evaluationEpisodes)
            {
                FinishEvaluation();
                return;
            }

            if (loggedEpisodes % logEveryEpisodes != 0)
            {
                return;
            }

            var successes = outcomeCounts[(int)PickEpisodeOutcome.Success];
            report.Clear();
            report.AppendLine($"[Pick Env] 에피소드 {loggedEpisodes}: 성공 {successes}, 오답 {outcomeCounts[(int)PickEpisodeOutcome.WrongItem]}, 시간 초과 {outcomeCounts[(int)PickEpisodeOutcome.Timeout]}");
            if (successes > 0)
            {
                report.AppendLine($"  성공 평균 {successSecondsTotal / successes:F2}s");
            }

            report.AppendLine($"  시작 시 후보 0개였던 에피소드 {zeroCandidateStarts}회 (배치 문제 신호)");

            var simNow = Now;
            var realNow = Time.realtimeSinceStartupAsDouble;
            var realDelta = realNow - realAtLastSummary;
            if (realDelta > 0.001)
            {
                report.AppendLine($"  sim speed {(simNow - simAtLastSummary) / realDelta:F2}x (sim s / real s), clock {(BotSimClock.IsInjected ? "Fusion" : "Unity")}, timeScale {Time.timeScale:F1}");
            }

            simAtLastSummary = simNow;
            realAtLastSummary = realNow;

            if (actionCounts != null)
            {
                report.AppendLine($"  마지막 에피소드 행동 횟수 Continue={actionCounts[0]} Explore={actionCounts[1]} Collect0={actionCounts[2]} Collect1={actionCounts[3]} Collect2={actionCounts[4]}");
            }

            Debug.Log(report.ToString(), this);
        }
    }
}
