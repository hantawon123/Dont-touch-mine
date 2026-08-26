using System;
using System.Collections.Generic;

namespace Game.Core.Home
{
    public enum FriendRequestState
    {
        None,
        Pending
    }

    public readonly struct FriendSearchHit
    {
        public FriendSearchHit(
            string playerId,
            string nickname,
            FriendRequestState requestState)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new ArgumentException("Player id is required.", nameof(playerId));
            }

            if (string.IsNullOrWhiteSpace(nickname))
            {
                throw new ArgumentException("Nickname is required.", nameof(nickname));
            }

            if (!Enum.IsDefined(typeof(FriendRequestState), requestState))
            {
                throw new ArgumentOutOfRangeException(nameof(requestState));
            }

            PlayerId = playerId.Trim();
            Nickname = nickname.Trim();
            RequestState = requestState;
        }

        public string PlayerId { get; }
        public string Nickname { get; }
        public FriendRequestState RequestState { get; }
        public bool IsPending => RequestState == FriendRequestState.Pending;
    }

    public sealed class FriendSearchSystem
    {
        private readonly List<FriendSummary> directory = new List<FriendSummary>();
        private readonly HashSet<string> pendingRequests = new HashSet<string>();
        private readonly HashSet<string> excludedFriendIds = new HashSet<string>();
        private List<FriendSearchHit> results = new List<FriendSearchHit>();
        private string lastQuery = string.Empty;

        public IReadOnlyList<FriendSearchHit> Results => results;

        public event Action ResultsChanged;

        public void ReplaceDirectory(IEnumerable<FriendSummary> users)
        {
            if (users == null)
            {
                throw new ArgumentNullException(nameof(users));
            }

            directory.Clear();
            foreach (var user in users)
            {
                directory.Add(user);
            }

            RebuildResults();
        }

        public void Search(string query, IEnumerable<string> existingFriendIds)
        {
            if (existingFriendIds == null)
            {
                throw new ArgumentNullException(nameof(existingFriendIds));
            }

            lastQuery = query == null ? string.Empty : query.Trim();
            excludedFriendIds.Clear();
            foreach (var friendId in existingFriendIds)
            {
                if (!string.IsNullOrWhiteSpace(friendId))
                {
                    excludedFriendIds.Add(friendId.Trim());
                }
            }

            RebuildResults();
        }

        public void ClearResults()
        {
            lastQuery = string.Empty;
            if (results.Count == 0)
            {
                return;
            }

            results = new List<FriendSearchHit>();
            ResultsChanged?.Invoke();
        }

        public bool TrySendRequest(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new ArgumentException("Player id is required.", nameof(playerId));
            }

            var id = playerId.Trim();
            if (excludedFriendIds.Contains(id) || pendingRequests.Contains(id))
            {
                return false;
            }

            var inDirectory = false;
            for (var index = 0; index < directory.Count; index++)
            {
                if (directory[index].PlayerId != id)
                {
                    continue;
                }

                inDirectory = true;
                break;
            }

            if (!inDirectory)
            {
                return false;
            }

            pendingRequests.Add(id);
            RebuildResults();
            return true;
        }

        private void RebuildResults()
        {
            var next = new List<FriendSearchHit>();
            if (lastQuery.Length > 0)
            {
                for (var index = 0; index < directory.Count; index++)
                {
                    var user = directory[index];
                    if (excludedFriendIds.Contains(user.PlayerId) || !Matches(user, lastQuery))
                    {
                        continue;
                    }

                    var state = pendingRequests.Contains(user.PlayerId)
                        ? FriendRequestState.Pending
                        : FriendRequestState.None;
                    next.Add(new FriendSearchHit(user.PlayerId, user.Nickname, state));
                }
            }

            results = next;
            ResultsChanged?.Invoke();
        }

        private static bool Matches(FriendSummary user, string query)
        {
            return user.Nickname.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || user.PlayerId.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
