using System;
using System.Collections.Generic;
using Game.Client.Home;
using Game.Core.Flow;
using Game.Core.Home;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class HomeMenuPresenterTests
    {
        [Test]
        public void Presenter_BindsProfileAndForwardsEveryMenuAction()
        {
            var profile = new PlayerProfile("사용자닉네임", 1);
            var menu = new HomeMenuSystem();
            var view = new FakeHomeMenuView();
            var host = new FakeHomeApplicationHost();
            var appFlow = new AppFlowSystem();
            var friends = new FriendListSystem();
            var requestedActions = new List<HomeMenuAction>();
            menu.ActionRequested += requestedActions.Add;

            using (var presenter = new HomeMenuPresenter(profile, menu, view, host, appFlow, friends))
            {
                presenter.Start();
                Assert.That(view.Nickname, Is.EqualTo("사용자닉네임"));
                Assert.That(view.Level, Is.EqualTo(1));

                Assert.That(profile.TryChangeNickname("새닉네임", out _), Is.True);
                Assert.That(profile.TryUpdateLevel(3, out _), Is.True);
                Assert.That(view.Nickname, Is.EqualTo("새닉네임"));
                Assert.That(view.Level, Is.EqualTo(3));

                foreach (HomeMenuAction action in Enum.GetValues(typeof(HomeMenuAction)))
                {
                    view.Raise(action);
                }
            }

            Assert.That(
                requestedActions,
                Is.EqualTo(Enum.GetValues(typeof(HomeMenuAction))));
            Assert.That(host.QuitCount, Is.EqualTo(1));
            Assert.That(host.RoomBrowserOpenCount, Is.EqualTo(1));
            Assert.That(appFlow.CurrentState, Is.EqualTo(AppFlowState.RoomBrowser));

            view.Raise(HomeMenuAction.FindRoom);
            Assert.That(requestedActions.Count, Is.EqualTo(Enum.GetValues(typeof(HomeMenuAction)).Length));
            Assert.That(host.QuitCount, Is.EqualTo(1));
            Assert.That(host.RoomBrowserOpenCount, Is.EqualTo(1));
        }

        [Test]
        public void Presenter_FindRoom_OpensRoomBrowser()
        {
            var profile = new PlayerProfile("사용자닉네임", 1);
            var menu = new HomeMenuSystem();
            var view = new FakeHomeMenuView();
            var host = new FakeHomeApplicationHost();
            var appFlow = new AppFlowSystem();
            var friends = new FriendListSystem();

            using var presenter = new HomeMenuPresenter(profile, menu, view, host, appFlow, friends);
            presenter.Start();
            view.Raise(HomeMenuAction.FindRoom);

            Assert.That(appFlow.CurrentState, Is.EqualTo(AppFlowState.RoomBrowser));
            Assert.That(host.RoomBrowserOpenCount, Is.EqualTo(1));
            Assert.That(host.HomeOpenCount, Is.Zero);
            Assert.That(host.QuitCount, Is.Zero);
        }

        [Test]
        public void Presenter_FriendsAction_ShowsFriendListPanel()
        {
            var profile = new PlayerProfile("사용자닉네임", 1);
            var menu = new HomeMenuSystem();
            var view = new FakeHomeMenuView();
            var host = new FakeHomeApplicationHost();
            var appFlow = new AppFlowSystem();
            var friends = new FriendListSystem();

            using var presenter = new HomeMenuPresenter(profile, menu, view, host, appFlow, friends);
            presenter.Start();

            Assert.That(view.FriendListVisible, Is.False);
            Assert.That(view.OnlineFriends, Is.Empty);
            Assert.That(view.OfflineFriends, Is.Empty);

            view.Raise(HomeMenuAction.Friends);
            Assert.That(view.FriendListVisible, Is.True);

            view.Raise(HomeMenuAction.Friends);
            Assert.That(view.FriendListVisible, Is.True);
        }

        [Test]
        public void Presenter_BindsFriendListAndUpdatesWhenFriendsChange()
        {
            var profile = new PlayerProfile("사용자닉네임", 1);
            var menu = new HomeMenuSystem();
            var view = new FakeHomeMenuView();
            var host = new FakeHomeApplicationHost();
            var appFlow = new AppFlowSystem();
            var friends = new FriendListSystem();

            using var presenter = new HomeMenuPresenter(profile, menu, view, host, appFlow, friends);
            presenter.Start();

            friends.ReplaceFriends(new[]
            {
                new FriendSummary("player-1", "친구1", FriendPresence.InGame),
                new FriendSummary("player-2", "친구2", FriendPresence.Online),
                new FriendSummary("player-3", "친구3", FriendPresence.Offline)
            });

            Assert.That(view.OnlineFriends.Count, Is.EqualTo(2));
            Assert.That(view.OnlineFriends[0].Nickname, Is.EqualTo("친구1"));
            Assert.That(view.OnlineFriends[0].Presence, Is.EqualTo(FriendPresence.InGame));
            Assert.That(view.OnlineFriends[1].Nickname, Is.EqualTo("친구2"));
            Assert.That(view.OfflineFriends.Count, Is.EqualTo(1));
            Assert.That(view.OfflineFriends[0].Nickname, Is.EqualTo("친구3"));

            friends.ReplaceFriends(new[]
            {
                new FriendSummary("player-4", "친구4", FriendPresence.Offline)
            });

            Assert.That(view.OnlineFriends, Is.Empty);
            Assert.That(view.OfflineFriends.Count, Is.EqualTo(1));
            Assert.That(view.OfflineFriends[0].Nickname, Is.EqualTo("친구4"));
        }

        [Test]
        public void Presenter_ClickOutsideFriendList_HidesPanel()
        {
            var profile = new PlayerProfile("사용자닉네임", 1);
            var menu = new HomeMenuSystem();
            var view = new FakeHomeMenuView();
            var host = new FakeHomeApplicationHost();
            var appFlow = new AppFlowSystem();
            var friends = new FriendListSystem();

            using var presenter = new HomeMenuPresenter(profile, menu, view, host, appFlow, friends);
            presenter.Start();
            view.Raise(HomeMenuAction.Friends);
            Assert.That(view.FriendListVisible, Is.True);

            view.RaiseFriendListDismissed();

            Assert.That(view.FriendListVisible, Is.False);
        }

        [Test]
        public void Presenter_FindRoom_HidesOpenFriendList()
        {
            var profile = new PlayerProfile("사용자닉네임", 1);
            var menu = new HomeMenuSystem();
            var view = new FakeHomeMenuView();
            var host = new FakeHomeApplicationHost();
            var appFlow = new AppFlowSystem();
            var friends = new FriendListSystem();

            using var presenter = new HomeMenuPresenter(profile, menu, view, host, appFlow, friends);
            presenter.Start();
            view.Raise(HomeMenuAction.Friends);
            Assert.That(view.FriendListVisible, Is.True);

            view.Raise(HomeMenuAction.FindRoom);

            Assert.That(view.FriendListVisible, Is.False);
            Assert.That(host.RoomBrowserOpenCount, Is.EqualTo(1));
        }

        [Test]
        public void Presenter_RequiresDependencies()
        {
            var profile = new PlayerProfile("사용자닉네임", 1);
            var menu = new HomeMenuSystem();
            var view = new FakeHomeMenuView();
            var host = new FakeHomeApplicationHost();
            var appFlow = new AppFlowSystem();
            var friends = new FriendListSystem();

            Assert.That(
                () => new HomeMenuPresenter(null, menu, view, host, appFlow, friends),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new HomeMenuPresenter(profile, null, view, host, appFlow, friends),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new HomeMenuPresenter(profile, menu, null, host, appFlow, friends),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new HomeMenuPresenter(profile, menu, view, null, appFlow, friends),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new HomeMenuPresenter(profile, menu, view, host, null, friends),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new HomeMenuPresenter(profile, menu, view, host, appFlow, null),
                Throws.TypeOf<ArgumentNullException>());
        }

        private sealed class FakeHomeMenuView : IHomeMenuView
        {
            public string Nickname { get; private set; }

            public int Level { get; private set; }

            public bool FriendListVisible { get; private set; }

            public IReadOnlyList<FriendSummary> OnlineFriends { get; private set; } =
                Array.Empty<FriendSummary>();

            public IReadOnlyList<FriendSummary> OfflineFriends { get; private set; } =
                Array.Empty<FriendSummary>();

            public event Action<HomeMenuAction> ActionClicked;

            public event Action FriendListDismissed;

            public void SetNickname(string nickname)
            {
                Nickname = nickname;
            }

            public void SetLevel(int level)
            {
                Level = level;
            }

            public void SetFriendListVisible(bool visible)
            {
                FriendListVisible = visible;
            }

            public void SetFriends(
                IReadOnlyList<FriendSummary> onlineFriends,
                IReadOnlyList<FriendSummary> offlineFriends)
            {
                OnlineFriends = onlineFriends;
                OfflineFriends = offlineFriends;
            }

            public void Raise(HomeMenuAction action)
            {
                ActionClicked?.Invoke(action);
            }

            public void RaiseFriendListDismissed()
            {
                FriendListDismissed?.Invoke();
            }
        }

        private sealed class FakeHomeApplicationHost : IHomeApplicationHost
        {
            public int QuitCount { get; private set; }

            public int HomeOpenCount { get; private set; }

            public int RoomBrowserOpenCount { get; private set; }

            public void Quit()
            {
                QuitCount++;
            }

            public void OpenHome()
            {
                HomeOpenCount++;
            }

            public void OpenRoomBrowser()
            {
                RoomBrowserOpenCount++;
            }
        }
    }
}
