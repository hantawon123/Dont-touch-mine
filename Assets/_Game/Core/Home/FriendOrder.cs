using System;
using System.Collections.Generic;

namespace Game.Core.Home
{
    /// <summary>
    /// The order friends are listed in: Hangul first, then Latin, then digits.
    /// </summary>
    /// <remarks>
    /// Ordinal comparison alone would put digits first and Hangul last, because
    /// that is the order their code points happen to fall in. The design asks
    /// for the reverse, so each character is ranked by which alphabet it
    /// belongs to before its own value is looked at.
    /// </remarks>
    public sealed class FriendNameComparer : IComparer<string>
    {
        public static readonly FriendNameComparer Instance = new FriendNameComparer();

        public int Compare(string left, string right)
        {
            if (left == null)
            {
                return right == null ? 0 : 1;
            }

            if (right == null)
            {
                return -1;
            }

            var shared = Math.Min(left.Length, right.Length);
            for (var index = 0; index < shared; index++)
            {
                var byAlphabet = Rank(left[index]).CompareTo(Rank(right[index]));
                if (byAlphabet != 0)
                {
                    return byAlphabet;
                }

                var byCharacter = left[index].CompareTo(right[index]);
                if (byCharacter != 0)
                {
                    return byCharacter;
                }
            }

            // One is a prefix of the other, so the shorter comes first.
            return left.Length.CompareTo(right.Length);
        }

        private static int Rank(char character)
        {
            if (character >= '가' && character <= '힣')
            {
                return 0;
            }

            if ((character >= 'a' && character <= 'z') || (character >= 'A' && character <= 'Z'))
            {
                return 1;
            }

            if (character >= '0' && character <= '9')
            {
                return 2;
            }

            return 3;
        }
    }

    /// <summary>
    /// The order friend requests are listed in: the one that arrived last sits
    /// at the top.
    /// </summary>
    /// <remarks>
    /// Ties are broken by <see cref="FriendNameComparer"/> rather than left to
    /// whatever order the server answered in. Two requests can share a
    /// timestamp — the server records them to the second — and a list that
    /// reshuffles between refreshes is a list a player cannot click reliably.
    /// </remarks>
    public sealed class FriendRequestComparer : IComparer<FriendRequestSummary>
    {
        public static readonly FriendRequestComparer Instance = new FriendRequestComparer();

        public int Compare(FriendRequestSummary left, FriendRequestSummary right)
        {
            var byTime = right.RequestedAtUtc.CompareTo(left.RequestedAtUtc);
            return byTime != 0
                ? byTime
                : FriendNameComparer.Instance.Compare(left.Nickname, right.Nickname);
        }
    }

    /// <summary>
    /// Puts a list of requests in the order the panel draws them, dropping the
    /// same sender listed twice.
    /// </summary>
    /// <remarks>
    /// A duplicate is not a server bug to report: a request the player has
    /// already answered can still be in a list read a moment earlier. Two rows
    /// for one person means the second one refuses to do anything when pressed,
    /// so only the first is kept.
    /// </remarks>
    public static class FriendRequestOrder
    {
        public static IReadOnlyList<FriendRequestSummary> Arrange(
            IReadOnlyList<FriendRequestSummary> requests)
        {
            if (requests == null || requests.Count == 0)
            {
                return Array.Empty<FriendRequestSummary>();
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var arranged = new List<FriendRequestSummary>(requests.Count);
            for (var index = 0; index < requests.Count; index++)
            {
                if (seen.Add(requests[index].PlayerId))
                {
                    arranged.Add(requests[index]);
                }
            }

            arranged.Sort(FriendRequestComparer.Instance);
            return arranged;
        }
    }
}
