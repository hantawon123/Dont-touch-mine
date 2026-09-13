using System;
using System.Collections.Generic;
using Game.Client.Home;
using Game.Core.Flow;
using Game.Core.Home;
using Game.Core.Ports;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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

            using (var presenter = new HomeMenuPresenter(
                profile, menu, view, host, appFlow, friends, search,
                new ServerRegionSystem(new InMemoryServerRegionStore())))
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
        public void Presenter_Settings_OpensSettingsScreen()
        {
            using var presenter = CreateStartedPresenter(out var view, out var host, out var appFlow, out _, out _);
            view.Raise(HomeMenuAction.Settings);

            Assert.That(appFlow.CurrentState, Is.EqualTo(AppFlowState.Settings));
            Assert.That(host.SettingsOpenCount, Is.EqualTo(1));
        }

        [Test]
        public void Presenter_Settings_IsRefusedAwayFromHome()
        {
            using var presenter = CreateStartedPresenter(out var view, out var host, out var appFlow, out _, out _);
            view.Raise(HomeMenuAction.FindRoom);

            view.Raise(HomeMenuAction.Settings);

            Assert.That(appFlow.CurrentState, Is.EqualTo(AppFlowState.RoomBrowser));
            Assert.That(host.SettingsOpenCount, Is.EqualTo(0));
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

            // The button that opened the panel closes it again. It used to
            // re-open what was already open, so the only way out was to press
            // something else.
            view.Raise(HomeMenuAction.Friends);
            Assert.That(view.FriendListVisible, Is.False);
            Assert.That(view.FriendSearchVisible, Is.False);

            view.Raise(HomeMenuAction.Friends);
            Assert.That(view.FriendListVisible, Is.True);
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

            view.Raise(HomeMenuAction.ProfileSettings);
            Assert.That(view.ProfileSettingsVisible, Is.True);
            Assert.That(view.FriendListVisible, Is.False);
            Assert.That(view.Nickname, Is.EqualTo("사용자닉네임"));

            // Pressing the chip again closes it, as pressing the globe again
            // closes the region panel.
            view.Raise(HomeMenuAction.ProfileSettings);
            Assert.That(view.ProfileSettingsVisible, Is.False);

            view.Raise(HomeMenuAction.ProfileSettings);
            Assert.That(view.ProfileSettingsVisible, Is.True);

            view.RaiseProfileSettingsDismissed();
            Assert.That(view.ProfileSettingsVisible, Is.False);
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
            Assert.That(host.RoomBrowserOpenCount, Is.EqualTo(1));
        }

        [Test]
        public void Presenter_CannotChangeNicknameUntilProfileSettingsIsOpen()
        {
            using var presenter = CreateStartedPresenter(out var view, out _, out _, out _, out _);

            view.RaiseNicknameChangeRequested("해킹닉네임");

            Assert.That(view.Nickname, Is.EqualTo("사용자닉네임"));
            Assert.That(view.ProfileSettingsVisible, Is.False);
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
        public void Presenter_Search_BindsWhatTheSearchFound()
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
            view.RaiseFriendSearchRequested("검색유저");

            Assert.That(view.SearchResults.Count, Is.EqualTo(1));
            Assert.That(view.SearchResults[0].Nickname, Is.EqualTo("검색유저"));

            // Pressing 친구요청 is not checked here. The presenter deliberately
            // does not answer that click — HomeFriendBridge does, so the server
            // call and the mark happen together. Both places answering it was a
            // real bug: the row went to 요청 중 and nothing was ever sent.
            // FriendRequestClickTests puts the two together and covers it.
            Assert.That(view.SearchResults[0].IsPending, Is.False);
        }

        [Test]
        public void Presenter_ListTabSearch_NarrowsTheFriendsShown()
        {
            using var presenter = CreateStartedPresenter(out var view, out _, out _, out var friends, out _);
            friends.ReplaceFriends(new[]
            {
                new FriendSummary("p1", "가나다", FriendPresence.Online),
                new FriendSummary("p2", "나비야", FriendPresence.Online),
                new FriendSummary("p3", "다람쥐", FriendPresence.Offline)
            });
            view.Raise(HomeMenuAction.Friends);

            view.RaiseFriendSearchRequested("나");

            Assert.That(view.OnlineFriends.Count, Is.EqualTo(2), "가나다와 나비야가 남아야 한다.");
            Assert.That(view.OfflineFriends, Is.Empty);

            view.RaiseFriendSearchRequested(string.Empty);
            Assert.That(view.OnlineFriends.Count, Is.EqualTo(2));
            Assert.That(view.OfflineFriends.Count, Is.EqualTo(1));
        }

        [Test]
        public void Presenter_SwitchingTabs_DropsTheListFilter()
        {
            using var presenter = CreateStartedPresenter(out var view, out _, out _, out var friends, out _);
            friends.ReplaceFriends(new[]
            {
                new FriendSummary("p1", "가나다", FriendPresence.Online),
                new FriendSummary("p2", "다람쥐", FriendPresence.Online)
            });
            view.Raise(HomeMenuAction.Friends);
            view.RaiseFriendSearchRequested("가");
            Assert.That(view.OnlineFriends.Count, Is.EqualTo(1));

            // The box is shared and the view empties it on the way across, so a
            // filter left behind would hide friends nobody asked to hide.
            view.RaiseFriendSearchOpened();
            view.RaiseFriendSearchClosed();

            Assert.That(view.OnlineFriends.Count, Is.EqualTo(2));
        }

        [Test]
        public void Presenter_RequestTabSearch_MatchesTheWholeNicknameOnly()
        {
            using var presenter = CreateStartedPresenter(out var view, out _, out _, out _, out var search);
            search.ReplaceDirectory(new[]
            {
                new FriendSummary("p1", "금오산냥펀치", FriendPresence.Online),
                new FriendSummary("p2", "금오산냥옹2", FriendPresence.Online)
            });
            view.Raise(HomeMenuAction.Friends);
            view.RaiseFriendSearchOpened();

            view.RaiseFriendSearchRequested("금오산");
            Assert.That(view.SearchResults, Is.Empty, "부분 일치로는 아무도 나오면 안 된다.");

            view.RaiseFriendSearchRequested("금오산냥펀치");
            Assert.That(view.SearchResults.Count, Is.EqualTo(1));
            Assert.That(view.SearchResults[0].Nickname, Is.EqualTo("금오산냥펀치"));
        }

        [Test]
        public void Presenter_Refresh_RedrawsTheList()
        {
            using var presenter = CreateStartedPresenter(out var view, out _, out _, out var friends, out _);
            view.Raise(HomeMenuAction.Friends);
            Assert.That(view.OnlineFriends, Is.Empty);

            friends.ReplaceFriends(new[]
            {
                new FriendSummary("p1", "가나다", FriendPresence.Online)
            });
            view.RaiseRefresh();

            Assert.That(view.OnlineFriends.Count, Is.EqualTo(1));
        }

        [Test]
        public void Presenter_CreateRoom_OpensTheModalAndClosesTheRest()
        {
            using var presenter = CreateStartedPresenter(out var view, out _, out _, out _, out _);
            view.Raise(HomeMenuAction.Friends);

            view.Raise(HomeMenuAction.CreateRoom);

            Assert.That(view.CreateRoomVisible, Is.True);
            Assert.That(view.FriendListVisible, Is.False);
            Assert.That(view.ProfileSettingsVisible, Is.False);
            Assert.That(view.ServerSettingsVisible, Is.False);
        }

        [Test]
        public void Presenter_CreatingARoom_PassesTheFormOnAndClosesTheModal()
        {
            using var presenter = CreateStartedPresenter(out var view, out var host, out var appFlow, out _, out _);
            view.Raise(HomeMenuAction.CreateRoom);

            view.RaiseRoomCreationRequested("우리방", false, 4);

            Assert.That(host.CreatedTitle, Is.EqualTo("우리방"));
            Assert.That(host.CreatedPublic, Is.False);
            Assert.That(host.CreatedMaxPlayers, Is.EqualTo(4));
            Assert.That(view.CreateRoomVisible, Is.False);
        }

        [Test]
        public void Presenter_AskingForARoom_DoesNotMoveTheFlowYet()
        {
            using var presenter = CreateStartedPresenter(
                out var view, out _, out var appFlow, out _, out _);
            view.Raise(HomeMenuAction.CreateRoom);

            view.RaiseRoomCreationRequested("우리방", false, 4);

            // Opening a room is a call that can be refused, and there is no way
            // back from Lobby to Home. Moving here left a refused player sitting
            // on this screen while the app believed they were in a lobby, and
            // the flow could only go on to a room browser or a match from
            // there. The host moves it when a room actually opens.
            Assert.That(appFlow.CurrentState, Is.EqualTo(AppFlowState.Home));
        }

        [Test]
        public void Presenter_CreatingARoom_IsRefusedFromAStateThatCannotLeave()
        {
            using var presenter = CreateStartedPresenter(out var view, out var host, out var appFlow, out _, out _);

            // Highlight is the one state with no way on to a lobby: it only
            // goes to the result. InGame does allow it, for the rematch.
            Assert.That(appFlow.TryTransitionTo(AppFlowState.RoomBrowser), Is.True);
            Assert.That(appFlow.TryTransitionTo(AppFlowState.Lobby), Is.True);
            Assert.That(appFlow.TryTransitionTo(AppFlowState.InGame), Is.True);
            Assert.That(appFlow.TryTransitionTo(AppFlowState.Highlight), Is.True);

            // The refusal is logged on purpose, so the test says it expects one
            // rather than failing on it.
            LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex("Cannot open a room from Highlight"));
            view.RaiseRoomCreationRequested("우리방", true, 6);

            Assert.That(
                host.CreateCount,
                Is.Zero,
                "흐름이 허락하지 않는 곳에서 방을 열면 안 된다.");
            Assert.That(appFlow.CurrentState, Is.EqualTo(AppFlowState.Highlight));
        }

        [Test]
        public void Presenter_DismissingTheCreateRoomModal_ClosesIt()
        {
            using var presenter = CreateStartedPresenter(out var view, out var host, out _, out _, out _);
            view.Raise(HomeMenuAction.CreateRoom);

            view.RaiseCreateRoomDismissed();

            Assert.That(view.CreateRoomVisible, Is.False);
            Assert.That(host.CreateCount, Is.Zero);
        }

        [Test]
        public void Presenter_ServerSettings_TheGlobeOpensAndClosesIt()
        {
            using var presenter = CreateStartedPresenter(out var view, out _, out _, out _, out _);
            Assert.That(view.ServerSettingsVisible, Is.False);

            view.Raise(HomeMenuAction.ServerSettings);
            Assert.That(view.ServerSettingsVisible, Is.True);

            view.Raise(HomeMenuAction.ServerSettings);
            Assert.That(
                view.ServerSettingsVisible,
                Is.False,
                "지구본을 다시 눌러도 닫히지 않으면 패널을 닫을 방법이 없다.");
        }

        [Test]
        public void Presenter_ServerSettings_StartsOnTheDefaultRegion()
        {
            using var presenter = CreateStartedPresenter(out var view, out _, out _, out _, out _);

            Assert.That(view.SelectedRegion, Is.EqualTo(ServerRegionCatalog.Default.Code));
        }

        [Test]
        public void Presenter_PickingARegion_WritesItDown()
        {
            using var presenter = CreateStartedPresenter(
                out var view, out _, out _, out _, out _, out var regionStore);

            view.RaiseRegionSelected("eu");

            Assert.That(regionStore.TryLoad(out var saved), Is.True);
            Assert.That(saved, Is.EqualTo("eu"));
        }

        [Test]
        public void Presenter_StartsOnTheRegionSavedLastTime()
        {
            var store = new InMemoryServerRegionStore("eu");
            var view = new FakeHomeMenuView();
            using var presenter = new HomeMenuPresenter(
                new PlayerProfile("사용자닉네임"),
                new HomeMenuSystem(),
                view,
                new FakeHomeApplicationHost(),
                new AppFlowSystem(),
                new FriendListSystem(),
                new FriendSearchSystem(),
                new ServerRegionSystem(store));

            presenter.Start();

            Assert.That(view.SelectedRegion, Is.EqualTo("eu"));
        }

        [Test]
        public void Presenter_ARegionWeNoLongerOffer_FallsBackToTheDefault()
        {
            // A code saved by an older build, or one dropped from the
            // catalogue. Keeping it would leave the picker showing nothing
            // chosen while the game connected somewhere unnamed.
            var store = new InMemoryServerRegionStore("mars");
            var view = new FakeHomeMenuView();
            using var presenter = new HomeMenuPresenter(
                new PlayerProfile("사용자닉네임"),
                new HomeMenuSystem(),
                view,
                new FakeHomeApplicationHost(),
                new AppFlowSystem(),
                new FriendListSystem(),
                new FriendSearchSystem(),
                new ServerRegionSystem(store));

            presenter.Start();

            Assert.That(view.SelectedRegion, Is.EqualTo(ServerRegionCatalog.Default.Code));
        }

        [Test]
        public void Presenter_AnUnknownRegion_IsRefusedAndTheMarkPutBack()
        {
            using var presenter = CreateStartedPresenter(
                out var view, out _, out _, out _, out _, out var regionStore);

            view.RaiseRegionSelected("mars");

            Assert.That(regionStore.SaveCount, Is.Zero);
            Assert.That(view.SelectedRegion, Is.EqualTo(ServerRegionCatalog.Default.Code));
        }

        [Test]
        public void Presenter_ClickOutsideRegionPicker_ClosesIt()
        {
            using var presenter = CreateStartedPresenter(out var view, out _, out _, out _, out _);
            view.Raise(HomeMenuAction.ServerSettings);

            view.RaiseServerSettingsDismissed();
            Assert.That(view.ServerSettingsVisible, Is.False);

            // Closing by pressing away must leave the globe able to open it
            // again on the next press, not on the one after.
            view.Raise(HomeMenuAction.ServerSettings);
            Assert.That(view.ServerSettingsVisible, Is.True);
        }

        [Test]
        public void Presenter_RegionPicker_ClosesTheOtherPanels()
        {
            using var presenter = CreateStartedPresenter(out var view, out _, out _, out _, out _);

            view.Raise(HomeMenuAction.ProfileSettings);
            view.Raise(HomeMenuAction.ServerSettings);
            Assert.That(view.ProfileSettingsVisible, Is.False);
            Assert.That(view.ServerSettingsVisible, Is.True);

            view.Raise(HomeMenuAction.ServerSettings);
            view.Raise(HomeMenuAction.Friends);
            view.Raise(HomeMenuAction.ServerSettings);
            Assert.That(view.FriendListVisible, Is.False);
            Assert.That(view.ServerSettingsVisible, Is.True);
        }

        [Test]
        public void Presenter_OtherPanels_CloseTheRegionPicker()
        {
            using var presenter = CreateStartedPresenter(out var view, out _, out _, out _, out _);

            view.Raise(HomeMenuAction.ServerSettings);
            view.Raise(HomeMenuAction.Friends);
            Assert.That(view.ServerSettingsVisible, Is.False);

            view.Raise(HomeMenuAction.ServerSettings);
            view.Raise(HomeMenuAction.ProfileSettings);
            Assert.That(view.ServerSettingsVisible, Is.False);
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
            var regions = new ServerRegionSystem(new InMemoryServerRegionStore());

            Assert.That(
                () => new HomeMenuPresenter(
                    null, menu, view, host, appFlow, friends, search, regions),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new HomeMenuPresenter(
                    profile, null, view, host, appFlow, friends, search, regions),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new HomeMenuPresenter(
                    profile, menu, null, host, appFlow, friends, search, regions),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new HomeMenuPresenter(
                    profile, menu, view, null, appFlow, friends, search, regions),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new HomeMenuPresenter(
                    profile, menu, view, host, null, friends, search, regions),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new HomeMenuPresenter(
                    profile, menu, view, host, appFlow, null, search, regions),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new HomeMenuPresenter(
                    profile, menu, view, host, appFlow, friends, null, regions),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new HomeMenuPresenter(
                    profile, menu, view, host, appFlow, friends, search, null),
                Throws.TypeOf<ArgumentNullException>());
        }

        private static HomeMenuPresenter CreateStartedPresenter(
            out FakeHomeMenuView view,
            out FakeHomeApplicationHost host,
            out AppFlowSystem appFlow,
            out FriendListSystem friends,
            out FriendSearchSystem search)
        {
            return CreateStartedPresenter(
                out view, out host, out appFlow, out friends, out search, out _);
        }

        private static HomeMenuPresenter CreateStartedPresenter(
            out FakeHomeMenuView view,
            out FakeHomeApplicationHost host,
            out AppFlowSystem appFlow,
            out FriendListSystem friends,
            out FriendSearchSystem search,
            out InMemoryServerRegionStore regionStore)
        {
            var profile = new PlayerProfile("사용자닉네임");
            var menu = new HomeMenuSystem();
            view = new FakeHomeMenuView();
            host = new FakeHomeApplicationHost();
            appFlow = new AppFlowSystem();
            friends = new FriendListSystem();
            search = new FriendSearchSystem();
            regionStore = new InMemoryServerRegionStore();
            var presenter = new HomeMenuPresenter(
                profile, menu, view, host, appFlow, friends, search,
                new ServerRegionSystem(regionStore));
            presenter.Start();
            return presenter;
        }

        /// <summary>
        /// A region store that lives only as long as the test.
        /// </summary>
        private sealed class InMemoryServerRegionStore : IServerRegionStore
        {
            private string saved;

            public InMemoryServerRegionStore(string initial = null)
            {
                saved = initial;
            }

            public int SaveCount { get; private set; }

            public bool TryLoad(out string code)
            {
                code = saved;
                return !string.IsNullOrWhiteSpace(saved);
            }

            public void Save(string code)
            {
                saved = code;
                SaveCount++;
            }
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

            public string NicknameError { get; private set; } = string.Empty;

            public bool NicknameSettled { get; private set; }

            public IReadOnlyList<FriendRequestSummary> IncomingRequests { get; private set; } =
                Array.Empty<FriendRequestSummary>();

            public IReadOnlyList<FriendRequestSummary> OutgoingRequests { get; private set; } =
                Array.Empty<FriendRequestSummary>();

            public string FriendActionError { get; private set; } = string.Empty;

            public bool ServerSettingsVisible { get; private set; }

            public string SelectedRegion { get; private set; }

            public bool CreateRoomVisible { get; private set; }

            public event Action<HomeMenuAction> ActionClicked;

            public event Action FriendListDismissed;

            public event Action ProfileSettingsDismissed;

            public event Action ServerSettingsDismissed;

            public event Action<string> RegionSelected;

            public event Action<string, bool, int> RoomCreationRequested;

            public event Action CreateRoomDismissed;

            public event Action<string> NicknameChangeRequested;

            public event Action<string> NicknameEdited;

            public event Action FriendSearchOpened;

            public event Action FriendSearchClosed;

            public event Action<string> FriendSearchRequested;

            public event Action<string> FriendRequestClicked;

            public event Action FriendListRefreshRequested;

            public event Action<string> FriendRequestAccepted;

            public event Action<string> FriendRequestDeclined;

            public event Action<string> FriendRequestCancelled;

            public event Action<string> FriendRemoved;
            public event Action<string> RoomInviteAccepted;
            public event Action<string> RoomInviteDeclined;

            public void SetNickname(string nickname)
            {
                Nickname = nickname;
            }

            public void SetProfileSettingsVisible(bool visible)
            {
                ProfileSettingsVisible = visible;
            }

            public void SetSuspendedNoticeVisible(bool visible) =>
                SuspendedNoticeVisible = visible;

            public bool SuspendedNoticeVisible { get; private set; }

            public void SetNicknameError(string message)
            {
                NicknameError = message ?? string.Empty;
            }

            /// <remarks>
            /// A no-op on the real screen too: the panel says nothing on a
            /// rename that worked, it just shows the new name.
            /// </remarks>
            public void SetNicknameAppliedFeedbackVisible(bool visible)
            {
            }

            public void SetRoomInvites(IReadOnlyList<RoomInvite> invites)
            {
            }

            public void SetNicknameSettled(bool settled)
            {
                NicknameSettled = settled;
            }

            public void SetServerSettingsVisible(bool visible)
            {
                ServerSettingsVisible = visible;
            }

            public void SetSelectedRegion(string code)
            {
                SelectedRegion = code;
            }

            public void SetCreateRoomVisible(bool visible)
            {
                CreateRoomVisible = visible;
            }

            public event Action<bool> NicknameSearchAllowedChanged;

            public bool SearchAllowed { get; private set; }

            public void RaiseSearchAllowedChanged(bool allowed)
            {
                NicknameSearchAllowedChanged?.Invoke(allowed);
            }

            public void SetNicknameSearchAllowed(bool allowed)
            {
                SearchAllowed = allowed;
            }

            public void SetNicknameSearchAllowedError(string message)
            {
            }

            public string ConnectionError { get; private set; } = string.Empty;

            public void ShowConnectionError(string message)
            {
                ConnectionError = message ?? string.Empty;
            }

            public void RaiseRoomCreationRequested(string title, bool isPublic, int maxPlayers)
            {
                RoomCreationRequested?.Invoke(title, isPublic, maxPlayers);
            }

            public void RaiseCreateRoomDismissed()
            {
                CreateRoomDismissed?.Invoke();
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

            public void RaiseRegionSelected(string code)
            {
                RegionSelected?.Invoke(code);
            }

            public void SetIncomingRequests(IReadOnlyList<FriendRequestSummary> requests)
            {
                IncomingRequests = requests;
            }

            public void SetOutgoingRequests(IReadOnlyList<FriendRequestSummary> requests)
            {
                OutgoingRequests = requests;
            }

            public void SetFriendActionError(string message)
            {
                FriendActionError = message ?? string.Empty;
            }

            public void RaiseRequestAccepted(string playerId)
            {
                FriendRequestAccepted?.Invoke(playerId);
            }

            public void RaiseRequestDeclined(string playerId)
            {
                FriendRequestDeclined?.Invoke(playerId);
            }

            public void RaiseRequestCancelled(string playerId)
            {
                FriendRequestCancelled?.Invoke(playerId);
            }

            public void RaiseFriendRemoved(string playerId)
            {
                FriendRemoved?.Invoke(playerId);
            }

            public void RaiseRefresh()
            {
                FriendListRefreshRequested?.Invoke();
            }

            public void RaiseServerSettingsDismissed()
            {
                ServerSettingsDismissed?.Invoke();
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

            public int CharacterClosetOpenCount { get; private set; }

            public int SettingsOpenCount { get; private set; }

            public int LobbyOpenCount { get; private set; }

            public string CreatedTitle { get; private set; }

            public bool CreatedPublic { get; private set; }

            public int CreatedMaxPlayers { get; private set; }

            public int CreateCount { get; private set; }

            public void JoinRoom(string roomCode)
            {
            }

            public void CreateRoom(string title, bool isPublic, int maxPlayers)
            {
                CreatedTitle = title;
                CreatedPublic = isPublic;
                CreatedMaxPlayers = maxPlayers;
                CreateCount++;
            }

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

            public void OpenCharacterCloset()
            {
                CharacterClosetOpenCount++;
            }

            public void OpenSettings()
            {
                SettingsOpenCount++;
            }
        }
    }
}
