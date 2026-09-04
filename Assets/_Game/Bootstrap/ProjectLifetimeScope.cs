using Game.Backend;
using Game.Core.Flow;
using Game.Client.Home;
using Game.Client.Match;
using Game.Core.Home;
using Game.Core.Lobby;
using Game.Core.Ports;
using Game.Core.Voice;
using Game.Network;
using Game.Network.Lobby;
using Game.Network.Match;
using Game.Network.Players;
using Game.Network.Session;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
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

        [SerializeField]
        [Tooltip("Photon region code supplied by the deployment settings; room lists are region-local.")]
        private string _networkRegion;

        [SerializeField]
        [Tooltip("Backend address. Leave empty for the deployed server; set http://localhost:8080 to work against a local one.")]
        private string _backendBaseUrl;

        protected override void Configure(IContainerBuilder builder)
        {
            RegisterServices(
                builder,
                _networkPrefabs,
                _networkScenes,
                null,
                _networkRegion);

            // Built here rather than in RegisterServices: the device identifier
            // is this machine's saved credential, and a test container must not
            // pick up the account belonging to whoever last played here.
            RegisterBackend(builder, _backendBaseUrl);

            // Shared across Playground and Result so scene unloading cannot reveal gameplay.
            var transition = new GameObject("Highlight Transition").AddComponent<HighlightTransitionView>();
            transition.transform.SetParent(transform, false);
            builder.RegisterComponent(transition).As<IHighlightTransitionView>();

            var inputObject = new GameObject("UI EventSystem");
            inputObject.SetActive(false);
            inputObject.transform.SetParent(transform, false);
            var eventSystem = inputObject.AddComponent<EventSystem>();
            var inputModule = inputObject.AddComponent<InputSystemUIInputModule>();
            inputObject.AddComponent<SharedUiInputActions>().Bind(inputModule);
            inputObject.SetActive(true);
            builder.RegisterComponent(eventSystem);

            builder.Register<UnityHomeApplicationHost>(Lifetime.Singleton).As<IHomeApplicationHost>();
            builder.RegisterEntryPoint<FrontendSceneCoordinator>().AsSelf();
            builder.RegisterEntryPoint<NetworkRoomDisconnectController>();

            // Listens to something live. Tests build the same container without
            // wanting anything to react to scene loads.
            builder.RegisterEntryPoint<MatchSceneSpawnPoints>();
        }

        private sealed class SharedUiInputActions : MonoBehaviour
        {
            private DefaultInputActions actions;

            public void Bind(InputSystemUIInputModule inputModule)
            {
                actions = new DefaultInputActions();
                inputModule.actionsAsset = actions.asset;
                inputModule.cancel = InputActionReference.Create(actions.UI.Cancel);
                inputModule.submit = InputActionReference.Create(actions.UI.Submit);
                inputModule.move = InputActionReference.Create(actions.UI.Navigate);
                inputModule.leftClick = InputActionReference.Create(actions.UI.Click);
                inputModule.rightClick = InputActionReference.Create(actions.UI.RightClick);
                inputModule.middleClick = InputActionReference.Create(actions.UI.MiddleClick);
                inputModule.point = InputActionReference.Create(actions.UI.Point);
                inputModule.scrollWheel = InputActionReference.Create(actions.UI.ScrollWheel);
                inputModule.trackedDeviceOrientation =
                    InputActionReference.Create(actions.UI.TrackedDeviceOrientation);
                inputModule.trackedDevicePosition =
                    InputActionReference.Create(actions.UI.TrackedDevicePosition);
            }

            private void OnDestroy()
            {
                actions?.Dispose();
            }
        }

        /// <summary>
        /// Registers the backend client and the ports that speak through it.
        /// </summary>
        /// <remarks>
        /// The gateways are registered by their port only. Presentation asks for
        /// <see cref="IFriendGateway"/>, never for the client underneath, so the
        /// day this talks to something other than REST the callers do not move.
        /// <para>
        /// One client for the whole application, because the account it signs
        /// into is one account: sign in through one gateway and every other
        /// gateway is signed in too.
        /// </para>
        /// </remarks>
        private static void RegisterBackend(IContainerBuilder builder, string baseUrl)
        {
            var client = new BackendClient(
                new UnityWebRequestTransport(),
                new BackendEndpoint(baseUrl),
                new BackendSession(DeviceIdentity.Current()));

            // The client itself is not registered. Nothing above this line has a
            // reason to hold it, and a container that hands it out is one where
            // a presenter can send its own request and skip the ports entirely.
            builder.RegisterInstance<IAccountGateway>(new AccountGateway(client));
            builder.RegisterInstance<IFriendGateway>(new FriendGateway(client));
            builder.RegisterInstance<IPresenceGateway>(new PresenceGateway(client));
            builder.RegisterInstance<IBlockGateway>(new BlockGateway(client));
            builder.RegisterInstance<IInviteGateway>(new InviteGateway(client));

            // Registered beside the gateways rather than in RegisterServices,
            // because it needs one. A test container that builds only the
            // services has no backend to command.
            builder.Register<FriendUiCommands>(Lifetime.Singleton);

            // Registered as itself as well as an entry point, because the two
            // things that wait on it resolve it. One registration, so they wait
            // on the sign-in that actually ran rather than on a second instance
            // that never started.
            builder.RegisterEntryPoint<BackendSignIn>().AsSelf();
            builder.RegisterEntryPoint<PresenceHeartbeat>();
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
        /// Optional so tests get a predictable name. The application leaves it
        /// null: sign-in replaces it with the account's nickname as soon as the
        /// server answers, and until then the default stands in.
        /// </param>
        public static void RegisterServices(
            IContainerBuilder builder,
            NetworkPrefabs networkPrefabs = null,
            NetworkScenes networkScenes = null,
            PlayerProfile profile = null,
            string networkRegion = null)
        {
            builder.Register<AppFlowSystem>(Lifetime.Singleton);
            builder.Register<HomeMenuSystem>(Lifetime.Singleton);
            builder.Register<FriendListSystem>(Lifetime.Singleton);
            builder.Register<FriendSearchSystem>(Lifetime.Singleton);

            // One instance for the whole application. The home screen edits this
            // one and the network reads this one, so a rename is visible in both
            // without either knowing about the other.
            builder.RegisterInstance(profile ?? new PlayerProfile(DefaultNickname));

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
            // would let anything ask for a Fusion scene reference. The interfaces
            // it also answers to are listed here rather than resolved separately,
            // so every one of them means the same instance.
            builder.Register(
                    c => new NetworkRunnerService(
                        c.Resolve<IRoomListSink>(),
                        c.Resolve<IRoomSessionSink>(),
                        c.Resolve<IRoomParticipantSink>(),
                        c.Resolve<IMatchStartSink>(),
                        c.Resolve<PlayerSpawner>(),
                        c.Resolve<PlayerProfile>(),
                        networkScenes,
                        networkRegion),
                    Lifetime.Singleton)
                .AsSelf()
                .As<INetworkMatchRuntimeSource>()
                .As<INetworkMatchAuthority>()
                .As<INetworkMatchEvents>()
                .As<INetworkResultNavigation>()
                .As<ILobbyChatTransport>()
                .As<IMatchChatTransport>();
            builder.RegisterEntryPoint<NetworkMatchFlowSynchronizer>();
            builder.RegisterEntryPoint<NetworkResultLobbyReturnController>().AsSelf();
            // Outlives every screen. The rig that opens the microphone is
            // rebuilt with each session and the control that drives it with each
            // screen, but a player who muted themselves meant it to hold.
            builder.Register<VoicePreferences>(Lifetime.Singleton);

            builder.Register<RoomCodeGenerator>(Lifetime.Singleton);
            builder.Register<IRoomBrowser, RoomBrowser>(Lifetime.Singleton);
            builder.Register<RoomUiCommands>(Lifetime.Singleton);
        }
    }
}
