using Game.Client.Character;
using Game.Client.Home;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// The character closet scene.
    /// </summary>
    /// <remarks>
    /// The appearance the player has settled on is not registered here. It
    /// belongs to the whole application — the lobby dresses the player in it
    /// too — so it is resolved from the project scope, and a copy registered
    /// for this scene would be forgotten the moment the closet closed.
    /// </remarks>
    public sealed class CharacterClosetLifetimeScope : LifetimeScope
    {
        [SerializeField]
        private CharacterClosetView closetView;

        [SerializeField]
        [Tooltip("The wardrobe the grid is filled from. The preview character " +
                 "must be pointed at the same asset.")]
        private AvatarPartCatalog partCatalog;

        protected override void Configure(IContainerBuilder builder)
        {
            if (closetView == null)
            {
                Debug.LogError(
                    "CharacterClosetView must be assigned on CharacterClosetLifetimeScope.",
                    this);
                return;
            }

            if (partCatalog == null)
            {
                Debug.LogError(
                    "AvatarPartCatalog must be assigned on CharacterClosetLifetimeScope.",
                    this);
                return;
            }

            builder.Register<ClosetApplicationHost>(Lifetime.Scoped)
                .As<IHomeApplicationHost>();
            builder.RegisterComponent(closetView).As<ICharacterClosetView>();
            builder.RegisterInstance(partCatalog);
            builder.RegisterEntryPoint<CharacterClosetPresenter>()
                .WithParameter<System.Action>((System.Action)null);

            // Stores what the player applies. Kept out of the presenter so the
            // screen's rules stay free of the network.
            builder.RegisterEntryPoint<ClosetAppearanceSaver>();
        }

        /// <summary>
        /// Leaves through the frontend coordinator rather than by loading Home
        /// outright, so the screens that are already warm stay warm.
        /// </summary>
        private sealed class ClosetApplicationHost : IHomeApplicationHost
        {
            private readonly FrontendSceneCoordinator scenes;
            private readonly UnityHomeApplicationHost fallback = new();

            public ClosetApplicationHost(FrontendSceneCoordinator scenes)
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
            /// Not from here. The closet answers the Home presenter's interface
            /// only because leaving is the one thing every screen does.
            /// </summary>
            public void CreateRoom(string title, bool isPublic, int maxPlayers) =>
                fallback.CreateRoom(title, isPublic, maxPlayers);

            public void JoinRoom(string roomCode) => fallback.JoinRoom(roomCode);

            public void OpenLobby() => fallback.OpenLobby();
        }
    }
}
