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
            var profile = new PlayerProfile("사용자닉네임");
            var menu = new HomeMenuSystem();
            var view = new FakeHomeMenuView();
            var host = new FakeHomeApplicationHost();
            var appFlow = new AppFlowSystem();
            var friends = new FriendListSystem();
            var search = new FriendSearchSystem();
            var requestedActions = new List<HomeMenuAction>();
            menu.ActionRequested += requestedActions.Add;

            using (var presenter = new HomeMenuPresenter(profile, menu, view, host, appFlow, friends, search))
            {
                presenter.Start();
                Assert.That(view.Nickname, Is.EqualTo("사용자닉네임"));

                Assert.That(profile.TryChangeNickname("새닉네임", out _), Is.True);
                Assert.That(view.Nickname, Is.EqualTo("새닉네임"));

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
            using var presenter = CreateStartedPresenter(out var view, out var host, out var appFlow, out _, out _);
            view.Raise(HomeMenuAction.FindRoom);

            Assert.That(appFlow.CurrentState, Is.EqualTo(AppFlowState.RoomBrowser));
            Assert.That(host.RoomBrowserOpenCount, Is.EqualTo(1));
            Assert.That(host.HomeOpenCount, Is.Zero);
            Assert.That(host.QuitCount, Is.Zero);
        }

        [Test]
        public void Presenter_FriendsAction_ShowsFriendListPanel()
        {
            using var presenter = CreateStartedPresenter(out var view, out _, out _, out _, out _);

            Assert.That(view.FriendListVisible, Is.False);
            Assert.That(view.OnlineFriends, Is.Empty);
            Assert.That(view.OfflineFriends, Is.Empty);

            view.Raise(HomeMenuAction.Friends);
            Assert.That(view.FriendListVisible, Is.True);
            Assert.That(view.FriendSearchVisible, Is.False);

            view.Raise(HomeMenuAction.Friends);
            Assert.That(view.FriendListVisible, Is.True);
            Assert.That(view.FriendSearchVisible, Is.False);
        }

        [Test]
        public void Presenter_CannotShowSearchUntilFriendListIsOpen()
        {
            using var presenter = CreateStartedPresenter(out var view, out _, out _, out _, out _);

            view.RaiseFriendSearchOpened();

            Assert.That(view.FriendListVisible, Is.False);
            Assert.That(view.FriendSearchVisible, Is.False);
        }

        [Test]
        public void Presenter_BindsFriendListAndUpdatesWhenFriendsChange()
        {
            using var presenter = CreateStartedPresenter(out var view, out _, out _, out var friends, out _);

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
            using var presenter = CreateStartedPresenter(out var view, out _, out _, out _, out _);
            view.Raise(HomeMenuAction.Friends);
            Assert.That(view.FriendListVisible, Is.True);

            view.RaiseFriendListDismissed();

            Assert.That(view.FriendListVisible, Is.False);
            Assert.That(view.FriendSearchVisible, Is.False);
        }

        [Test]
        public void Presenter_FindRoom_HidesOpenFriendList()
        {
            using var presenter = CreateStartedPresenter(out var view, out var host, out _, out _, out _);
            view.Raise(HomeMenuAction.Friends);
            Assert.That(view.FriendListVisible, Is.True);

            view.Raise(HomeMenuAction.FindRoom);

            Assert.That(view.FriendListVisible, Is.False);
            Assert.That(view.FriendSearchVisible, Is.False);
            Assert.That(view.ProfileSettingsVisible, Is.False);
            Assert.That(host.RoomBrowserOpenCount, Is.EqualTo(1));
        }

        [Test]
        public void Presenter_ProfileSettings_ShowsOverlayAndBackHidesIt()
        {
            using var presenter = CreateStartedPresenter(out var view, out _, out _, out _, out _);

            Assert.That(view.ProfileSettingsVisible, Is.False);
            Assert.That(view.NicknameAppliedFeedbackVisible, Is.False);

            view.Raise(HomeMenuAction.ProfileSettings);
            Assert.That(view.ProfileSettingsVisible, Is.True);
            Assert.That(view.FriendListVisible, Is.False);
            Assert.That(view.Nickname, Is.EqualTo("사용자닉네임"));
            Assert.That(view.NicknameAppliedFeedbackVisible, Is.False);

            view.Raise(HomeMenuAction.ProfileSettings);
            Assert.That(view.ProfileSettingsVisible, Is.True);

            view.RaiseProfileSettingsDismissed();
            Assert.That(view.ProfileSettingsVisible, Is.False);
            Assert.That(view.NicknameAppliedFeedbackVisible, Is.False);
        }

        [Test]
        public void Presenter_FriendsAndProfileSettings_ReplaceEachOther()
        {
            using var presenter = CreateStartedPresenter(out var view, out _, out _, out _, out _);

            view.Raise(HomeMenuAction.Friends);
            Assert.That(view.FriendListVisible, Is.True);

            view.Raise(HomeMenuAction.ProfileSettings);
            Assert.That(view.ProfileSettingsVisible, Is.True);
            Assert.That(view.FriendListVisible, Is.False);

            view.Raise(HomeMenuAction.Friends);
            Assert.That(view.FriendListVisible, Is.True);
            Assert.That(view.ProfileSettingsVisible, Is.False);
        }

        [Test]
        public void Presenter_FindRoom_HidesOpenProfileSettings()
        {
            using var presenter = CreateStartedPresenter(out var view, out var host, out _, out _, out _);
            view.Raise(HomeMenuAction.ProfileSettings);
            Assert.That(view.ProfileSettingsVisible, Is.True);

            view.Raise(HomeMenuAction.FindRoom);

            Assert.That(view.ProfileSettingsVisible, Is.False);
            Assert.That(view.NicknameAppliedFeedbackVisible, Is.False);
            Assert.That(host.RoomBrowserOpenCount, Is.EqualTo(1));
        }

        [Test]
        public void Presenter_ChangeNickname_ShowsAppliedFeedbackUntilTextDiffers()
        {
            using var presenter = CreateStartedPresenter(out var view, out _, out _, out _, out _);
            view.Raise(HomeMenuAction.ProfileSettings);

            view.RaiseNicknameChangeRequested("새닉네임");
            Assert.That(view.Nickname, Is.EqualTo("새닉네임"));
            Assert.That(view.NicknameAppliedFeedbackVisible, Is.True);

            view.RaiseNicknameEdited("새닉네임");
            Assert.That(view.NicknameAppliedFeedbackVisible, Is.True);

            view.RaiseNicknameEdited("새닉네임 ");
            Assert.That(view.NicknameAppliedFeedbackVisible, Is.False);

            view.RaiseNicknameChangeRequested("새닉네임");
            Assert.That(view.NicknameAppliedFeedbackVisible, Is.True);

            view.RaiseNicknameEdited("새닉네임!");
            Assert.That(view.NicknameAppliedFeedbackVisible, Is.False);
            Assert.That(view.Nickname, Is.EqualTo("새닉네임"));
        }

        [Test]
        public void Presenter_EmptyNickname_DoesNotShowAppliedFeedback()
        {
            using var presenter = CreateStartedPresenter(out var view, out _, out _, out _, out _);
            view.Raise(HomeMenuAction.ProfileSettings);

            view.RaiseNicknameChangeRequested(" ");
            Assert.That(view.Nickname, Is.EqualTo("사용자닉네임"));
            Assert.That(view.NicknameAppliedFeedbackVisible, Is.False);
        }

        [Test]
        public void Presenter_CannotChangeNicknameUntilProfileSettingsIsOpen()
        {
            using var presenter = CreateStartedPresenter(out var view, out _, out _, out _, out _);

            view.RaiseNicknameChangeRequested("해킹닉네임");

            Assert.That(view.Nickname, Is.EqualTo("사용자닉네임"));
            Assert.That(view.NicknameAppliedFeedbackVisible, Is.False);
            Assert.That(view.ProfileSettingsVisible, Is.False);
        }

        [Test]
        public void Presenter_ReopeningProfileSettings_HidesPreviousAppliedFeedback()
        {
            using var presenter = CreateStartedPresenter(out var view, out _, out _, out _, out _);
            view.Raise(HomeMenuAction.ProfileSettings);
            view.RaiseNicknameChangeRequested("새닉네임");
            Assert.That(view.NicknameAppliedFeedbackVisible, Is.True);

            view.RaiseProfileSettingsDismissed();
            view.Raise(HomeMenuAction.ProfileSettings);

            Assert.That(view.ProfileSettingsVisible, Is.True);
            Assert.That(view.NicknameAppliedFeedbackVisible, Is.False);
            Assert.That(view.Nickname, Is.EqualTo("새닉네임"));
        }

        [Test]
        public void Presenter_ChangeNicknameAgain_ShowsAppliedFeedback()
        {
            using var presenter = CreateStartedPresenter(out var view, out _, out _, out _, out _);
            view.Raise(HomeMenuAction.ProfileSettings);
            view.RaiseNicknameChangeRequested("새닉네임");
            view.RaiseNicknameEdited("다른닉");
            Assert.That(view.NicknameAppliedFeedbackVisible, Is.False);

            view.RaiseNicknameChangeRequested("다른닉");
            Assert.That(view.Nickname, Is.EqualTo("다른닉"));
            Assert.That(view.NicknameAppliedFeedbackVisible, Is.True);
        }

        [Test]
        public void Presenter_OpenSearch_SwitchesPanelToSearchThenCloseReturnsToList()
        {
            using var presenter = CreateStartedPresenter(out var view, out _, out _, out _, out _);
            view.Raise(HomeMenuAction.Friends);
            view.RaiseFriendSearchOpened();

            Assert.That(view.FriendListVisible, Is.True);
            Assert.That(view.FriendSearchVisible, Is.True);

            view.RaiseFriendSearchClosed();

            Assert.That(view.FriendListVisible, Is.True);
            Assert.That(view.FriendSearchVisible, Is.False);
        }

        [Test]
        public void Presenter_SearchAndRequest_BindsResultsAndMarksPending()
        {
            using var presenter = CreateStartedPresenter(out var view, out _, out _, out var friends, out var search);
            friends.ReplaceFriends(new[]
            {
                new FriendSummary("player-1", "친구1", FriendPresence.Online)
            });
            search.ReplaceDirectory(new[]
            {
                new FriendSummary("player-1", "친구1", FriendPresence.Online),
                new FriendSummary("player-2", "검색유저", FriendPresence.Online)
            });

            view.Raise(HomeMenuAction.Friends);
            view.RaiseFriendSearchOpened();
            view.RaiseFriendSearchRequested("검색");

            Assert.That(view.SearchResults.Count, Is.EqualTo(1));
            Assert.That(view.SearchResults[0].Nickname, Is.EqualTo("검색유저"));
            Assert.That(view.SearchResults[0].IsPending, Is.False);

            // The presenter does not answer this click any more. Marking the row
            // belongs to the command that sends the request, because doing both
            // left the command looking at a row that already said it was waiting
            // and dropping the request.
            view.RaiseFriendRequestClicked("player-2");

            Assert.That(view.SearchResults[0].IsPending, Is.False);
        }

        [Test]
        public void Presenter_RequiresDependencies()
        {
            var profile = new PlayerProfile("사용자닉네임");
            var menu = new HomeMenuSystem();
            var view = new FakeHomeMenuView();
            var host = new FakeHomeApplicationHost();
            var appFlow = new AppFlowSystem();
            var friends = new FriendListSystem();
            var search = new FriendSearchSystem();

            Assert.That(
                () => new HomeMenuPresenter(null, menu, view, host, appFlow, friends, search),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new HomeMenuPresenter(profile, null, view, host, appFlow, friends, search),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new HomeMenuPresenter(profile, menu, null, host, appFlow, friends, search),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new HomeMenuPresenter(profile, menu, view, null, appFlow, friends, search),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new HomeMenuPresenter(profile, menu, view, host, null, friends, search),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new HomeMenuPresenter(profile, menu, view, host, appFlow, null, search),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new HomeMenuPresenter(profile, menu, view, host, appFlow, friends, null),
                Throws.TypeOf<ArgumentNullException>());
        }

        private static HomeMenuPresenter CreateStartedPresenter(
            out FakeHomeMenuView view,
            out FakeHomeApplicationHost host,
            out AppFlowSystem appFlow,
            out FriendListSystem friends,
            out FriendSearchSystem search)
        {
            var profile = new PlayerProfile("사용자닉네임");
            var menu = new HomeMenuSystem();
            view = new FakeHomeMenuView();
            host = new FakeHomeApplicationHost();
            appFlow = new AppFlowSystem();
            friends = new FriendListSystem();
            search = new FriendSearchSystem();
            var presenter = new HomeMenuPresenter(profile, menu, view, host, appFlow, friends, search);
            presenter.Start();
            return presenter;
        }

        private sealed class FakeHomeMenuView : IHomeMenuView
        {
            public string Nickname { get; private set; }

            public bool FriendListVisible { get; private set; }

            public bool FriendSearchVisible { get; private set; }

            public IReadOnlyList<FriendSummary> OnlineFriends { get; private set; } =
                Array.Empty<FriendSummary>();

            public IReadOnlyList<FriendSummary> OfflineFriends { get; private set; } =
                Array.Empty<FriendSummary>();

            public IReadOnlyList<FriendSearchHit> SearchResults { get; private set; } =
                Array.Empty<FriendSearchHit>();

            public bool ProfileSettingsVisible { get; private set; }

            public bool NicknameAppliedFeedbackVisible { get; private set; }

            public event Action<HomeMenuAction> ActionClicked;

            public event Action FriendListDismissed;

            public event Action ProfileSettingsDismissed;

            public event Action<string> NicknameChangeRequested;

            public event Action<string> NicknameEdited;

            public event Action FriendSearchOpened;

            public event Action FriendSearchClosed;

            public event Action<string> FriendSearchRequested;

            public event Action<string> FriendRequestClicked;

            public event Action<string> FriendRequestAccepted;

            public event Action<string> FriendRequestDeclined;

            public event Action<string> FriendRequestCancelled;

            public event Action FriendListRefreshRequested;

            public event Action<string> FriendRemoved;

            public event Action<string> FriendBlocked;

            public IReadOnlyList<FriendRequestSummary> IncomingRequests { get; private set; } =
                Array.Empty<FriendRequestSummary>();

            public void SetIncomingRequests(IReadOnlyList<FriendRequestSummary> requests)
            {
                IncomingRequests = requests;
            }

            public IReadOnlyList<FriendRequestSummary> OutgoingRequests { get; private set; } =
                Array.Empty<FriendRequestSummary>();

            public string FriendActionError { get; private set; } = string.Empty;

            public void SetFriendActionError(string message)
            {
                FriendActionError = message ?? string.Empty;
            }

            public void SetOutgoingRequests(IReadOnlyList<FriendRequestSummary> requests)
            {
                OutgoingRequests = requests;
            }

            public void RaiseFriendRequestAccepted(string playerId)
            {
                FriendRequestAccepted?.Invoke(playerId);
            }

            public void RaiseFriendRequestDeclined(string playerId)
            {
                FriendRequestDeclined?.Invoke(playerId);
            }

            public void SetNickname(string nickname)
            {
                Nickname = nickname;
            }

            public void SetProfileSettingsVisible(bool visible)
            {
                ProfileSettingsVisible = visible;
            }

            public string NicknameError { get; private set; } = string.Empty;

            public void SetNicknameError(string message)
            {
                NicknameError = message ?? string.Empty;
            }

            public void SetNicknameAppliedFeedbackVisible(bool visible)
            {
                NicknameAppliedFeedbackVisible = visible;
            }

            public void SetFriendListVisible(bool visible)
            {
                FriendListVisible = visible;
                if (!visible)
                {
                    FriendSearchVisible = false;
                }
            }

            public void SetFriends(
                IReadOnlyList<FriendSummary> onlineFriends,
                IReadOnlyList<FriendSummary> offlineFriends)
            {
                OnlineFriends = onlineFriends;
                OfflineFriends = offlineFriends;
            }

            public void SetFriendSearchVisible(bool visible)
            {
                if (visible && !FriendListVisible)
                {
                    return;
                }

                FriendSearchVisible = visible;
            }

            public void SetFriendSearchResults(IReadOnlyList<FriendSearchHit> results)
            {
                SearchResults = results;
            }

            public void Raise(HomeMenuAction action)
            {
                ActionClicked?.Invoke(action);
            }

            public void RaiseFriendListDismissed()
            {
                FriendListDismissed?.Invoke();
            }

            public void RaiseProfileSettingsDismissed()
            {
                ProfileSettingsDismissed?.Invoke();
            }

            public void RaiseNicknameChangeRequested(string nickname)
            {
                NicknameChangeRequested?.Invoke(nickname);
            }

            public void RaiseNicknameEdited(string nickname)
            {
                NicknameEdited?.Invoke(nickname);
            }

            public void RaiseFriendSearchOpened()
            {
                FriendSearchOpened?.Invoke();
            }

            public void RaiseFriendSearchClosed()
            {
                FriendSearchClosed?.Invoke();
            }

            public void RaiseFriendSearchRequested(string query)
            {
                FriendSearchRequested?.Invoke(query);
            }

            public void RaiseFriendRequestClicked(string playerId)
            {
                FriendRequestClicked?.Invoke(playerId);
            }
        }

        private sealed class FakeHomeApplicationHost : IHomeApplicationHost
        {
            public int QuitCount { get; private set; }

            public int HomeOpenCount { get; private set; }

            public int RoomBrowserOpenCount { get; private set; }

            public int LobbyOpenCount { get; private set; }

            public void OpenLobby()
            {
                LobbyOpenCount++;
            }

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
