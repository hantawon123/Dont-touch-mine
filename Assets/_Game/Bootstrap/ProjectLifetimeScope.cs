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
        /// <summary>Name a machine starts with before anyone renames themselves.</summary>
        private const string DefaultNickname = "Player";

        [SerializeField]
        [Tooltip("Prefabs this application spawns over the network.")]
        private NetworkPrefabs _networkPrefabs;

        [SerializeField]
        [Tooltip("Scenes this application loads over the network.")]
        private NetworkScenes _networkScenes;

        protected override void Configure(IContainerBuilder builder)
        {
            // The store is built here, not in RegisterServices: it reads this
            // machine's saved preferences, and a test container must not pick up
            // whoever last played on the developer's machine.
            var store = new PlayerPrefsProfileStore();

            RegisterServices(builder, _networkPrefabs, _networkScenes, LoadProfile(store));
            builder.RegisterInstance<IProfileStore>(store);

            // Both listen to something live. Tests build the same container
            // without wanting anything to react to scene loads or to write to
            // this machine's preferences.
            builder.RegisterEntryPoint<MatchSceneSpawnPoints>();
            builder.RegisterEntryPoint<ProfilePersistence>();
        }

        /// <summary>
        /// The saved profile, or a first-run default.
        /// </summary>
        /// <remarks>
        /// One default in one place. It used to be decided twice — once here and
        /// once on the home screen's own scope — which produced two profiles that
        /// never met: renaming yourself on the home screen changed a copy the
        /// network never read.
        /// </remarks>
        private static PlayerProfile LoadProfile(IProfileStore store)
        {
            return store.TryLoad(out var nickname, out var level)
                ? new PlayerProfile(nickname, level)
                : new PlayerProfile(DefaultNickname, 1);
        }

        /// <param name="networkPrefabs">
        /// Optional so tests can build the same container without a project
        /// asset. A spawner without prefabs reports the problem when it is first
        /// asked to spawn rather than failing to construct.
        /// </param>
        /// <param name="networkScenes">
        /// Optional for the same reason. A session without it still opens; only
        /// moving into a map reports that it has nowhere to go.
        /// </param>
        /// <param name="profile">
        /// Optional so tests get a predictable default instead of this machine's
        /// saved profile.
        /// </param>
        public static void RegisterServices(
            IContainerBuilder builder,
            NetworkPrefabs networkPrefabs = null,
            NetworkScenes networkScenes = null,
            PlayerProfile profile = null)
        {
            builder.Register<AppFlowSystem>(Lifetime.Singleton);
            builder.Register<HomeMenuSystem>(Lifetime.Singleton);
            builder.Register<FriendListSystem>(Lifetime.Singleton);
            builder.Register<FriendSearchSystem>(Lifetime.Singleton);

            // One instance for the whole application. The home screen edits this
            // one and the network reads this one, so a rename is visible in both
            // without either knowing about the other.
            builder.RegisterInstance(profile ?? new PlayerProfile(DefaultNickname, 1));

            builder.Register<RoomBrowserSystem>(Lifetime.Singleton)
                .AsSelf()
                .As<IRoomListSink>()
                .As<IRoomSessionSink>()
                .As<IRoomParticipantSink>()
                .As<IMatchStartSink>();

            builder.Register<PlayerRegistry>(Lifetime.Singleton);

            // Built by hand because the prefab asset is a value, not a service,
            // and registering it as a resolvable type would let anything ask for
            // a Fusion prefab.
            builder.Register(
                c => new PlayerSpawner(networkPrefabs, c.Resolve<PlayerRegistry>()),
                Lifetime.Singleton);

            // Built by hand for the same reason the spawner is: the scene asset
            // is a value, not a service, and registering it as a resolvable type
            // would let anything ask for a Fusion scene reference.
            builder.Register(
                c => new NetworkRunnerService(
                    c.Resolve<IRoomListSink>(),
                    c.Resolve<IRoomSessionSink>(),
                    c.Resolve<IRoomParticipantSink>(),
                    c.Resolve<IMatchStartSink>(),
                    c.Resolve<PlayerSpawner>(),
                    c.Resolve<PlayerProfile>(),
                    networkScenes),
                Lifetime.Singleton);
            builder.Register<RoomCodeGenerator>(Lifetime.Singleton);
            builder.Register<IRoomBrowser, RoomBrowser>(Lifetime.Singleton);
            builder.Register<RoomUiCommands>(Lifetime.Singleton);
        }
    }
}
