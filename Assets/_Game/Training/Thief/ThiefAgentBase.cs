using Game.Training.HideSeek;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Game.Training.Thief
{
    /// <summary>Shared plumbing of the two thief brains: decisions on request only, labels, behavior checks.</summary>
    public abstract class ThiefAgentBase : Agent
    {
        [SerializeField]
        protected HideSeekHeuristic heuristicMode = HideSeekHeuristic.Rule;

        protected ThiefArena arena;
        protected ThiefMatch match;
        protected float[] observation;

        public string PolicyLabel { get; private set; } = "?";

        protected abstract int ObservationSize { get; }
        protected abstract int ActionCount { get; }

        public void Bind(ThiefArena owner, ThiefMatch round)
        {
            arena = owner;
            match = round;
        }

        public override void Initialize()
        {
            observation = new float[ObservationSize];
            var behavior = GetComponent<BehaviorParameters>();
            if (behavior == null)
            {
                return;
            }

            var brain = behavior.BrainParameters;
            if (brain.VectorObservationSize != ObservationSize || brain.ActionSpec.NumDiscreteActions != 1 ||
                brain.ActionSpec.BranchSizes[0] != ActionCount)
            {
                Debug.LogError($"[Thief] {behavior.BehaviorName} Behavior Parameters need obs {ObservationSize} and one discrete branch of {ActionCount}.", this);
            }

            var heuristic = behavior.BehaviorType == BehaviorType.HeuristicOnly ||
                            (behavior.BehaviorType == BehaviorType.Default && behavior.Model == null && !Academy.Instance.IsCommunicatorOn);
            PolicyLabel = heuristic
                ? "Heuristic " + heuristicMode
                : $"{behavior.BehaviorType} model={(behavior.Model != null ? behavior.Model.name : "(none)")}";
        }

        public void AskForDecision() => RequestDecision();

        public void Finish(float reward)
        {
            SetReward(reward);
            EndEpisode();
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            if (match != null)
            {
                Write(observation);
            }

            sensor.AddObservation(observation);
        }

        protected abstract void Write(float[] into);
    }
}
