using Game.Client.Home;
using Game.Client.Match;
using Game.Client.Rooms;
using Game.Client.Common;
using Game.Network.Session;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    public sealed class RoomBrowserLifetimeScope : LifetimeScope
    {
        [SerializeField]
        private RoomBrowserView roomBrowserView;

        [SerializeField]
        private RoomScreenPresenter roomScreenPresenter;

        protected override void Configure(IContainerBuilder builder)
        {
            if (roomBrowserView == null)
            {
                Debug.LogError("RoomBrowserView must be assigned on RoomBrowserLifetimeScope.", this);
                return;
            }

            builder.Register<NetworkRoomApplicationHost>(Lifetime.Scoped)
                .As<IHomeApplicationHost>();
            builder.RegisterComponent(roomBrowserView).As<IRoomBrowserView>();
            builder.RegisterEntryPoint<RoomBrowserPresenter>();
            builder.RegisterEntryPoint<RoomBrowserTransitionReset>();

            if (roomScreenPresenter != null)
            {
                builder.RegisterComponent(roomScreenPresenter);

                // Registered with the screen because it is the screen's requests
                // it carries; without the screen there is nothing to carry.
                builder.RegisterEntryPoint<NetworkRoomScreenBridge>();
            }
            else
            {
                Debug.LogError(
                    "RoomScreenPresenter must be assigned on RoomBrowserLifetimeScope, " +
                    "otherwise the screen never reaches Photon.",
                    this);
            }
        }

        private sealed class RoomBrowserTransitionReset : IStartable
        {
            private readonly IHighlightTransitionView transition;
            public RoomBrowserTransitionReset(IHighlightTransitionView transition) => this.transition = transition;
            public void Start() => transition.SetOpacity(0f);
        }

        /// <summary>
        /// The room screen already owns a running Fusion session. Its lobby
        /// transition must therefore use Fusion instead of replacing the Unity
        /// scene behind the runner.
        /// </summary>
        private sealed class NetworkRoomApplicationHost : IHomeApplicationHost
        {
            private readonly NetworkRunnerService network;
            private readonly FrontendSceneCoordinator scenes;
            private readonly UnityHomeApplicationHost fallback = new();
            private readonly ILoadingOverlay loading;

            public NetworkRoomApplicationHost(
                NetworkRunnerService network,
                FrontendSceneCoordinator scenes,
                ILoadingOverlay loading)
            {
                this.network = network;
                this.scenes = scenes;
                this.loading = loading;
            }

            public void Quit() => fallback.Quit();

            public void OpenHome() => scenes.OpenHome();

            public void OpenRoomBrowser() => scenes.OpenRoomBrowser();

            public void OpenCharacterCloset() => scenes.OpenCharacterCloset();

            public void OpenSettings() => scenes.OpenSettings();

            /// <summary>
            /// Not from here. The room screen has its own way of opening a
            /// room; this host answers the Home presenter's interface only
            /// because the two screens share it.
            /// </summary>
            public void CreateRoom(string title, bool isPublic, int maxPlayers) =>
                fallback.CreateRoom(title, isPublic, maxPlayers);

            public void JoinRoom(string roomCode) => fallback.JoinRoom(roomCode);

            public void OpenLobby()
            {
                // The request already painted the cover before connecting. Fusion may
                // have finished the lobby and hidden it before this callback arrives.
                if (!network.EnterLobbyScene())
                {
                    loading.HideImmediate();
                    Debug.LogError(
                        "[Session] Cannot enter Lobby without a running room session.");
                }
            }
        }
    }
}
