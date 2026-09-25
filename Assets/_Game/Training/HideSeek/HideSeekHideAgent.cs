using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Game.Training.HideSeek
{
    /// <summary>Behavior "HideSelect" (team 0): which of 8 bank spots to hide the prop on, or go elsewhere.</summary>
    public sealed class HideSeekHideAgent : HideSeekAgentBase
    {
        protected override int ObservationSize => HideSeekArena.HideObservationSize;
        protected override int ActionCount => HideSeekMatch.HideActionCount;

        protected override void Write(float[] into) => arena.WriteHideObservation(match, into);

        public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
        {
            if (match == null)
            {
                return;
            }

            for (var slot = match.HideCandidates.Count; slot < HideSeekMatch.HideCandidateSlots; slot++)
            {
                actionMask.SetActionEnabled(0, slot, false);
            }
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            match?.ApplyHideAction(actions.DiscreteActions[0]);
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var d = actionsOut.DiscreteActions;
            d[0] = match == null ? HideSeekMatch.HideActionRelocate
                : heuristicMode == HideSeekHeuristic.Rule ? arena.RuleHideAction(match)
                : arena.RandomHideAction(match, match.Rng);
        }
    }
}
