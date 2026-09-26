using Game.Training.HideSeek;
using Unity.MLAgents.Actuators;

namespace Game.Training.Thief
{
    /// <summary>Behavior "ThiefSeek": while empty-handed, where to search, how to look, which seen prop to take, flee or attack.</summary>
    public sealed class ThiefSeekAgent : ThiefAgentBase
    {
        protected override int ObservationSize => ThiefArena.SeekObservationSize;
        protected override int ActionCount => ThiefMatch.SeekActionCount;

        protected override void Write(float[] into) => arena.WriteSeekObservation(match, into);

        public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
        {
            if (match == null) return;
            for (var a = 0; a < ThiefMatch.SeekActionCount; a++)
            {
                if (!match.SeekAllowed(a)) actionMask.SetActionEnabled(0, a, false);
            }
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            if (match != null && !match.ThiefCarrying) match.ApplyThiefAction(actions.DiscreteActions[0]);
            else if (match != null) match.ThiefAwaiting = false;
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var d = actionsOut.DiscreteActions;
            d[0] = match == null ? ThiefMatch.SeekActionLook
                : heuristicMode == HideSeekHeuristic.Rule ? arena.RuleSeekAction(match)
                : ThiefArena.RandomAllowed(match.SeekAllowed, ThiefMatch.SeekActionCount, match.Rng, ThiefMatch.SeekActionLook);
        }
    }
}
