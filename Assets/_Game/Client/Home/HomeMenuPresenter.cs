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
    }

    public sealed class UnityHomeApplicationHost : IHomeApplicationHost
    {
        public const string HomeSceneName = "Home";
        public const string RoomBrowserSceneName = "RoomBrowserPreview";

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
    }

    public sealed class HomeMenuPresenter : IStartable, IDisposable
    {
        private readonly PlayerProfile profile;
        private readonly HomeMenuSystem menu;
        private readonly FriendListSystem friends;
        private readonly IHomeMenuView view;
        private readonly IHomeApplicationHost applicationHost;
        private readonly AppFlowSystem appFlow;
        private bool isFriendListVisible;

        public HomeMenuPresenter(
            PlayerProfile profile,
            HomeMenuSystem menu,
            IHomeMenuView view,
            IHomeApplicationHost applicationHost,
            AppFlowSystem appFlow,
            FriendListSystem friends)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.menu = menu ?? throw new ArgumentNullException(nameof(menu));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.applicationHost = applicationHost
                ?? throw new ArgumentNullException(nameof(applicationHost));
            this.appFlow = appFlow ?? throw new ArgumentNullException(nameof(appFlow));
            this.friends = friends ?? throw new ArgumentNullException(nameof(friends));
        }

        public void Start()
        {
            view.ActionClicked += OnActionClicked;
            profile.Changed += OnProfileChanged;
            friends.FriendsChanged += BindFriends;
            BindProfile(profile);
            BindFriends();
            view.SetFriendListVisible(false);
        }

        public void Dispose()
        {
            view.ActionClicked -= OnActionClicked;
            profile.Changed -= OnProfileChanged;
            friends.FriendsChanged -= BindFriends;
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
                isFriendListVisible = !isFriendListVisible;
                view.SetFriendListVisible(isFriendListVisible);
                return;
            }

            if (action == HomeMenuAction.FindRoom &&
                appFlow.TryTransitionTo(AppFlowState.RoomBrowser))
            {
                HideFriendList();
                applicationHost.OpenRoomBrowser();
            }
        }

        private void HideFriendList()
        {
            if (!isFriendListVisible)
            {
                return;
            }

            isFriendListVisible = false;
            view.SetFriendListVisible(false);
        }

        private void BindFriends()
        {
            view.SetFriends(friends.OnlineFriends, friends.OfflineFriends);
        }

        private void OnProfileChanged(PlayerProfile changed)
        {
            BindProfile(changed);
        }

        private void BindProfile(PlayerProfile source)
        {
            view.SetNickname(source.Nickname);
            view.SetLevel(source.Level);
        }
    }
}
