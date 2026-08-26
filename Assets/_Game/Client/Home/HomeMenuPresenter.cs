using System;
using Game.Core.Home;
using UnityEngine;
using VContainer.Unity;

namespace Game.Client.Home
{
    public interface IHomeApplicationHost
    {
        void Quit();
    }

    public sealed class UnityHomeApplicationHost : IHomeApplicationHost
    {
        public void Quit()
        {
            Application.Quit();
#if UNITY_EDITOR
            var editorApplicationType = Type.GetType("UnityEditor.EditorApplication, UnityEditor");
            editorApplicationType?.GetProperty("isPlaying")?.SetValue(null, false);
#endif
        }
    }

    public sealed class HomeMenuPresenter : IStartable, IDisposable
    {
        private readonly PlayerProfile profile;
        private readonly HomeMenuSystem menu;
        private readonly IHomeMenuView view;
        private readonly IHomeApplicationHost applicationHost;

        public HomeMenuPresenter(
            PlayerProfile profile,
            HomeMenuSystem menu,
            IHomeMenuView view,
            IHomeApplicationHost applicationHost)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.menu = menu ?? throw new ArgumentNullException(nameof(menu));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.applicationHost = applicationHost
                ?? throw new ArgumentNullException(nameof(applicationHost));
        }

        public void Start()
        {
            view.ActionClicked += OnActionClicked;
            profile.Changed += OnProfileChanged;
            BindProfile(profile);
        }

        public void Dispose()
        {
            view.ActionClicked -= OnActionClicked;
            profile.Changed -= OnProfileChanged;
        }

        private void OnActionClicked(HomeMenuAction action)
        {
            menu.Request(action);
            if (action == HomeMenuAction.Quit)
            {
                applicationHost.Quit();
            }
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
