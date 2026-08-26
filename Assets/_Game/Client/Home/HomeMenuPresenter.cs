using System;
using Game.Core.Flow;
using Game.Core.Home;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace Game.Client.Home
{
    public interface IHomeApplicationHost
    {
        void Quit();

        void OpenHome();

        void OpenRoomBrowser();

        void OpenSettings();
    }

    public sealed class UnityHomeApplicationHost : IHomeApplicationHost
    {
        public const string HomeSceneName = "Home";
        public const string RoomBrowserSceneName = "Room";
        public const string SettingsSceneName = "Settings";

        public void Quit()
        {
            Application.Quit();
#if UNITY_EDITOR
            var editorApplicationType = Type.GetType("UnityEditor.EditorApplication, UnityEditor");
            editorApplicationType?.GetProperty("isPlaying")?.SetValue(null, false);
#endif
        }

        public void OpenHome()
        {
            SceneManager.LoadScene(HomeSceneName);
        }

        public void OpenRoomBrowser()
        {
            SceneManager.LoadScene(RoomBrowserSceneName);
        }

        public void OpenSettings()
        {
            SceneManager.LoadScene(SettingsSceneName);
        }
    }

    public sealed class HomeMenuPresenter : IStartable, IDisposable
    {
        private readonly PlayerProfile profile;
        private readonly HomeMenuSystem menu;
        private readonly FriendListSystem friends;
        private readonly FriendSearchSystem search;
        private readonly IHomeMenuView view;
        private readonly IHomeApplicationHost applicationHost;
        private readonly AppFlowSystem appFlow;
        private bool isFriendListVisible;
        private bool isProfileSettingsVisible;

        public HomeMenuPresenter(
            PlayerProfile profile,
            HomeMenuSystem menu,
            IHomeMenuView view,
            IHomeApplicationHost applicationHost,
            AppFlowSystem appFlow,
            FriendListSystem friends,
            FriendSearchSystem search)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.menu = menu ?? throw new ArgumentNullException(nameof(menu));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.applicationHost = applicationHost
                ?? throw new ArgumentNullException(nameof(applicationHost));
            this.appFlow = appFlow ?? throw new ArgumentNullException(nameof(appFlow));
            this.friends = friends ?? throw new ArgumentNullException(nameof(friends));
            this.search = search ?? throw new ArgumentNullException(nameof(search));
        }

        public void Start()
        {
            view.ActionClicked += OnActionClicked;
            view.FriendListDismissed += HideFriendList;
            view.ProfileSettingsDismissed += HideProfileSettings;
            view.NicknameChangeRequested += OnNicknameChangeRequested;
            view.NicknameEdited += OnNicknameEdited;
            view.FriendSearchOpened += OnFriendSearchOpened;
            view.FriendSearchClosed += OnFriendSearchClosed;
            view.FriendSearchRequested += OnFriendSearchRequested;
            view.FriendRequestClicked += OnFriendRequestClicked;
            profile.Changed += BindProfile;
            friends.FriendsChanged += BindFriends;
            search.ResultsChanged += BindSearchResults;
            BindProfile(profile);
            BindFriends();
            HideFriendList();
            HideProfileSettings();
        }

        public void Dispose()
        {
            view.ActionClicked -= OnActionClicked;
            view.FriendListDismissed -= HideFriendList;
            view.ProfileSettingsDismissed -= HideProfileSettings;
            view.NicknameChangeRequested -= OnNicknameChangeRequested;
            view.NicknameEdited -= OnNicknameEdited;
            view.FriendSearchOpened -= OnFriendSearchOpened;
            view.FriendSearchClosed -= OnFriendSearchClosed;
            view.FriendSearchRequested -= OnFriendSearchRequested;
            view.FriendRequestClicked -= OnFriendRequestClicked;
            profile.Changed -= BindProfile;
            friends.FriendsChanged -= BindFriends;
            search.ResultsChanged -= BindSearchResults;
        }

        private void OnActionClicked(HomeMenuAction action)
        {
            menu.Request(action);
            if (action == HomeMenuAction.Quit)
            {
                applicationHost.Quit();
                return;
            }

            if (action == HomeMenuAction.Friends)
            {
                HideProfileSettings();
                ShowFriendList();
                return;
            }

            if (action == HomeMenuAction.ProfileSettings)
            {
                HideFriendList();
                ShowProfileSettings();
                return;
            }

            if (action == HomeMenuAction.Settings &&
                appFlow.TryTransitionTo(AppFlowState.Settings))
            {
                HideFriendList();
                HideProfileSettings();
                applicationHost.OpenSettings();
                return;
            }

            if (action == HomeMenuAction.FindRoom &&
                appFlow.TryTransitionTo(AppFlowState.RoomBrowser))
            {
                HideFriendList();
                HideProfileSettings();
                applicationHost.OpenRoomBrowser();
            }
        }

        private void OnNicknameChangeRequested(string nickname)
        {
            if (!isProfileSettingsVisible)
            {
                return;
            }

            if (profile.TryChangeNickname(nickname, out _))
            {
                view.SetNicknameAppliedFeedbackVisible(true);
            }
        }

        private void OnNicknameEdited(string nickname)
        {
            if (!isProfileSettingsVisible)
            {
                return;
            }

            if (!string.Equals(nickname, profile.Nickname, StringComparison.Ordinal))
            {
                view.SetNicknameAppliedFeedbackVisible(false);
            }
        }

        private void OnFriendSearchOpened()
        {
            search.ClearResults();
            view.SetFriendSearchVisible(true);
            BindSearchResults();
        }

        private void OnFriendSearchClosed()
        {
            HideFriendSearch();
        }

        private void OnFriendSearchRequested(string query)
        {
            search.Search(query, CollectFriendIds());
        }

        private void OnFriendRequestClicked(string playerId)
        {
            search.TrySendRequest(playerId);
        }

        private void ShowFriendList()
        {
            if (isFriendListVisible)
            {
                return;
            }

            isFriendListVisible = true;
            HideFriendSearch();
            view.SetFriendListVisible(true);
        }

        private void HideFriendList()
        {
            HideFriendSearch();
            isFriendListVisible = false;
            view.SetFriendListVisible(false);
        }

        private void ShowProfileSettings()
        {
            if (isProfileSettingsVisible)
            {
                return;
            }

            isProfileSettingsVisible = true;
            view.SetNickname(profile.Nickname);
            view.SetLevel(profile.Level);
            view.SetNicknameAppliedFeedbackVisible(false);
            view.SetProfileSettingsVisible(true);
        }

        private void HideProfileSettings()
        {
            isProfileSettingsVisible = false;
            view.SetNickname(profile.Nickname);
            view.SetNicknameAppliedFeedbackVisible(false);
            view.SetProfileSettingsVisible(false);
        }

        private void HideFriendSearch()
        {
            search.ClearResults();
            view.SetFriendSearchVisible(false);
        }

        private void BindFriends()
        {
            view.SetFriends(friends.OnlineFriends, friends.OfflineFriends);
        }

        private void BindSearchResults()
        {
            view.SetFriendSearchResults(search.Results);
        }

        private string[] CollectFriendIds()
        {
            var ids = new string[friends.OnlineFriends.Count + friends.OfflineFriends.Count];
            var index = 0;
            for (var i = 0; i < friends.OnlineFriends.Count; i++)
            {
                ids[index++] = friends.OnlineFriends[i].PlayerId;
            }

            for (var i = 0; i < friends.OfflineFriends.Count; i++)
            {
                ids[index++] = friends.OfflineFriends[i].PlayerId;
            }

            return ids;
        }

        private void BindProfile(PlayerProfile source)
        {
            view.SetNickname(source.Nickname);
            view.SetLevel(source.Level);
        }
    }
}
