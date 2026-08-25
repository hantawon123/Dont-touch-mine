using System;
using System.Collections.Generic;

namespace Game.Core.Home
{
    public enum FriendPresence
    {
        Offline,
        Online,
        InGame
    }

    public readonly struct FriendSummary
    {
        public FriendSummary(
            string playerId,
            string nickname,
            FriendPresence presence)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new ArgumentException("Player id is required.", nameof(playerId));
            }

            if (string.IsNullOrWhiteSpace(nickname))
            {
                throw new ArgumentException("Nickname is required.", nameof(nickname));
            }

            if (!Enum.IsDefined(typeof(FriendPresence), presence))
            {
                throw new ArgumentOutOfRangeException(nameof(presence));
            }

            PlayerId = playerId.Trim();
            Nickname = nickname.Trim();
            Presence = presence;
        }

        public string PlayerId { get; }
        public string Nickname { get; }
        public FriendPresence Presence { get; }
        public bool IsOnline => Presence != FriendPresence.Offline;
    }

    public sealed class FriendListSystem
    {
        private List<FriendSummary> onlineFriends = new List<FriendSummary>();
        private List<FriendSummary> offlineFriends = new List<FriendSummary>();

        public IReadOnlyList<FriendSummary> OnlineFriends => onlineFriends;
        public IReadOnlyList<FriendSummary> OfflineFriends => offlineFriends;

        public event Action FriendsChanged;

        public void ReplaceFriends(IEnumerable<FriendSummary> friends)
        {
            if (friends == null)
            {
                throw new ArgumentNullException(nameof(friends));
            }

            var nextOnlineFriends = new List<FriendSummary>();
            var nextOfflineFriends = new List<FriendSummary>();

            foreach (var friend in friends)
            {
                if (friend.IsOnline)
                {
                    nextOnlineFriends.Add(friend);
                }
                else
                {
                    nextOfflineFriends.Add(friend);
                }
            }

            onlineFriends = nextOnlineFriends;
            offlineFriends = nextOfflineFriends;
            FriendsChanged?.Invoke();
        }
    }
}
