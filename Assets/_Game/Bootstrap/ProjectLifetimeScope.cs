using Game.Backend;
using Game.Core.Flow;
using Game.Client.Common;
using Game.Client.Home;
using Game.Client.Match;
using Game.Core.Home;
using Game.Core.Lobby;
using Game.Core.Players;
using Game.Core.Ports;
using Game.Core.Settings;
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

        [SerializeField]
        [Tooltip("Picture for the mouse pointer. 32x32, imported as Cursor with Read/Write on. Empty keeps the system arrow.")]
        private Texture2D _cursor;

        [SerializeField]
        [Tooltip("Pixel of the cursor picture that clicks, from its top-left. An arrow's tip, a hand's fingertip.")]
        private Vector2 _cursorHotspot;

        [SerializeField]
        private AudioClip _menuBgm;

        [SerializeField]
        [Tooltip("Played once for every UI button click.")]
        private AudioClip _uiButtonClick;

        [SerializeField]
        [Tooltip("Looped only during highlight playback.")]
        private AudioClip _endingBgm;

        protected override void Configure(IContainerBuilder builder)
        {
            if (DedicatedServerStartup.IsRequested)
            {
                RegisterServices(builder, _networkPrefabs, _networkScenes,
                    new PlayerProfile("Server"), new ServerRegionSystem(new InMemoryServerRegionStore(),
                        DedicatedServerStartup.Argument("-region", _networkRegion)));
                RegisterBackend(builder, DedicatedServerStartup.Argument("-backendUrl", _backendBaseUrl));
                builder.RegisterEntryPoint<MatchSceneSpawnPoints>();
                builder.RegisterEntryPoint<DedicatedServerStartup>();
                return;
            }
            // Built here rather than in RegisterServices: it reads this
            // machine's preferences, and a test container must not pick up
            // whichever region the developer last chose. The deployment's own
            // region seeds it, so a build shipped for one region still starts
            // there before anybody picks another.
            var regionStore = new PlayerPrefsServerRegionStore();

            // Built here for the same reason: the settings screen reads this
            // machine's preferences, and a test container must not pick up
            // whichever language the developer last applied.
            var generalSettingsStore = new PlayerPrefsGeneralSettingsStore();

            // Likewise for the graphics settings, whose applier is the one
            // object in the game that speaks to the renderer.
            var graphicsSettingsStore = new PlayerPrefsGraphicsSettingsStore();
            var graphicsSettingsApplier = new UnityGraphicsSettingsApplier();
            var interfaceSettingsStore = new PlayerPrefsInterfaceSettingsStore();
            var soundSettingsStore = new PlayerPrefsSoundSettingsStore();
            var soundSettingsApplier = new UnitySoundSettingsApplier();
            var microphones = new UnityMicrophoneDevices();
            var controlSettingsStore = new PlayerPrefsControlSettingsStore();
            var notificationSettingsStore = new PlayerPrefsNotificationSettingsStore();

            // Likewise for the HUD microphone and speaker: a mute set in one
            // room is what the next room should open with.
            var voicePreferencesStore = new PlayerPrefsVoicePreferencesStore();

            // Likewise for first person versus third person: a view set in the
            // lobby is what the match should open with, and the other way.
            var cameraViewStore = new PlayerPrefsCameraViewStore();

            RegisterServices(
                builder,
                _networkPrefabs,
                _networkScenes,
                null,
                new ServerRegionSystem(regionStore, _networkRegion),
                new GeneralSettingsSystem(generalSettingsStore),
                new GraphicsSettingsSystem(graphicsSettingsStore, graphicsSettingsApplier),
                new InterfaceSettingsSystem(interfaceSettingsStore),
                new SoundSettingsSystem(soundSettingsStore, soundSettingsApplier, microphones),
                new ControlSettingsSystem(controlSettingsStore),
                new NotificationSettingsSystem(notificationSettingsStore),
                registerNullMicrophoneTest: false);
            builder.RegisterEntryPoint<UnityMicrophoneTest>().As<IMicrophoneTest>();
            builder.RegisterInstance<IServerRegionStore>(regionStore);

            // Lets BackendSignIn fall back to the pair saved by an earlier launch
            // when the backend cannot be reached, so a server restart does not
            // stop people from connecting to Photon (S15P21D205-925).
            builder.RegisterInstance<IPhotonCredentialStore>(
                new PlayerPrefsPhotonCredentialStore());
            builder.RegisterInstance<IGeneralSettingsStore>(generalSettingsStore);
            builder.RegisterInstance<IGraphicsSettingsStore>(graphicsSettingsStore);
            builder.RegisterInstance<IGraphicsSettingsApplier>(graphicsSettingsApplier);
            builder.RegisterInstance<IInterfaceSettingsStore>(interfaceSettingsStore);
            builder.RegisterInstance<ISoundSettingsStore>(soundSettingsStore);
            builder.RegisterInstance<ISoundSettingsApplier>(soundSettingsApplier);
            builder.RegisterInstance<IMicrophoneDevices>(microphones);
            builder.RegisterInstance<IControlSettingsStore>(controlSettingsStore);
            builder.RegisterInstance<IVoicePreferencesStore>(voicePreferencesStore);
            builder.RegisterInstance<ICameraViewStore>(cameraViewStore);
            builder.RegisterInstance<INotificationSettingsStore>(notificationSettingsStore);

            // Listens to the whole keyboard and mouse while a key is being
            // put on an action, so only the application has one.
            builder.Register<IKeyCapture, UnityKeyCapture>(Lifetime.Singleton);
            builder.RegisterEntryPoint<SoundSettingsStartup>();
            if (_menuBgm != null)
            {
                var musicObject = new GameObject("Menu BGM");
                musicObject.transform.SetParent(transform, false);
                var music = musicObject.AddComponent<AudioSource>();
                music.playOnAwake = false;
                music.loop = true;
                music.spatialBlend = 0f;
                music.clip = _menuBgm;
                builder.RegisterEntryPoint<MenuBgmController>().WithParameter(music);
            }

            if (_endingBgm != null)
            {
                var endingObject = new GameObject("Ending BGM");
                endingObject.transform.SetParent(transform, false);
                var ending = endingObject.AddComponent<AudioSource>();
                ending.playOnAwake = false;
                ending.loop = true;
                ending.spatialBlend = 0f;
                ending.clip = _endingBgm;
                builder.RegisterEntryPoint<EndingBgmController>().WithParameter(ending);
            }

            // Makes a saved choice real. Registered here rather than in
            // RegisterServices because only the application has a window to
            // resize; a test container must not touch one.
            builder.RegisterEntryPoint<GraphicsSettingsStartup>();
            builder.RegisterEntryPoint<CameraSettingsBinder>();
            builder.RegisterEntryPoint<KeySettingGuideBinder>();

            // Only when a picture was given: the system arrow needs no setting,
            // and a test container has no texture to hand over.
            if (_cursor != null)
            {
                builder.RegisterEntryPoint<CursorSkin>()
                    .WithParameter(_cursor)
                    .WithParameter(_cursorHotspot);
            }
            builder.RegisterEntryPoint<NetworkInterfaceSettings>();

            // Built here rather than in RegisterServices: the device identifier
            // is this machine's saved credential, and a test container must not
            // pick up the account belonging to whoever last played here.
            RegisterBackend(builder, _backendBaseUrl);

            // Shared across Playground and Result so scene unloading cannot reveal gameplay.
            var transition = new GameObject("Highlight Transition").AddComponent<HighlightTransitionView>();
            transition.transform.SetParent(transform, false);
            builder.RegisterComponent(transition).As<IHighlightTransitionView>();

            var loading = LoadingView.Create(null);
            Object.DontDestroyOnLoad(loading.gameObject);
            loading.HideImmediate();
            builder.RegisterComponent(loading).As<ILoadingView>();
            builder.RegisterBuildCallback(container =>
                container.Resolve<ILoadingOverlay>().Attach(container.Resolve<ILoadingView>()));
            builder.RegisterEntryPoint<LoadingSceneCoordinator>();
            builder.RegisterEntryPoint<LoadingOverlayCoordinator>();

            var inputObject = new GameObject("UI EventSystem");
            inputObject.SetActive(false);
            inputObject.transform.SetParent(transform, false);
            EventSystem eventSystem = inputObject.AddComponent<ExclusiveEventSystem>();
            var inputModule = inputObject.AddComponent<InputSystemUIInputModule>();
            inputObject.AddComponent<SharedUiInputActions>().Bind(inputModule);
            inputObject.SetActive(true);
            builder.RegisterComponent(eventSystem);
            UiButtonClickAudio.Create(transform, _uiButtonClick);

#if UNITY_WEBGL && !UNITY_EDITOR
            var webText = new GameObject("Web Text Input").AddComponent<Game.Client.Common.WebTextInput>();
            webText.transform.SetParent(transform, false);
            builder.RegisterComponent(webText);

#endif

            var performance = new GameObject("Frame Capture");
            performance.transform.SetParent(transform, false);
            builder.RegisterComponent(performance.AddComponent<Game.Client.Common.WebFrameCapture>());

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
            var endpoint = new BackendEndpoint(baseUrl);
            var session = new BackendSession(DedicatedServerStartup.IsRequested ? DedicatedServerStartup.DeviceId() : DeviceIdentity.Current());
            var client = new BackendClient(new UnityWebRequestTransport(), endpoint, session);
            builder.RegisterInstance(new MatchAnalyticsUpload(new UnityWebRequestTransport(), endpoint,
                System.IO.Path.Combine(Application.persistentDataPath, "match-analytics")));

            // Only a dedicated server relays chat, so only it needs the word list or posts the
            // records. A player's build never learns the internal address and never holds the
            // key, which is what keeps the list off the machines that would type around it.
            if (DedicatedServerStartup.IsRequested)
            {
                builder.RegisterInstance(new ChatModerationService(
                        new UnityWebRequestTransport(),
                        new BackendEndpoint(DedicatedServerStartup.Argument(
                            "-internalUrl", "http://127.0.0.1:8080")),
                        DedicatedServerStartup.Secret("D205_CHAT_KEY")))
                    .AsSelf()
                    .As<IChatModeration>();
            }
            builder.RegisterEntryPoint<MatchAnalyticsRecorder>();
            builder.RegisterInstance<IHighlightDirectorGateway>(new HighlightDirectorGateway(client));
            if (DedicatedServerStartup.IsRequested)
            {
                builder.RegisterInstance<IAccountGateway>(new AccountGateway(client));
                // VContainer resolves optional constructor parameters too. A server
                // must explicitly omit the player's saved-credential fallback.
                builder.RegisterEntryPoint<BackendSignIn>().AsSelf().As<IAccountReady>()
                    .WithParameter(typeof(IPhotonCredentialStore), (object)null);
                return;
            }

            // First, because the presence gateway sends over it when it is up.
            var frames = RegisterNotifications(builder, endpoint, session);

            // The client itself is not registered. Nothing above this line has a
            // reason to hold it, and a container that hands it out is one where
            // a presenter can send its own request and skip the ports entirely.
            builder.RegisterInstance<IAccountGateway>(new AccountGateway(client));
            builder.RegisterInstance<IFriendGateway>(new FriendGateway(client));
            builder.RegisterInstance<IPresenceGateway>(new PresenceGateway(client, frames));
            builder.RegisterInstance<IInviteGateway>(new InviteGateway(client));
            builder.RegisterInstance<IReportGateway>(new ReportGateway(client));
            builder.RegisterInstance<IFeedbackGateway>(new FeedbackGateway(client));

            // Registered beside the gateways rather than in RegisterServices,
            // because it needs one. A test container that builds only the
            // services has no backend to command.
            builder.Register<FriendUiCommands>(Lifetime.Singleton);

            // Registered as itself as well as an entry point, because the two
            // things that wait on it resolve it. One registration, so they wait
            // on the sign-in that actually ran rather than on a second instance
            // that never started.
            builder.RegisterEntryPoint<BackendSignIn>()
                .AsSelf()
                .As<IAccountReady>()
                .As<BackendSignIn>();
            builder.RegisterEntryPoint<PresenceHeartbeat>();

            // Waits on that sign-in and dresses the player in what the account
            // remembers. Registered beside it rather than in RegisterServices,
            // because without an account there is nothing to remember.
            builder.RegisterEntryPoint<AvatarAppearanceSeed>();
        }

        /// <summary>
        /// Registers the realtime channel, or a silent stand-in when the socket
        /// package is not in this build.
        /// </summary>
        /// <remarks>
        /// Two branches so that <see cref="INotificationStream"/> always resolves.
        /// The screens that subscribe to it should not each have to ask whether
        /// there is a channel; with the stand-in they simply never hear anything,
        /// which is what they heard before the channel existed.
        /// <para>
        /// The define is set by this assembly's versionDefines when
        /// com.endel.nativewebsocket resolves. Without it the adapter is not
        /// compiled at all, which is also what keeps <c>dotnet build</c> — which
        /// never sees Unity packages — building the rest of the assembly.
        /// </para>
        /// </remarks>
        /// <returns>
        /// The sender the presence gateway puts its frames through. Returned
        /// rather than resolved so the gateway, which is built by hand above, can
        /// be handed the same instance the container holds.
        /// </returns>
        private static INotificationFrameSender RegisterNotifications(
            IContainerBuilder builder, BackendEndpoint endpoint, BackendSession session)
        {
#if NATIVEWEBSOCKET_PRESENT
            var transport = new NativeWebSocketTransport(endpoint.TimeoutSeconds);
            builder.RegisterInstance(transport).As<IWebSocketTransport>().As<ITickable>();

            var stream = new WebSocketNotificationStream(transport, endpoint, session);
            builder.RegisterInstance(stream)
                .As<INotificationStream>()
                .As<INotificationFrameSender>()
                .AsSelf();

            builder.RegisterEntryPoint<NotificationLink>();
            return stream;
#else
            Debug.LogWarning(
                "[Notifications] NativeWebSocket is not in this build. Realtime notifications are off; "
                + "lists refresh when opened, as before.");
            var silent = new SilentNotificationStream();
            builder.RegisterInstance(silent)
                .As<INotificationStream>()
                .As<INotificationFrameSender>();
            return silent;
#endif
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
        /// <param name="registerNullMicrophoneTest">
        /// True for tests and the dedicated server, which must not open a
        /// microphone. The client passes false and registers the loopback
        /// itself.
        /// </param>
        public static void RegisterServices(
            IContainerBuilder builder,
            NetworkPrefabs networkPrefabs = null,
            NetworkScenes networkScenes = null,
            PlayerProfile profile = null,
            ServerRegionSystem regions = null,
            GeneralSettingsSystem generalSettings = null,
            GraphicsSettingsSystem graphicsSettings = null,
            InterfaceSettingsSystem interfaceSettings = null,
            SoundSettingsSystem soundSettings = null,
            ControlSettingsSystem controlSettings = null,
            NotificationSettingsSystem notificationSettings = null,
            bool registerNullMicrophoneTest = true)
        {
            builder.Register<AppFlowSystem>(Lifetime.Singleton);
            builder.Register<HomeMenuSystem>(Lifetime.Singleton);
            builder.Register<FriendListSystem>(Lifetime.Singleton);
            builder.Register<InterfacePresentation>(Lifetime.Singleton);

            // One pseudonym for the whole visit, so the room list and the room
            // itself call a player the same thing.
            builder.Register<PublishedPlayerName>(Lifetime.Singleton);
            builder.Register<FriendSearchSystem>(Lifetime.Singleton);

            // Registered here so every container has one, with a store that
            // forgets when the process does. The application replaces it with
            // one backed by this machine's preferences.
            builder.RegisterInstance(
                regions ?? new ServerRegionSystem(new InMemoryServerRegionStore()));

            // Likewise: one for every container, forgetting with the process
            // unless the application hands in one backed by preferences.
            builder.RegisterInstance(
                generalSettings ?? new GeneralSettingsSystem(new InMemoryGeneralSettingsStore()));

            // Words the interface draws, in the language last applied. Built
            // by hand so a test can hand the locale its own catalogue without
            // VContainer looking for one.
            builder.Register(
                c => new UiLocale(c.Resolve<GeneralSettingsSystem>()),
                Lifetime.Singleton);

            // Built with the container rather than on first use. Scenes that
            // inject nothing - the tutorial is one - read the applied language
            // through UiLocale.Current, and a lazy singleton would leave them
            // in Korean until some other screen happened to ask for it.
            builder.RegisterBuildCallback(container => container.Resolve<UiLocale>());

            // Forgetting with the process, and changing nothing about the
            // picture, unless the application hands in one backed by
            // preferences and wired to the renderer.
            builder.RegisterInstance(
                graphicsSettings ?? new GraphicsSettingsSystem(new InMemoryGraphicsSettingsStore()));

            builder.RegisterInstance(
                interfaceSettings ?? new InterfaceSettingsSystem(new InMemoryInterfaceSettingsStore()));

            builder.RegisterInstance(
                soundSettings ?? new SoundSettingsSystem(new InMemorySoundSettingsStore()));

            builder.RegisterInstance(
                controlSettings ?? new ControlSettingsSystem(new InMemoryControlSettingsStore()));

            builder.RegisterInstance(
                notificationSettings
                    ?? new NotificationSettingsSystem(new InMemoryNotificationSettingsStore()));

            // The application registers the loopback after this method so
            // only a client with speakers opens the microphone. Tests and
            // the dedicated server keep the no-op.
            if (registerNullMicrophoneTest)
            {
                builder.Register<IMicrophoneTest, NullMicrophoneTest>(Lifetime.Singleton);
            }

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

            // Which match a report is about, read off the room above. One for
            // the application because the room is (S15P21D205-1018).
            builder.Register<RoomReportContext>(Lifetime.Singleton).As<IReportContext>();

            // One instance for the whole application, for the same reason the
            // profile is: the closet writes what was applied and the lobby
            // reads it, and a copy per screen would dress the player
            // differently depending on where they were looked at.
            builder.Register<AvatarAppearanceState>(Lifetime.Singleton);
            builder.RegisterEntryPoint<NetworkAvatarAppearancePresenter>();

            builder.Register<LoadingOverlay>(Lifetime.Singleton).As<ILoadingOverlay>().AsSelf();

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
                        c.Resolve<ServerRegionSystem>(),
                        c.Resolve<PublishedPlayerName>(),

                        // Photon 접속이 로그인을 기다리게 합니다(S15P21D205-928).
                        // 이것이 없으면 토큰이 도착하기 전에 붙어 인증값 없이
                        // 접속하고, Photon 이 익명을 막고 있으면 거절당합니다.
                        c.TryResolve<IAccountReady>(out var accountReady)
                            ? accountReady
                            : null,

                        // 전용 서버에만 등록됩니다. 없으면 채팅은 지금과 똑같이 지나갑니다.
                        c.TryResolve<IChatModeration>(out var chatModeration)
                            ? chatModeration
                            : null),
                    Lifetime.Singleton)
                .AsSelf()
                .As<IRoomSessionProbe>()
                .As<INetworkMatchRuntimeSource>()
                .As<INetworkMatchAuthority>()
                .As<INetworkMatchEvents>()
                .As<IMatchAnalyticsSource>()
                .As<INetworkResultNavigation>()
                .As<ILobbyChatTransport>()
                .As<IMatchChatTransport>();
            builder.RegisterEntryPoint<NetworkMatchFlowSynchronizer>();
            builder.RegisterEntryPoint<NetworkResultLobbyReturnController>().AsSelf();
            // Outlives every screen. The rig that opens the microphone is
            // rebuilt with each session and the control that drives it with each
            // screen, but a player who muted themselves meant it to hold —
            // including the next room. The store is this machine's when one is
            // registered; tests and the dedicated server keep it in memory.
            builder.Register(
                c => new VoicePreferences(
                    c.TryResolve<IVoicePreferencesStore>(out var store)
                        ? store
                        : new InMemoryVoicePreferencesStore()),
                Lifetime.Singleton);

            // Outlives every screen. The camera rig is rebuilt with each scene,
            // but a player who switched to first person in the lobby meant it
            // to hold — including the match and the lobby they return to. The
            // store is this machine's when one is registered; tests and the
            // dedicated server keep it in memory.
            builder.Register(
                c => new CameraViewPreference(
                    c.TryResolve<ICameraViewStore>(out var store)
                        ? store
                        : new InMemoryCameraViewStore()),
                Lifetime.Singleton);

            builder.Register<RoomCodeGenerator>(Lifetime.Singleton);
            builder.Register<IRoomBrowser, RoomBrowser>(Lifetime.Singleton);
            builder.Register<RoomUiCommands>(Lifetime.Singleton);
        }
    }
}
