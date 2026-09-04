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
        private readonly HashSet<string> incomingRequestIds = new HashSet<string>();
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
            Exclude(existingFriendIds);
            RebuildResults();
        }

        /// <summary>
        /// Updates who counts as already befriended, leaving the query alone.
        /// </summary>
        /// <remarks>
        /// Called when the friend list changes under a search that is already on
        /// screen. Without it the results keep offering to befriend someone who
        /// became a friend a moment ago — which happens on every accept, and on
        /// every request the server settles on the spot because the other player
        /// had asked first. Pressing that button answers ALREADY_FRIENDS.
        /// <para>
        /// Separate from <see cref="Search"/> because that one also replaces the
        /// query, and there is no new query here — only a new answer to which
        /// rows still make sense.
        /// </para>
        /// </remarks>
        public void ExcludeFriends(IEnumerable<string> friendIds)
        {
            if (friendIds == null)
            {
                throw new ArgumentNullException(nameof(friendIds));
            }

            Exclude(friendIds);
            RebuildResults();
        }

        /// <summary>
        /// Hides the people who have already asked to be friends with this
        /// player, and stops requests being sent back to them.
        /// </summary>
        /// <remarks>
        /// A separate set from the friends one, not the same one, because the
        /// two are refreshed at different moments: reading the friend list would
        /// otherwise wipe the requesters, and reading the requests would wipe
        /// the friends.
        /// <para>
        /// Hidden rather than shown with a different button. The panel lists
        /// received requests directly above these results with an accept button
        /// on each, so the person is already on screen with the right action
        /// beside them. Leaving them here too offered a second, worse way to
        /// reach the same outcome — the server settles a mutual request on the
        /// spot, so pressing it worked, but the screen was showing one person as
        /// two things at once.
        /// </para>
        /// </remarks>
        public void ExcludeIncomingRequests(IEnumerable<string> requesterIds)
        {
            if (requesterIds == null)
            {
                throw new ArgumentNullException(nameof(requesterIds));
            }

            incomingRequestIds.Clear();
            foreach (var requesterId in requesterIds)
            {
                if (!string.IsNullOrWhiteSpace(requesterId))
                {
                    incomingRequestIds.Add(requesterId.Trim());
                }
            }

            RebuildResults();
        }

        private void Exclude(IEnumerable<string> friendIds)
        {
            excludedFriendIds.Clear();
            foreach (var friendId in friendIds)
            {
                if (!string.IsNullOrWhiteSpace(friendId))
                {
                    excludedFriendIds.Add(friendId.Trim());
                }
            }
        }

        private bool IsHidden(string playerId)
        {
            return excludedFriendIds.Contains(playerId)
                || incomingRequestIds.Contains(playerId);
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
            if (IsHidden(id) || pendingRequests.Contains(id))
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

        /// <summary>
        /// Takes back a request that was shown as pending but did not happen.
        /// </summary>
        /// <remarks>
        /// <see cref="TrySendRequest"/> marks the row pending before the server
        /// has answered, so the button reacts to the click rather than to the
        /// round trip. That leaves the row lying whenever the server refuses,
        /// and without this there is no way to stop it: the pending set only
        /// ever grew, so one failed request showed "요청 중" until the player
        /// closed the panel.
        /// <para>
        /// Does nothing when the row was not pending, so a caller can undo
        /// without first checking whether there is anything to undo.
        /// </para>
        /// </remarks>
        public void CancelPendingRequest(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new ArgumentException("Player id is required.", nameof(playerId));
            }

            if (pendingRequests.Remove(playerId.Trim()))
            {
                RebuildResults();
            }
        }

        private void RebuildResults()
        {
            var next = new List<FriendSearchHit>();
            if (lastQuery.Length > 0)
            {
                for (var index = 0; index < directory.Count; index++)
                {
                    var user = directory[index];
                    if (IsHidden(user.PlayerId) || !Matches(user, lastQuery))
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
