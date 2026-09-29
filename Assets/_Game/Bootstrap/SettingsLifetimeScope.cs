using Game.Client.Home;
using Game.Client.Settings;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// The settings scene.
    /// </summary>
    /// <remarks>
    /// The settings in force are not registered here. They belong to the whole
    /// application — whatever draws in the chosen language reads them — so
    /// they are resolved from the project scope, and a copy registered for
    /// this scene would be forgotten the moment the screen closed.
    /// </remarks>
    public sealed class SettingsLifetimeScope : LifetimeScope
    {
        [SerializeField]
        private SettingsView settingsView;

        protected override void Configure(IContainerBuilder builder)
        {
            if (settingsView == null)
            {
                Debug.LogError(
                    "SettingsView must be assigned on SettingsLifetimeScope.",
                    this);
                return;
            }

            builder.Register<SettingsApplicationHost>(Lifetime.Scoped)
                .As<IHomeApplicationHost>();
            builder.RegisterComponent(settingsView).As<ISettingsView>();
            builder.RegisterEntryPoint<SettingsPresenter>()
                .WithParameter<System.Action>((System.Action)null);

            // 피드백을 서버로 보내는 쪽. 화면과 같은 수명이라 여기 둡니다. 프로젝트
            // 범위에 두면 설정 화면이 없을 때도 살아 있고, 그때 ISettingsView 를
            // 해결할 수 없어 컨테이너가 기동에서 터집니다.
            builder.RegisterEntryPoint<SettingsFeedbackBridge>();
        }

        /// <summary>
        /// Leaves through the frontend coordinator rather than by loading Home
        /// outright, so the screens that are already warm stay warm.
        /// </summary>
        private sealed class SettingsApplicationHost : IHomeApplicationHost
        {
            private readonly FrontendSceneCoordinator scenes;
            private readonly UnityHomeApplicationHost fallback = new();

            public SettingsApplicationHost(FrontendSceneCoordinator scenes)
            {
                this.scenes = scenes;
            }

            public void Quit() => fallback.Quit();

            public void OpenHome() => scenes.OpenHome();

            public void OpenRoomBrowser() => scenes.OpenRoomBrowser();

            public void OpenCharacterCloset() => scenes.OpenCharacterCloset();

            public void OpenSettings() => scenes.OpenSettings();

            public void OpenTutorial() => fallback.OpenTutorial();

            /// <summary>
            /// Not from here. The settings screen answers the Home presenter's
            /// interface only because leaving is the one thing every screen
            /// does.
            /// </summary>
            public void CreateRoom(string title, bool isPublic, int maxPlayers) =>
                fallback.CreateRoom(title, isPublic, maxPlayers);

            public void JoinRoom(string roomCode) => fallback.JoinRoom(roomCode);

            public void OpenLobby() => fallback.OpenLobby();
        }
    }
}
