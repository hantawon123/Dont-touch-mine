using System;
using Game.Core.Home;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class HomeProfileFriendSystemTests
    {
        [Test]
        public void HomeMenu_ForwardsEveryButtonAction()
        {
            var menu = new HomeMenuSystem();
            var requestedActions = new System.Collections.Generic.List<HomeMenuAction>();
            menu.ActionRequested += requestedActions.Add;

            foreach (HomeMenuAction action in Enum.GetValues(typeof(HomeMenuAction)))
            {
                menu.Request(action);
            }

            Assert.That(
                requestedActions,
                Is.EqualTo(Enum.GetValues(typeof(HomeMenuAction))));
            Assert.That(
                () => menu.Request((HomeMenuAction)999),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void PlayerProfile_StoresAndUpdatesNickname()
        {
            var profile = new PlayerProfile(" 사용자 ");
            var changedCount = 0;
            profile.Changed += _ => changedCount++;

            Assert.That(
                profile.TryChangeNickname(" 새닉네임 ", out var error),
                Is.True);
            Assert.That(error, Is.EqualTo(PlayerProfileError.None));
            Assert.That(profile.Nickname, Is.EqualTo("새닉네임"));
            Assert.That(changedCount, Is.EqualTo(1));

            Assert.That(
                profile.TryChangeNickname(" ", out error),
                Is.False);
            Assert.That(error, Is.EqualTo(PlayerProfileError.NicknameRequired));
            Assert.That(profile.Nickname, Is.EqualTo("새닉네임"), "a blank name changes nothing");
            Assert.That(changedCount, Is.EqualTo(1), "and tells nobody");
        }

        [Test]
        public void FriendList_GroupsOnlineAndOfflineFriends()
        {
            var friendList = new FriendListSystem();
            var changedCount = 0;
            friendList.FriendsChanged += () => changedCount++;

            friendList.ReplaceFriends(new[]
            {
                new FriendSummary("player-1", "친구1", FriendPresence.InGame),
                new FriendSummary("player-2", "친구2", FriendPresence.Online),
                new FriendSummary("player-3", "친구3", FriendPresence.Offline)
            });

            Assert.That(friendList.OnlineFriends.Count, Is.EqualTo(2));
            Assert.That(friendList.OnlineFriends[0].Presence, Is.EqualTo(FriendPresence.InGame));
            Assert.That(friendList.OfflineFriends.Count, Is.EqualTo(1));
            Assert.That(friendList.OfflineFriends[0].Nickname, Is.EqualTo("친구3"));
            Assert.That(changedCount, Is.EqualTo(1));
            Assert.That(
                () => friendList.ReplaceFriends(null),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void FriendSearch_FindsUsersAndSendsPendingRequest()
        {
            var search = new FriendSearchSystem();
            var changedCount = 0;
            search.ResultsChanged += () => changedCount++;
            search.ReplaceDirectory(new[]
            {
                new FriendSummary("player-1", "친구1", FriendPresence.Online),
                new FriendSummary("player-2", "검색유저", FriendPresence.Offline)
            });

            search.Search("검색", new[] { "player-1" });
            Assert.That(search.Results.Count, Is.EqualTo(1));
            Assert.That(search.Results[0].Nickname, Is.EqualTo("검색유저"));
            Assert.That(search.Results[0].IsPending, Is.False);

            Assert.That(search.TrySendRequest("player-2"), Is.True);
            Assert.That(search.Results[0].IsPending, Is.True);
            Assert.That(search.TrySendRequest("player-2"), Is.False);
            Assert.That(search.TrySendRequest("player-1"), Is.False);

            search.ClearResults();
            Assert.That(search.Results, Is.Empty);
            Assert.That(changedCount, Is.GreaterThan(0));
            Assert.That(
                () => search.ReplaceDirectory(null),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => search.Search("검색", null),
                Throws.TypeOf<ArgumentNullException>());
        }
    }
}
