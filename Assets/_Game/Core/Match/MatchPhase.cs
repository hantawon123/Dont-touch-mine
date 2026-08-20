namespace Game.Core.Match
{
    public enum MatchPhase
    {
        Waiting,
        Hiding,
        Searching,
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
                    next = MatchPhase.Result;
                    return true;
                default:
                    next = current;
                    return false;
            }
        }
    }
}
