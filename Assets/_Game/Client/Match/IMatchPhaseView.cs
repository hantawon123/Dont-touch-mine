using Game.Core.Match;

namespace Game.Client.Match
{
    public interface IMatchPhaseView
    {
        /// <param name="hidingPlayerName">
        /// Who the hiding phase is waiting on, or empty when nobody is — every
        /// other phase, and hiding itself until the line-up carries a name. The
        /// phase alone used to be shown, which read as though everyone hid at
        /// once when in fact players take one turn each.
        /// </param>
        void SetPhase(MatchPhase phase, string hidingPlayerName);
    }
}
