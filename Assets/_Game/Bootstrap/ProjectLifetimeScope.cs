using Game.Core.Ports;
using Game.Server.Lobby;
using Game.Server.Network;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    public sealed class ProjectLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // Register services that must live for the entire application here.

            // The room list has a single owner per application, and the network
            // service depends on it, so it is registered here rather than in a
            // scene scope. Swapped for the client's reactive store later.
            builder.Register<IRoomListSink, DebugRoomListSink>(Lifetime.Singleton);

            // One network session exists per application and it has to survive
            // scene loads, so it belongs to the root scope rather than a scene.
            builder.Register<NetworkRunnerService>(Lifetime.Singleton);

            builder.Register<RoomCodeGenerator>(Lifetime.Singleton);
            builder.Register<IRoomBrowser, RoomBrowser>(Lifetime.Singleton);
        }
    }
}
