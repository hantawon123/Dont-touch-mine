using Game.Client.Home;
using Game.Client.Rooms;
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

            builder.Register<UnityHomeApplicationHost>(Lifetime.Scoped).As<IHomeApplicationHost>();
            builder.RegisterComponent(roomBrowserView).As<IRoomBrowserView>();
            builder.RegisterEntryPoint<RoomBrowserPresenter>();

            if (roomScreenPresenter != null)
            {
                builder.RegisterComponent(roomScreenPresenter);
            }
        }
    }
}
