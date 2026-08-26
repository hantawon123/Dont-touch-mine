using Game.Client.Home;
using Game.Core.Home;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    public sealed class HomeLifetimeScope : LifetimeScope
    {
        [SerializeField]
        private HomeMenuView homeMenuView;

        protected override void Configure(IContainerBuilder builder)
        {
            if (homeMenuView == null)
            {
                Debug.LogError("HomeMenuView must be assigned on HomeLifetimeScope.", this);
                return;
            }

            // No PlayerProfile here on purpose. Registering one would shadow the
            // application-wide profile for this scene only, so renaming yourself
            // on this screen would change a copy that nothing else can see: not
            // the saved profile, and not the name the network sends. Resolution
            // falls through to the project scope instead.
            builder.Register<UnityHomeApplicationHost>(Lifetime.Scoped).As<IHomeApplicationHost>();
            builder.RegisterComponent(homeMenuView).As<IHomeMenuView>();
            builder.RegisterEntryPoint<HomeMenuPresenter>();

            // Placeholder rows until a Steam adapter calls FriendListSystem.ReplaceFriends.
            builder.RegisterBuildCallback(container =>
            {
                var friendList = container.Resolve<FriendListSystem>();
                var friendSearch = container.Resolve<FriendSearchSystem>();
                if (friendList.OnlineFriends.Count > 0 || friendList.OfflineFriends.Count > 0)
                {
                    return;
                }

                var previewFriends = new[]
                {
                    new FriendSummary("preview-1", "친구1", FriendPresence.InGame),
                    new FriendSummary("preview-2", "친구2", FriendPresence.InGame),
                    new FriendSummary("preview-3", "친구3", FriendPresence.Online),
                    new FriendSummary("preview-4", "친구4", FriendPresence.Offline)
                };
                friendList.ReplaceFriends(previewFriends);
                friendSearch.ReplaceDirectory(new[]
                {
                    previewFriends[0],
                    previewFriends[1],
                    previewFriends[2],
                    previewFriends[3],
                    new FriendSummary("preview-search-1", "친구5", FriendPresence.Online),
                    new FriendSummary("preview-search-2", "친구6", FriendPresence.Offline),
                    new FriendSummary("preview-search-3", "플레이어A", FriendPresence.Online)
                });
            });
        }
    }
}
