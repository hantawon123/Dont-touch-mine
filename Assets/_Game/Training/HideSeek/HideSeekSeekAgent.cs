using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Game.Training.HideSeek
{
    /// <summary>Behavior "SeekSelect" (team 1): where to search next, how to look, when to go for the prop.</summary>
    public sealed class HideSeekSeekAgent : HideSeekAgentBase
    {
        protected override int ObservationSize => HideSeekArena.SeekObservationSize;
        protected override int ActionCount => HideSeekMatch.SeekActionCount;

        protected override void Write(float[] into) => arena.WriteSeekObservation(match, into);

        public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
        {
            if (match == null)
            {
                return;
            }

            for (var a = 0; a < HideSeekMatch.SeekActionCount; a++)
            {
                if (!arena.SeekActionAllowed(match, a))
                {
                    actionMask.SetActionEnabled(0, a, false);
                }
            }
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            match?.ApplySeekAction(actions.DiscreteActions[0]);
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var d = actionsOut.DiscreteActions;
            d[0] = match == null ? HideSeekMatch.SeekActionLook
                : heuristicMode == HideSeekHeuristic.Rule ? arena.RuleSeekAction(match)
                : arena.RandomSeekAction(match, match.Rng);
        }
    }
}
