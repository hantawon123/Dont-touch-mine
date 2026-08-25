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
        public void PlayerProfile_StoresAndUpdatesNicknameAndLevel()
        {
            var profile = new PlayerProfile(" 사용자 ", 1);
            var changedCount = 0;
            profile.Changed += _ => changedCount++;

            Assert.That(
                profile.TryChangeNickname(" 새닉네임 ", out var error),
                Is.True);
            Assert.That(profile.TryUpdateLevel(2, out error), Is.True);
            Assert.That(error, Is.EqualTo(PlayerProfileError.None));
            Assert.That(profile.Nickname, Is.EqualTo("새닉네임"));
            Assert.That(profile.Level, Is.EqualTo(2));
            Assert.That(changedCount, Is.EqualTo(2));

            Assert.That(
                profile.TryChangeNickname(" ", out error),
                Is.False);
            Assert.That(error, Is.EqualTo(PlayerProfileError.NicknameRequired));
            Assert.That(profile.Nickname, Is.EqualTo("새닉네임"));
            Assert.That(profile.Level, Is.EqualTo(2));

            Assert.That(profile.TryUpdateLevel(0, out error), Is.False);
            Assert.That(error, Is.EqualTo(PlayerProfileError.InvalidLevel));
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
    }
}
