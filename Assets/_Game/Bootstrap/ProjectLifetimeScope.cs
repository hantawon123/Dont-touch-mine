using Game.Core.Flow;
using Game.Core.Home;
using Game.Core.Lobby;
using Game.Core.Ports;
using Game.Network;
using Game.Network.Lobby;
using Game.Network.Players;
using Game.Network.Session;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    public sealed class ProjectLifetimeScope : LifetimeScope
    {
        [SerializeField]
        [Tooltip("Prefabs this application spawns over the network.")]
        private NetworkPrefabs _networkPrefabs;

        protected override void Configure(IContainerBuilder builder)
        {
            RegisterServices(builder, _networkPrefabs);
        }

        /// <param name="networkPrefabs">
        /// Optional so tests can build the same container without a project
        /// asset. A spawner without prefabs reports the problem when it is first
        /// asked to spawn rather than failing to construct.
        /// </param>
        public static void RegisterServices(
            IContainerBuilder builder, NetworkPrefabs networkPrefabs = null)
        {
            builder.Register<AppFlowSystem>(Lifetime.Singleton);
            builder.Register<HomeMenuSystem>(Lifetime.Singleton);
            builder.Register<FriendListSystem>(Lifetime.Singleton);

            // Replaced by the saved Steam/backend profile when that adapter is connected.
            builder.RegisterInstance(new PlayerProfile("Player", 1));

            builder.Register<RoomBrowserSystem>(Lifetime.Singleton)
                .AsSelf()
                .As<IRoomListSink>()
                .As<IRoomSessionSink>()
                .As<IRoomParticipantSink>();

            builder.Register<PlayerRegistry>(Lifetime.Singleton);

            // Built by hand because the prefab asset is a value, not a service,
            // and registering it as a resolvable type would let anything ask for
            // a Fusion prefab.
            builder.Register(
                c => new PlayerSpawner(networkPrefabs, c.Resolve<PlayerRegistry>()),
                Lifetime.Singleton);

            builder.Register<NetworkRunnerService>(Lifetime.Singleton);
            builder.Register<RoomCodeGenerator>(Lifetime.Singleton);
            builder.Register<IRoomBrowser, RoomBrowser>(Lifetime.Singleton);
            builder.Register<RoomUiCommands>(Lifetime.Singleton);
        }
    }
}
