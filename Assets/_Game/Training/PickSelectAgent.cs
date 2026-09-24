using System;
using System.Collections.Generic;
using Game.BotRuntime;
using Game.BotRuntime.Perception;
using Game.BotRuntime.Policy;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Training
{
    /// <summary>
    /// 물건 선택 Agent. 관측 44칸, 이산 행동 5개(Continue, Explore, CollectSlot0~2).
    /// 결정은 환경이 0.5초마다 RequestDecision으로 요청한다(DecisionRequester를 쓰지 않는다).
    /// 보상은 확정 이벤트에만 준다: 목표 일치 집기 +1 종료, 불일치 집기 -0.2 종료, 시간 초과 -0.2 종료,
    /// 판단마다 시간 비용. Heuristic은 무작위 / 최근접 / 목표 일치 최근접 기준선 정책이다.
    /// </summary>
    public sealed class PickSelectAgent : Agent
    {
        public enum HeuristicMode
        {
            Random,
            NearestAny,
            NearestGoalMatch,
        }

        [SerializeField, FormerlySerializedAs("environment")]
        [Tooltip("IPickEnvironment를 구현한 컴포넌트(PickEpisodeEnvironment 또는 MansionSandboxEnvironment).")]
        private MonoBehaviour environmentComponent;

        private IPickEnvironment environment;

        [Header("보상")]
        [SerializeField, Min(0f)]
        private float successReward = 1f;

        [SerializeField, Min(0f)]
        private float wrongItemPenalty = 0.2f;

        [SerializeField, Min(0f)]
        private float timeoutPenalty = 0.2f;

        [SerializeField, Min(0f)]
        private float timeCostPerSecond = 0.005f;

        [Header("기준선(Behavior Type = Heuristic Only일 때)")]
        [SerializeField]
        private HeuristicMode heuristicMode = HeuristicMode.NearestGoalMatch;

        [SerializeField]
        private int heuristicSeed = 20260923;

        [SerializeField]
        private bool logDecisions;

        private readonly float[] observation = new float[PickObservationLayout.VectorSize];
        private readonly bool[] enabledActions = new bool[PickObservationLayout.ActionCount];
        private readonly List<BotSighting> sightings = new();
        private readonly List<PickCandidate> candidates = new();
        private readonly int[] actionCounts = new int[PickObservationLayout.ActionCount];

        private System.Random heuristicRng;
        private PickBotExecutor subscribedExecutor;
        private double episodeStartedAt;

        private PickBotExecutor Executor => environment != null ? environment.Executor : null;

        protected override void Awake()
        {
            base.Awake();
            ResolveEnvironment();
        }

        private void ResolveEnvironment()
        {
            environment = environmentComponent as IPickEnvironment;
            if (environmentComponent != null && environment == null)
            {
                Debug.LogError($"[Pick Agent] '{environmentComponent.GetType().Name}' does not implement IPickEnvironment.", this);
            }
        }

        /// <summary>Editor/builder hook: connects the agent to an environment in code.</summary>
        public void BindEnvironment(MonoBehaviour component)
        {
            environmentComponent = component;
            ResolveEnvironment();
        }

        /// <summary>평가 표에 어떤 기준선이었는지 남기기 위한 이름.</summary>
        public string HeuristicModeName => heuristicMode.ToString();

        public override void Initialize()
        {
            heuristicRng = new System.Random(heuristicSeed);

            var behavior = GetComponent<BehaviorParameters>();
            if (behavior != null)
            {
                var brain = behavior.BrainParameters;
                var obsOk = brain.VectorObservationSize == PickObservationLayout.VectorSize;
                var actOk = brain.ActionSpec.NumDiscreteActions == 1 &&
                            brain.ActionSpec.BranchSizes.Length == 1 &&
                            brain.ActionSpec.BranchSizes[0] == PickObservationLayout.ActionCount;
                if (!obsOk || !actOk)
                {
                    Debug.LogError(
                        $"[Pick Agent] Behavior Parameters 불일치. Vector Observation Space Size={PickObservationLayout.VectorSize}, " +
                        $"Discrete Branch 1개 크기 {PickObservationLayout.ActionCount}로 맞추세요. (현재 obs={brain.VectorObservationSize}, " +
                        $"branches={string.Join(",", brain.ActionSpec.BranchSizes)})",
                        this);
                }
            }
        }

        public override void OnEpisodeBegin()
        {
            if (environment == null)
            {
                return;
            }

            SubscribeExecutor();
            environment.ResetEpisode();
        }

        /// <summary>환경이 실제로 초기화를 마친 시점에 부른다. 준비 전 OnEpisodeBegin과 시계가 어긋나지 않게 한다.</summary>
        public void OnEnvironmentReset()
        {
            Array.Clear(actionCounts, 0, actionCounts.Length);
            episodeStartedAt = BotSimClock.Now;
            SubscribeExecutor();
        }

        private void SubscribeExecutor()
        {
            var executor = Executor;
            if (executor == null || subscribedExecutor == executor)
            {
                return;
            }

            if (subscribedExecutor != null)
            {
                subscribedExecutor.CollectFinished -= OnCollectFinished;
            }

            subscribedExecutor = executor;
            executor.CollectFinished += OnCollectFinished;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (subscribedExecutor != null)
            {
                subscribedExecutor.CollectFinished -= OnCollectFinished;
                subscribedExecutor = null;
            }
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            BuildCandidates();
            environment?.ReportFirstCandidateCount(candidates.Count);
            PickObservationEncoder.Encode(BuildSelfState(), candidates, Executor != null ? Executor.LastFailure : PickFailure.None, observation);
            for (var i = 0; i < observation.Length; i++)
            {
                sensor.AddObservation(observation[i]);
            }
        }

        public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
        {
            // CollectObservations가 먼저 불려 candidates가 채워져 있다. 같은 후보 목록으로 마스크를 쓴다.
            PickActionMask.Compute(BuildSelfState(), candidates, enabledActions);
            for (var i = 0; i < enabledActions.Length; i++)
            {
                if (!enabledActions[i])
                {
                    actionMask.SetActionEnabled(0, i, false);
                }
            }
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            var executor = Executor;
            if (environment == null || !environment.IsReady || executor == null)
            {
                return;
            }

            AddReward(-timeCostPerSecond * environment.DecisionIntervalSeconds);

            var action = actions.DiscreteActions[0];
            if (action < 0 || action >= PickObservationLayout.ActionCount)
            {
                return;
            }

            actionCounts[action]++;
            if (logDecisions)
            {
                Debug.Log($"[Pick Agent] ep{environment.EpisodeIndex} action={action} busy={executor.Busy} candidates={candidates.Count}", this);
            }

            if (action == PickObservationLayout.ActionExplore)
            {
                executor.TryBeginLookAround();
            }
            else if (PickObservationLayout.IsCollectAction(action))
            {
                var slot = PickObservationLayout.SlotOfAction(action);
                if (slot < sightings.Count)
                {
                    executor.TryBeginCollect(sightings[slot]);
                }
            }
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var discrete = actionsOut.DiscreteActions;
            discrete[0] = PickObservationLayout.ActionContinue;

            var executor = Executor;
            if (executor == null || executor.Busy)
            {
                return;
            }

            BuildCandidates();
            PickActionMask.Compute(BuildSelfState(), candidates, enabledActions);

            switch (heuristicMode)
            {
                case HeuristicMode.Random:
                {
                    var valid = new List<int>();
                    for (var i = 0; i < enabledActions.Length; i++)
                    {
                        if (enabledActions[i]) valid.Add(i);
                    }

                    discrete[0] = valid[heuristicRng.Next(valid.Count)];
                    return;
                }
                case HeuristicMode.NearestAny:
                {
                    discrete[0] = candidates.Count > 0
                        ? PickObservationLayout.ActionCollectSlot0
                        : PickObservationLayout.ActionExplore;
                    return;
                }
                case HeuristicMode.NearestGoalMatch:
                {
                    for (var slot = 0; slot < candidates.Count; slot++)
                    {
                        if (candidates[slot].GoalMatch)
                        {
                            discrete[0] = PickObservationLayout.ActionCollectSlot0 + slot;
                            return;
                        }
                    }

                    discrete[0] = PickObservationLayout.ActionExplore;
                    return;
                }
            }
        }

        /// <summary>환경이 제한 시간에 부른다.</summary>
        public void NotifyTimeout()
        {
            AddReward(-timeoutPenalty);
            environment.RecordEpisode(PickEpisodeOutcome.Timeout, (float)(BotSimClock.Now - episodeStartedAt), actionCounts);
            EndEpisode();
        }

        private void OnCollectFinished(PickCollectResult result)
        {
            if (!result.Success)
            {
                // 실패는 종료가 아니다. 실행 피드백 칸으로 모델에 전달되고 에피소드는 계속된다.
                return;
            }

            var goal = environment.IsGoalMatch(result.TargetId, result.KindKey);
            AddReward(goal ? successReward : -wrongItemPenalty);
            environment.RecordEpisode(
                goal ? PickEpisodeOutcome.Success : PickEpisodeOutcome.WrongItem,
                (float)(BotSimClock.Now - episodeStartedAt),
                actionCounts);
            EndEpisode();
        }

        private PickSelfState BuildSelfState()
        {
            var executor = Executor;
            return new PickSelfState(
                executor != null && executor.HandOccupied,
                environment != null ? environment.TimeLeftRatio : 0f,
                executor != null && executor.Busy,
                executor != null ? executor.LastOutcome : PickOutcome.None);
        }

        private void BuildCandidates()
        {
            candidates.Clear();
            var executor = Executor;
            if (executor == null || environment == null)
            {
                sightings.Clear();
                return;
            }

            executor.Observe(sightings);
            var now = BotSimClock.Now;
            var memory = Mathf.Max(executor.MemorySeconds, 0.01f);
            foreach (var sighting in sightings)
            {
                var id = sighting.Handle.TargetId;
                environment.TryGetSize(id, out var size);
                candidates.Add(new PickCandidate(
                    sighting.Observation.LocalDirection.x,
                    sighting.Observation.LocalDirection.z,
                    sighting.Observation.NormalizedDistance,
                    size,
                    shreddable: true,
                    heldByOther: false,
                    goalMatch: environment.IsGoalMatch(id, sighting.Observation.KindKey),
                    normalizedAge: Mathf.Clamp01((float)(now - sighting.Observation.ObservedTime) / memory),
                    targetId: id));
            }
        }
    }
}
