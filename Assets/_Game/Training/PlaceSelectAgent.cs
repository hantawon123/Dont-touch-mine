using Game.BotRuntime.Policy;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Game.Training
{
    /// <summary>
    /// v2 placement policy (behavior "PlaceSelect"): given four reachable floor spots described only by terrain
    /// cues, choose where to put the held prop. One decision per episode. Several of these agents can share one
    /// PlaceTrainingEnvironment to train in parallel.
    /// Heuristics: Random, Nearest (shortest path), MostEnclosed (a rule a person would write), Oracle (reads the
    /// hidden true score -- an upper bound only, never a policy to ship).
    /// </summary>
    public sealed class PlaceSelectAgent : Agent
    {
        public enum HeuristicMode
        {
            Random,
            Nearest,
            MostEnclosed,
            Oracle,
        }

        [SerializeField]
        private PlaceTrainingEnvironment environment;

        [SerializeField]
        private HeuristicMode heuristicMode = HeuristicMode.MostEnclosed;

        [SerializeField]
        private int heuristicSeed = 20260925;

        private readonly float[] observation = new float[PlaceObservationLayout.VectorSize];
        private System.Random heuristicRng;
        private PlaceEpisode episode;
        private bool awaitingDecision;

        public void Bind(PlaceTrainingEnvironment env) => environment = env;

        public override void Initialize()
        {
            heuristicRng = new System.Random(heuristicSeed);
            var behavior = GetComponent<BehaviorParameters>();
            if (behavior != null)
            {
                var brain = behavior.BrainParameters;
                var ok = brain.VectorObservationSize == PlaceObservationLayout.VectorSize &&
                         brain.ActionSpec.NumDiscreteActions == 1 &&
                         brain.ActionSpec.BranchSizes[0] == PlaceObservationLayout.ActionCount;
                if (!ok)
                {
                    Debug.LogError(
                        $"[Place Agent] Behavior Parameters mismatch: need obs {PlaceObservationLayout.VectorSize}, one discrete branch of {PlaceObservationLayout.ActionCount}.",
                        this);
                }

                if (environment != null && environment.MayAct(this))
                {
                    environment.SetPolicyName(behavior.BehaviorType == BehaviorType.HeuristicOnly
                        ? "Heuristic " + heuristicMode
                        : $"{behavior.BehaviorType} model={(behavior.Model != null ? behavior.Model.name : "(none)")}");
                }
            }
        }

        public override void OnEpisodeBegin()
        {
            episode = null;
            awaitingDecision = false;
        }

        private void FixedUpdate()
        {
            if (environment == null || !environment.IsReady || environment.IsFinished || awaitingDecision ||
                !environment.MayAct(this))
            {
                return;
            }

            if (episode == null && !environment.TryCreateEpisode(out episode))
            {
                return;
            }

            awaitingDecision = true;
            RequestDecision();
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            PlaceObservationEncoder.Encode(
                episode != null ? episode.HeldSize : PickSizeClass.Small,
                episode?.Candidates,
                observation);
            for (var i = 0; i < observation.Length; i++)
            {
                sensor.AddObservation(observation[i]);
            }
        }

        public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
        {
            // ML-Agents also collects a mask for the terminal step, when there may be no episode data.
            // Never mask every action: slot 0 always stays enabled.
            var count = episode != null ? episode.Count : 0;
            for (var slot = Mathf.Max(1, count); slot < PlaceObservationLayout.ActionCount; slot++)
            {
                actionMask.SetActionEnabled(0, slot, false);
            }
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            if (episode == null)
            {
                return;
            }

            var slot = Mathf.Clamp(actions.DiscreteActions[0], 0, episode.Count - 1);
            AddReward(episode.Rewards[slot]);
            environment.Record(episode, slot);
            EndEpisode();
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var discrete = actionsOut.DiscreteActions;
            discrete[0] = 0;
            if (episode == null || episode.Count == 0)
            {
                return;
            }

            switch (heuristicMode)
            {
                case HeuristicMode.Random:
                    discrete[0] = heuristicRng.Next(episode.Count);
                    return;
                case HeuristicMode.Nearest:
                {
                    var best = 0;
                    for (var i = 1; i < episode.Count; i++)
                    {
                        if (episode.PathLengths[i] < episode.PathLengths[best]) best = i;
                    }

                    discrete[0] = best;
                    return;
                }
                case HeuristicMode.MostEnclosed:
                {
                    // Lower mean ray distance = more enclosed; a roof overhead (table, stairs) counts as extra cover.
                    var best = 0;
                    var bestScore = float.PositiveInfinity;
                    for (var i = 0; i < episode.Count; i++)
                    {
                        var c = episode.Candidates[i];
                        var score = c.Openness - (c.Covered ? 0.25f : 0f);
                        if (score < bestScore)
                        {
                            bestScore = score;
                            best = i;
                        }
                    }

                    discrete[0] = best;
                    return;
                }
                case HeuristicMode.Oracle:
                    discrete[0] = episode.BestSlot;
                    return;
            }
        }
    }
}
