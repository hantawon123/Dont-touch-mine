using Game.Client.Accessibility;
using Game.Client.Audio;
using Game.Client.Controls;
using Game.Client.Graphics;
using Game.Core.Flow;
using Game.Core.Home;
using Game.Core.Lobby;
using Game.Core.Ports;
using Game.Network;
using Game.Network.Lobby;
using Game.Network.Players;
using Game.Network.Session;
using UnityEngine;
using UnityEngine.InputSystem;
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
            RegisterServices(builder, _networkPrefabs, InputSystem.actions);
        }

        /// <param name="networkPrefabs">
        /// Optional so tests can build the same container without a project
        /// asset. A spawner without prefabs reports the problem when it is first
        /// asked to spawn rather than failing to construct.
        /// </param>
        /// <param name="inputActions">
        /// Project-wide actions in play mode. Tests omit this so the applier
        /// does not touch the .inputactions asset during Edit Mode.
        /// </param>
        public static void RegisterServices(
            IContainerBuilder builder,
            NetworkPrefabs networkPrefabs = null,
            InputActionAsset inputActions = null)
        {
            builder.Register<AppFlowSystem>(Lifetime.Singleton);
            builder.Register<HomeMenuSystem>(Lifetime.Singleton);
            builder.Register<FriendListSystem>(Lifetime.Singleton);
            builder.Register<FriendSearchSystem>(Lifetime.Singleton);
            builder.Register<PlayerPrefsAudioSettingsStore>(Lifetime.Singleton)
                .As<IAudioSettingsStore>();
            builder.Register<UnityAudioSettingsApplier>(Lifetime.Singleton)
                .As<IAudioSettingsApplier>();
            builder.Register<AudioSettingsService>(Lifetime.Singleton)
                .As<IAudioSettings>();
            builder.Register<PlayerPrefsAccessibilitySettingsStore>(Lifetime.Singleton)
                .As<IAccessibilitySettingsStore>();
            builder.Register<UnityAccessibilitySettingsApplier>(Lifetime.Singleton)
                .As<IAccessibilitySettingsApplier>();
            builder.Register<AccessibilitySettingsService>(Lifetime.Singleton)
                .As<IAccessibilitySettings>();
            builder.Register<PlayerPrefsGraphicsSettingsStore>(Lifetime.Singleton)
                .As<IGraphicsSettingsStore>();
            builder.Register<UnityGraphicsSettingsApplier>(Lifetime.Singleton)
                .As<IGraphicsSettingsApplier>();
            builder.Register<GraphicsSettingsService>(Lifetime.Singleton)
                .As<IGraphicsSettings>();
            builder.Register<PlayerPrefsControlSettingsStore>(Lifetime.Singleton)
                .As<IControlSettingsStore>();
            builder.Register(_ => new UnityControlSettingsApplier(inputActions), Lifetime.Singleton)
                .As<IControlSettingsApplier>();
            builder.Register<ControlSettingsService>(Lifetime.Singleton)
                .As<IControlSettings>();
            builder.RegisterBuildCallback(container =>
            {
                container.Resolve<IAudioSettings>();
                container.Resolve<IAccessibilitySettings>();
                container.Resolve<IGraphicsSettings>();
                container.Resolve<IControlSettings>();
            });

            // Replaced by the saved Steam/backend profile when that adapter is connected.
            builder.RegisterInstance(new PlayerProfile("Player", 1));

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

            builder.Register<NetworkRunnerService>(Lifetime.Singleton);
            builder.Register<RoomCodeGenerator>(Lifetime.Singleton);
            builder.Register<IRoomBrowser, RoomBrowser>(Lifetime.Singleton);
            builder.Register<RoomUiCommands>(Lifetime.Singleton);
        }
    }
}
