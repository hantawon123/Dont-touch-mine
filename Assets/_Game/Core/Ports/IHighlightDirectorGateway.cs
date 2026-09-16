using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Core.Ports
{
    [Serializable] public sealed class HighlightDirectorCandidate
    {
        public int id, segments, involvedPlayers;
        public string eventType;
        public double seconds, remainingSeconds, ruleScore;
    }
    [Serializable] public sealed class HighlightDirectorPick { public int id; public string title, summary; }
    [Serializable] public sealed class HighlightDirectorReply
    {
        public bool available;
        public HighlightDirectorPick[] picks;
        public bool IsUsable(int count)
        {
            if(!available || picks == null || picks.Length != Math.Min(3,count)) return false;
            int used = 0;
            foreach(var pick in picks)
            {
                if(pick == null || pick.id < 0 || pick.id >= count || (used & (1 << pick.id)) != 0 ||
                    !ValidText(pick.title,24) || !ValidText(pick.summary,70)) return false;
                used |= 1 << pick.id;
            }
            return count > 0 && count <= 10;
        }
        public static bool ValidText(string value,int limit)
        {
            if(string.IsNullOrWhiteSpace(value) || value.Length > limit || value != value.Trim()) return false;
            foreach(char c in value) if(char.IsControl(c) || c == '<' || c == '>') return false;
            return !value.Contains("://") && !value.Contains("@");
        }
    }
    public interface IHighlightDirectorGateway
    {
        UniTask<HighlightDirectorReply> DirectAsync(HighlightDirectorCandidate[] candidates, CancellationToken cancellation);
    }
}
