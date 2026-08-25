namespace Game.Core.Match
{
    public enum MatchPhase
    {
        Waiting,
        Hiding,
        Searching,
        Highlight,
        Result
    }

    public static class MatchPhaseFlow
    {
        public static bool TryGetNext(MatchPhase current, out MatchPhase next)
        {
            switch (current)
            {
                case MatchPhase.Waiting:
                    next = MatchPhase.Hiding;
                    return true;
                case MatchPhase.Hiding:
                    next = MatchPhase.Searching;
                    return true;
                case MatchPhase.Searching:
                    next = MatchPhase.Highlight;
                    return true;
                case MatchPhase.Highlight:
                    next = MatchPhase.Result;
                    return true;
                default:
                    next = current;
                    return false;
            }
        }
    }
}
