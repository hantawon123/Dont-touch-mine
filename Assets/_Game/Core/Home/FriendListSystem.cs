using System;
using System.Collections.Generic;
using Game.Core.Players;

namespace Game.Core.Home
{
    public enum FriendPresence
    {
        Offline,
        Online,
        InGame,

        /// <summary>
        /// Signed in to Steam but not in this game. Offline as far as playing
        /// together goes, which is why it is listed under 오프라인, but worth
        /// telling apart: this friend is at the keyboard.
        /// </summary>
        SteamOnline,

        /// <summary>
        /// In a room, waiting for the match to start. The server's IN_LOBBY.
        /// </summary>
        /// <remarks>
        /// Online, and shown under 온라인. Kept apart from <see cref="InGame"/>
        /// because a friend in a lobby is about to be busy rather than busy, and
        /// the screen may want to say so. Inviting either is refused by the
        /// server: the toast only appears at home, so neither would see it.
        /// <para>
        /// Appended rather than placed beside <see cref="InGame"/> so the numbers
        /// the existing members carry do not shift.
        /// </para>
        /// </remarks>
        InLobby
    }

    public readonly struct FriendSummary
    {
        public FriendSummary(
            string playerId,
            string nickname,
            FriendPresence presence,
            bool appearanceSet = false,
            AvatarAppearance appearance = default)
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
            AppearanceSet = appearanceSet;
            Appearance = appearanceSet ? appearance : default;
        }

        public string PlayerId { get; }
        public string Nickname { get; }
        public FriendPresence Presence { get; }

        /// <summary>
        /// Whether <see cref="Appearance"/> is the look they last saved. The
        /// server sends this even when they are offline, so the friend list
        /// can keep showing that face.
        /// </summary>
        public bool AppearanceSet { get; }

        public AvatarAppearance Appearance { get; }

        /// <summary>
        /// In this game, which is what the 온라인 section means. A friend who
        /// is only on Steam is not one of these.
        /// </summary>
        public bool IsOnline =>
            Presence == FriendPresence.Online
            || Presence == FriendPresence.InLobby
            || Presence == FriendPresence.InGame;
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
                if (friend.AppearanceSet)
                {
                    AvatarAppearanceBoard.Remember(friend.PlayerId, friend.Appearance);
                }

                if (friend.IsOnline)
                {
                    nextOnlineFriends.Add(friend);
                }
                else
                {
                    nextOfflineFriends.Add(friend);
                }
            }

            // Hangul, then Latin, then digits, as the design asks. The offline
            // half is grouped before that so the friends who are at least on
            // Steam come first.
            nextOnlineFriends.Sort(CompareByName);
            nextOfflineFriends.Sort(CompareOffline);

            onlineFriends = nextOnlineFriends;
            offlineFriends = nextOfflineFriends;
            FriendsChanged?.Invoke();
        }


        private static int CompareByName(FriendSummary left, FriendSummary right)
        {
            return FriendNameComparer.Instance.Compare(left.Nickname, right.Nickname);
        }

        private static int CompareOffline(FriendSummary left, FriendSummary right)
        {
            var byPresence = OfflineRank(left).CompareTo(OfflineRank(right));
            return byPresence != 0 ? byPresence : CompareByName(left, right);
        }

        private static int OfflineRank(FriendSummary friend)
        {
            return friend.Presence == FriendPresence.SteamOnline ? 0 : 1;
        }
    }
}
