using Game.Training.HideSeek;
using Unity.MLAgents.Actuators;

namespace Game.Training.Thief
{
    /// <summary>Behavior "ThiefHide": while carrying a player's prop, which bank spot to hide it on, go elsewhere, or flee.</summary>
    public sealed class ThiefHideAgent : ThiefAgentBase
    {
        protected override int ObservationSize => ThiefArena.HideObservationSize;
        protected override int ActionCount => ThiefMatch.HideActionCount;

        protected override void Write(float[] into) => arena.WriteHideObservation(match, into);

        public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
        {
            if (match == null) return;
            for (var a = 0; a < ThiefMatch.HideActionCount; a++)
            {
                if (!match.HideAllowed(a)) actionMask.SetActionEnabled(0, a, false);
            }
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            if (match != null && match.ThiefCarrying) match.ApplyThiefAction(actions.DiscreteActions[0]);
            else if (match != null) match.ThiefAwaiting = false;
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var d = actionsOut.DiscreteActions;
            d[0] = match == null ? ThiefMatch.HideActionRelocate
                : heuristicMode == HideSeekHeuristic.Rule ? arena.RuleHideAction(match)
                : ThiefArena.RandomAllowed(match.HideAllowed, ThiefMatch.HideActionCount, match.Rng, ThiefMatch.HideActionRelocate);
        }
    }
}
