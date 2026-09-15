using System;
using System.Collections.Generic;
using Game.Client.Cameras;
using Game.Client.Interactions;
using Game.Client.Home;
using Game.Client.Common;
using Game.Client.Lobby;
using Game.Client.Match;
using Game.Client.Players;
using Game.Client.Voice;
using Game.Client.Settings;
using Game.Client.Character;
using Game.Core.Settings;
using Game.Core.Home;
using Game.Core.Lobby;
using Game.Core.Maps;
using Game.Core.Ports;
using Game.Core.Rooms;
using Game.Network.Players;
using Game.Network.Session;
using R3;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    public sealed class LobbyLifetimeScope : LifetimeScope
    {
        [SerializeField]
        private LobbyHudView hudView;

        [SerializeField]
        private LobbyPauseMenuView pauseMenuView;

        [SerializeField]
        private LobbyPlayerListView playerListView;

        [SerializeField]
        private PlaySettingsView playSettingsView;

        [SerializeField]
        private KickConfirmView kickConfirmView;

        [SerializeField]
        private MatchChatView chatView;

        [SerializeField]
        private MatchChatBubbleView chatBubbleView;

        [SerializeField]
        private VoiceView voiceView;

        /// <summary>
        /// Read for the talk key. Held here rather than reached through the
        /// camera rig, which keeps its own copy private and is a prefab this
        /// scope has not instantiated yet when the container is built.
        /// </summary>
        [SerializeField]
        private InputActionAsset inputActions;

        [SerializeField]
        private PlayerCameraController cameraRigPrefab;

        [SerializeField]
        private MatchSceneConfiguration sceneConfiguration;

        [SerializeField]
        [Tooltip("The wardrobe the character overlay is filled from. The same " +
                 "asset the Home closet scene uses.")]
        private AvatarPartCatalog partCatalog;

        private NetworkRunnerService stagingNetwork;
        private GameObject[] sceneRoots = Array.Empty<GameObject>();
        private readonly HashSet<CarryableItem> lobbyItems = new();
        internal bool OwnsItem(CarryableItem item) => lobbyItems.Contains(item);
        private Renderer[] stagingRenderers = Array.Empty<Renderer>();
        private bool[] stagingRendererStates = Array.Empty<bool>();
        private Collider[] stagingColliders = Array.Empty<Collider>();
        private bool[] stagingColliderStates = Array.Empty<bool>();
        private Behaviour[] stagingBehaviours = Array.Empty<Behaviour>();
        private bool[] stagingBehaviourStates = Array.Empty<bool>();
        private Behaviour[] outgoingBehaviours = Array.Empty<Behaviour>();
        private bool[] outgoingBehaviourStates = Array.Empty<bool>();
        private Renderer[] outgoingRenderers = Array.Empty<Renderer>();
        private bool[] outgoingRendererStates = Array.Empty<bool>();
        private bool[] outgoingRendererEnabledStates = Array.Empty<bool>();
        private Collider[] outgoingColliders = Array.Empty<Collider>();
        private bool[] outgoingColliderStates = Array.Empty<bool>();
        private bool highlightStaging;
        private bool stagingVisible;
        private int diagnosticSamples;
        private float nextDiagnosticTime;

        private sealed class LobbyStartCountdown : ITickable
        {
            private readonly NetworkRunnerService network;
            private readonly LobbyHudView view;
            private readonly LobbyPauseMenuPresenter pauseMenu;
            private bool dismissedForCountdown;

            public LobbyStartCountdown(
                NetworkRunnerService network,
                LobbyHudView view,
                LobbyPauseMenuPresenter pauseMenu)
            {
                this.network = network;
                this.view = view;
                this.pauseMenu = pauseMenu;
            }

            public void Tick()
            {
                var remaining = network.StartCountdownRemaining;
                view.SetStartCountdown(remaining);
                if (remaining <= 0d)
                {
                    dismissedForCountdown = false;
                    return;
                }

                if (dismissedForCountdown)
                {
                    return;
                }

                dismissedForCountdown = true;
                pauseMenu.DismissForMatchStart();
            }
        }

        protected override void Awake()
        {
            // Fusion can merge additive content into its runner scene after
            // Awake. Keep the roots that actually arrived with Lobby so match
            // objects are never shifted or hidden with it.
            sceneRoots = gameObject.scene.GetRootGameObjects();
            foreach (var root in sceneRoots)
            {
                if (root == null) continue;
                foreach (var item in root.GetComponentsInChildren<CarryableItem>(true)) lobbyItems.Add(item);
            }
            base.Awake();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            if (DedicatedServerStartup.IsRequested)
            {
                builder.RegisterEntryPoint<NetworkInteractionSceneBridge>()
                    .WithParameter(true).WithParameter(gameObject.scene).AsSelf();
                builder.RegisterBuildCallback(c =>
                {
                    c.Resolve<NetworkInteractionSceneBridge>().BindSceneItems(lobbyItems);
                    PrepareLobbyPhysics(c.Resolve<NetworkRunnerService>());
                });
                return;
            }
            var configureStartedAt = Time.realtimeSinceStartupAsDouble;
            if (hudView == null)
            {
                throw new InvalidOperationException("LobbyHudView must be assigned.");
            }

            if (pauseMenuView == null)
            {
                pauseMenuView = hudView.GetComponent<LobbyPauseMenuView>();
            }

            if (pauseMenuView == null)
            {
                throw new InvalidOperationException(
                    "LobbyPauseMenuView must be assigned. Lobby 씬에서 Game > Lobby > Build HUD Layout 을 실행하세요.");
            }

            if (playerListView == null)
            {
                throw new InvalidOperationException(
                    "LobbyPlayerListView must be assigned. Lobby 씬에서 Game > Lobby > Build HUD Layout 을 실행하세요.");
            }

            if (playSettingsView == null || kickConfirmView == null)
            {
                throw new InvalidOperationException(
                    "Host UI views must be assigned. Lobby 씬에서 Game > Lobby > Build HUD Layout 을 실행하세요.");
            }

            if (chatView == null || chatBubbleView == null)
            {
                throw new InvalidOperationException(
                    "Chat views must be assigned. Lobby 씬에서 Game > Lobby > Build HUD Layout 을 실행하세요.");
            }

            if (cameraRigPrefab == null)
            {
                throw new InvalidOperationException("PlayerCameraRig prefab must be assigned.");
            }

            builder.Register<UnityHomeApplicationHost>(Lifetime.Scoped).As<IHomeApplicationHost>();
            builder.RegisterComponent(hudView);
            builder.RegisterBuildCallback(c =>
            {
                var network = c.Resolve<NetworkRunnerService>();
                hudView.gameObject.AddComponent<Game.Client.Settings.InterfaceHudView>()
                    .Bind(c.Resolve<InterfaceSettingsSystem>(), () => network.LocalPingMilliseconds);
            });
            builder.RegisterEntryPoint<LobbyStartCountdown>();
            var shortcutOverlay = hudView.EnsureShortcutOverlay();
            shortcutOverlay.BindPlayerList(playerListView);
            builder.RegisterComponent(shortcutOverlay).As<ILobbyShortcutOverlay>();
            builder.RegisterComponent(hudView.EnsurePlayerCount()).As<ILobbyPlayerCountView>();
            builder.RegisterComponent(pauseMenuView).As<ILobbyPauseMenuView>();
            builder.RegisterComponent(playerListView).As<ILobbyPlayerListView>();
            builder.RegisterComponent(playSettingsView).As<IPlaySettingsView>();
            builder.RegisterComponent(kickConfirmView).As<ILobbyConfirmView>();
            chatView.SetKeepChromeVisible(true);
            builder.RegisterComponent(chatView).As<IChatView>();
            builder.RegisterComponent(chatBubbleView).As<IMatchChatBubbleView>();
            builder.RegisterComponent(voiceView).As<IVoiceView>();
            builder.RegisterInstance(inputActions);

            // Registered beside the asset, which the project scope has no
            // reference to. Puts the player's chosen keys on it as the lobby
            // comes up, and again whenever the 컨트롤 tab settles on new ones.
            builder.RegisterInstance<IControlBindingApplier>(
                new InputSystemControlBindingApplier(inputActions));
            builder.RegisterEntryPoint<ControlBindingBridge>();

            // An entry point because it mirrors the per-session rig every frame,
            // and a plain registration would never be ticked.
            builder.RegisterEntryPoint<NetworkVoiceControl>().As<IVoiceControl>();
            builder.Register<NetworkLobbyParticipantList>(Lifetime.Scoped)
                .As<ILobbyParticipantList>();

            // Standalone scenes retain defaults until the network session is ready.
            builder.Register(
                    c => new NetworkLobbyHostSession(
                        c.Resolve<RoomBrowserSystem>(),
                        c.Resolve<NetworkRunnerService>(),
                        CreateUnsyncedSettings()),
                    Lifetime.Scoped)
                .As<ILobbyHostSession>().As<ITickable>();
            builder.Register(
                    c => CreateChatLog(
                        c.Resolve<RoomBrowserSystem>(),
                        c.Resolve<PlayerProfile>()),
                    Lifetime.Scoped)
                .As<ILobbyChatLog>();
            builder.RegisterEntryPoint<LobbyPlayerListPresenter>();
            builder.RegisterEntryPoint<LobbyFriendRefresh>();
            builder.RegisterEntryPoint<LobbyPauseMenuPresenter>().AsSelf();
            var settingsObject = new GameObject("Lobby Settings");
            settingsObject.transform.SetParent(transform, false);
            settingsObject.SetActive(false);
            var settingsView = settingsObject.AddComponent<SettingsView>();
            settingsView.ConfigureAsLobbyOverlay();
            builder.RegisterComponent(settingsView).As<ISettingsView>().AsSelf();
            builder.RegisterEntryPoint<SettingsPresenter>().AsSelf()
                .WithParameter<Action>(() => settingsObject.SetActive(false));
            builder.RegisterEntryPoint<LobbySettingsOverlay>().WithParameter(chatView);
            if (partCatalog == null)
            {
                Debug.LogError(
                    "AvatarPartCatalog must be assigned on LobbyLifetimeScope.",
                    this);
            }
            else
            {
                var closetObject = new GameObject("Lobby Character Closet");
                closetObject.transform.SetParent(transform, false);
                closetObject.SetActive(false);
                var closetView = closetObject.AddComponent<CharacterClosetView>();
                closetView.ConfigureAsLobbyOverlay();
                builder.RegisterInstance(partCatalog);
                builder.RegisterComponent(closetView).As<ICharacterClosetView>().AsSelf();
                builder.RegisterEntryPoint<CharacterClosetPresenter>().AsSelf()
                    .WithParameter<Action>(() => closetObject.SetActive(false));
                builder.RegisterEntryPoint<ClosetAppearanceSaver>();
                builder.RegisterEntryPoint<LobbyCharacterOverlay>().WithParameter(chatView);
            }

            builder.RegisterEntryPoint<PlaySettingsPresenter>();
            builder.RegisterEntryPoint<LobbyMatchInfoPresenter>();
            // The board in the room opens the same play settings screen; it
            // reaches the menu through IPlaySettingsOpener, which the pause
            // menu presenter implements.
            builder.RegisterEntryPoint<LobbyPlanBoardPresenter>();
            builder.RegisterEntryPoint<VoicePresenter>();
            builder.RegisterEntryPoint<LobbyChatPresenter>();
            builder.RegisterEntryPoint<ChatBubbleBinder>();
            builder.RegisterEntryPoint<InGamePlayerNameplatePresenter>();
            // Scene-owned: leaving the lobby also removes its entry cover.
            // Do not reuse the project-wide highlight/result transition's state.
            var entryCover = new GameObject("Lobby Entry Transition").AddComponent<HighlightTransitionView>();
            entryCover.transform.SetParent(transform, false);
            entryCover.SetOpacity(1f);
            builder.RegisterComponent(entryCover).As<IHighlightTransitionView>();
            builder.RegisterEntryPoint<LobbyPlayerCameraBinder>();
            builder.RegisterEntryPoint<LobbyPlayerAnimationBinder>();
            builder.RegisterEntryPoint<NetworkInteractionSceneBridge>()
                .WithParameter(true).WithParameter(gameObject.scene).AsSelf();
            builder.RegisterBuildCallback(c => c.Resolve<NetworkInteractionSceneBridge>().BindSceneItems(lobbyItems));
            // Voluntary requests reach the project-owned session/exit flow through the bridge.
            builder.Register<LobbyExitPresenter>(Lifetime.Scoped);
            builder.RegisterEntryPoint<NetworkLobbyExitBridge>();

            builder.RegisterBuildCallback(container =>
            {
                var network = container.Resolve<NetworkRunnerService>();
                if (sceneConfiguration == null)
                {
                    throw new InvalidOperationException(
                        "Lobby requires the same scene spawn configuration used by matches.");
                }

                // The avatar is created in Room before Lobby's floor exists.
                // UI scene changes do not pass through Fusion's scene loader,
                // so hand the scene-owned points over once this scene is ready.
                PrepareLobbyPhysics(network);
                if (network.IsHighlightInProgress)
                    PrepareHighlightStaging(network);
                EnsurePlayerCameraRig(container.Resolve<ControlSettingsSystem>());
                if (highlightStaging)
                {
                    CaptureStagingPresentation();
                    SetStagingVisible(false);
                }
                Debug.Log(
                    $"[SceneTiming] Lobby scope ready, " +
                    $"elapsed={Time.realtimeSinceStartupAsDouble - configureStartedAt:F3}s.");
            });
        }

        private void Update()
        {
            if (diagnosticSamples > 0 && Time.unscaledTime >= nextDiagnosticTime)
            {
                diagnosticSamples--;
                nextDiagnosticTime = Time.unscaledTime + 1f;
                MatchTransitionDiagnostics.Dump($"lobby-sample staging={highlightStaging} visible={stagingVisible} outgoing={outgoingRenderers.Length} highlight={stagingNetwork?.IsHighlightInProgress} localComplete={stagingNetwork?.IsLocalHighlightComplete}");
            }
            if (!highlightStaging || stagingNetwork == null) return;
            if (!stagingNetwork.IsHighlightInProgress)
            {
                // A skipped viewer already completed this handoff. Do not scan
                // every outgoing renderer/collider again at the shared boundary.
                if (!stagingVisible) SetStagingVisible(true);
                highlightStaging = false;
                return;
            }

            var visible = stagingNetwork.IsLocalHighlightComplete;
            if (visible != stagingVisible) SetStagingVisible(visible);
            // Apply outgoing visibility once at the handoff, after replay cleanup.
        }


        private void PrepareLobbyPhysics(NetworkRunnerService network)
        {
            if (network.IsHighlightInProgress)
            {
                // The replay keeps the match scene loaded, but skipped players are
                // already simulated in Lobby. Both maps occupy the same world space.
                // Remove the old collision world on authority as well as on the
                // skipped client; otherwise server corrections pin players to walls
                // they cannot see until the match scene finally unloads.
                var playground = FindFirstObjectByType<PlaygroundLifetimeScope>(FindObjectsInactive.Include);
                if (playground != null)
                {
                    playground.SuspendLiveInteractionsForHighlights();
                    if (network.IsServer && DedicatedServerStartup.IsRequested)
                        playground.BeginRetiredMapCleanup();
                    foreach (var root in playground.SceneRoots)
                    {
                        if (root == null) continue;
                        foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                            collider.enabled = false;
                    }
                }
            }
            network.RepositionPlayers(sceneConfiguration.CaptureSpawnPoses());
        }

        private void PrepareHighlightStaging(NetworkRunnerService network)
        {
            stagingNetwork = network;
            highlightStaging = true;
            MatchTransitionDiagnostics.Dump("lobby-staging-prepare");
        }

        private void CaptureStagingPresentation()
        {
            var renderers = new List<Renderer>();
            var colliders = new List<Collider>();
            var behaviours = new List<Behaviour>();
            foreach (var root in sceneRoots)
            {
                if (root == null) continue;
                renderers.AddRange(root.GetComponentsInChildren<Renderer>(true));
                colliders.AddRange(root.GetComponentsInChildren<Collider>(true));
                foreach (var behaviour in root.GetComponentsInChildren<Behaviour>(true))
                {
                    if (behaviour is Camera or Canvas or AudioListener or AudioSource or
                        EventSystem or Light or HighlightTransitionView)
                        behaviours.Add(behaviour);
                }
            }

            stagingRenderers = renderers.ToArray();
            stagingRendererStates = new bool[stagingRenderers.Length];
            for (var index = 0; index < stagingRenderers.Length; index++)
                stagingRendererStates[index] = stagingRenderers[index].forceRenderingOff;
            stagingColliders = colliders.ToArray();
            stagingColliderStates = new bool[stagingColliders.Length];
            for (var index = 0; index < stagingColliders.Length; index++)
                stagingColliderStates[index] = stagingColliders[index].enabled;
            stagingBehaviours = behaviours.ToArray();
            stagingBehaviourStates = new bool[stagingBehaviours.Length];
            for (var index = 0; index < stagingBehaviours.Length; index++)
                stagingBehaviourStates[index] = stagingBehaviours[index].enabled;

            var playground = FindFirstObjectByType<PlaygroundLifetimeScope>(
                FindObjectsInactive.Include);
            if (playground == null) return;
            var outgoing = new List<Behaviour>();
            var outgoingMeshes = new List<Renderer>();
            var outgoingBodies = new List<Collider>();
            foreach (var root in playground.SceneRoots)
            {
                // Assigned items are roots too, and may have been destroyed during the match.
                // The gameplay rig is transferred to Lobby but remains in the old root snapshot.
                if (root == null || root.GetComponent<PlayerCameraController>() != null ||
                    (root.GetComponent<Camera>() != null && root.scene == gameObject.scene)) continue;
                outgoingMeshes.AddRange(root.GetComponentsInChildren<Renderer>(true));
                outgoingBodies.AddRange(root.GetComponentsInChildren<Collider>(true));
                foreach (var behaviour in root.GetComponentsInChildren<Behaviour>(true))
                {
                    if (behaviour is Camera or Canvas or AudioListener or AudioSource or
                        UnityEngine.Video.VideoPlayer or EventSystem or Light)
                        outgoing.Add(behaviour);
                }
            }
            outgoingBehaviours = outgoing.ToArray();
            outgoingBehaviourStates = new bool[outgoingBehaviours.Length];
            for (var index = 0; index < outgoingBehaviours.Length; index++)
                outgoingBehaviourStates[index] = outgoingBehaviours[index].enabled;
            outgoingRenderers = outgoingMeshes.ToArray();
            outgoingRendererStates = new bool[outgoingRenderers.Length];
            outgoingRendererEnabledStates = new bool[outgoingRenderers.Length];
            for (var index = 0; index < outgoingRenderers.Length; index++)
            {
                outgoingRendererStates[index] = outgoingRenderers[index].forceRenderingOff;
                outgoingRendererEnabledStates[index] = outgoingRenderers[index].enabled;
            }
            outgoingColliders = outgoingBodies.ToArray();
            outgoingColliderStates = new bool[outgoingColliders.Length];
            for (var index = 0; index < outgoingColliders.Length; index++)
                outgoingColliderStates[index] = outgoingColliders[index].enabled;
        }

        private void HideOutgoingGeometry()
        {
            for (var index = 0; index < outgoingRenderers.Length; index++)
                if (outgoingRenderers[index] != null)
                {
                    outgoingRenderers[index].forceRenderingOff = true;
                    outgoingRenderers[index].enabled = false;
                }
            for (var index = 0; index < outgoingColliders.Length; index++)
                if (outgoingColliders[index] != null)
                    outgoingColliders[index].enabled = false;
        }

        private void SetStagingVisible(bool visible)
        {
            if (visible && !stagingVisible)
            {
                diagnosticSamples = 3;
                nextDiagnosticTime = Time.unscaledTime;
            }
            stagingVisible = visible;
            for (var index = 0; index < stagingRenderers.Length; index++)
                if (stagingRenderers[index] != null)
                    stagingRenderers[index].forceRenderingOff =
                        !visible || stagingRendererStates[index];
            for (var index = 0; index < stagingColliders.Length; index++)
                if (stagingColliders[index] != null)
                    stagingColliders[index].enabled =
                        visible && stagingColliderStates[index];
            for (var index = 0; index < stagingBehaviours.Length; index++)
                if (stagingBehaviours[index] != null)
                    stagingBehaviours[index].enabled =
                        visible && stagingBehaviourStates[index];
            for (var index = 0; index < outgoingBehaviours.Length; index++)
                if (outgoingBehaviours[index] != null)
                    outgoingBehaviours[index].enabled =
                        !visible && outgoingBehaviourStates[index];
            if (visible && gameObject.scene.isLoaded)
            {
                // The outgoing scene can stay loaded until every peer finishes.
                // Hide its geometry and collisions before revealing the lobby.
                HideOutgoingGeometry();
                // This peer has finished viewing; other clients keep their own map intact.
                FindFirstObjectByType<PlaygroundLifetimeScope>(FindObjectsInactive.Include)
                    ?.BeginRetiredMapCleanup();
                SceneManager.SetActiveScene(gameObject.scene);
                foreach (var cover in FindObjectsByType<HighlightTransitionView>(
                             FindObjectsInactive.Include,
                             FindObjectsSortMode.None))
                    if (cover.gameObject.name == "Highlight Transition")
                        cover.SetOpacity(0f);
            }
            else if (!visible)
            {
                for (var index = 0; index < outgoingRenderers.Length; index++)
                    if (outgoingRenderers[index] != null)
                    {
                        outgoingRenderers[index].forceRenderingOff = outgoingRendererStates[index];
                        outgoingRenderers[index].enabled = outgoingRendererEnabledStates[index];
                    }
                for (var index = 0; index < outgoingColliders.Length; index++)
                    if (outgoingColliders[index] != null)
                        outgoingColliders[index].enabled = outgoingColliderStates[index];
            }
        }

        /// <summary>
        /// Makes sure the lobby has a camera rig to look through.
        /// </summary>
        /// <remarks>
        /// The cursor is not set here. Whether it is captured depends on whether
        /// the Esc menu is open, which is live state that a single call while the
        /// scene loads cannot hold — <see cref="LobbyPauseMenuPresenter"/> owns
        /// it.
        /// <para>
        /// The follow target is not looked for here either. On a client the
        /// avatar is a replicated object that has not arrived yet at this point
        /// in the scene load — measured as zero avatars present, while the host,
        /// which spawns its own locally, always found one.
        /// <see cref="LobbyPlayerCameraBinder"/> waits for it instead.
        /// </para>
        /// </remarks>
        private void EnsurePlayerCameraRig(ControlSettingsSystem settings)
        {
            var rig = FindFirstObjectByType<PlayerCameraController>(FindObjectsInactive.Include);
            if (rig == null) rig = Instantiate(cameraRigPrefab);
            if (highlightStaging)
            {
                // Keep the Brain/output paired with the rig through replay -> lobby.
                // Replacing only the output camera can leave the lobby viewing the replay pose.
                var output = Camera.main;
                if (output != null && output.gameObject.scene != gameObject.scene && output.transform.parent == null)
                {
                    foreach (var root in sceneRoots)
                        if (root != null)
                        {
                            foreach (var other in root.GetComponentsInChildren<Camera>(true))
                                other.enabled = false;
                            foreach (var listener in root.GetComponentsInChildren<AudioListener>(true))
                                listener.enabled = false;
                        }
                    SceneManager.MoveGameObjectToScene(output.gameObject, gameObject.scene);
                    Debug.Log($"[QA-Transition] transferred output camera={output.GetInstanceID()} with rig={rig.GetInstanceID()}");
                }
                if (rig.gameObject.scene != gameObject.scene && rig.transform.parent == null)
                    SceneManager.MoveGameObjectToScene(rig.gameObject, gameObject.scene);
            }
            rig.BindSettings(settings);
            rig.RequireExplicitFollowTarget();
        }

        /// <summary>
        /// The half of the room settings the session does not carry yet. Room
        /// code and player cap are overwritten from the room the peer is in; the
        /// rest waits on S15P21D205-205 and is only here so the settings form
        /// has usable values in the meantime.
        /// </summary>
        private static PlaySettingsDraft CreateUnsyncedSettings()
        {
            return new PlaySettingsDraft(
                string.Empty,
                string.Empty,
                false,
                string.Empty,
                RoomSettings.MaxPlayerCount,
                PlaySettingsDraft.DefaultDestructionLimit,
                MapCatalog.DefaultMapId);
        }

        private static LobbyChatLog CreateChatLog(
            RoomBrowserSystem room,
            PlayerProfile profile)
        {
            var localPlayerId = room.LocalPlayerId.CurrentValue;
            if (string.IsNullOrWhiteSpace(localPlayerId))
            {
                // The roster normally arrives before the scene. This fallback
                // keeps chat usable during the short gap without inventing a
                // name the user can see.
                localPlayerId = "local";
            }

            return new LobbyChatLog(
                localPlayerId,
                profile.Nickname);
        }
    }

    /// <summary>
    /// Points the lobby camera at the local character once it exists.
    /// </summary>
    /// <remarks>
    /// Looking once while the scene loads only ever worked for the host. A
    /// client's character is replicated to it, so at that moment there is no
    /// avatar in the scene at all and the camera was left following nothing for
    /// the whole visit. Binding is retried until both the replicated local avatar
    /// and camera rig exist. A migrated avatar replaces the previous binding.
    /// </remarks>
    internal sealed class LobbyPlayerCameraBinder : IStartable, ITickable, IDisposable
    {
        private readonly NetworkRunnerService network;
        private readonly IHighlightTransitionView entryCover;
        private readonly ILoadingOverlay loading;
        private PlayerAvatar boundAvatar;
        private PlayerCameraController boundRig;
        private int readyFrame = -1;
        private bool entryComplete;
        private float fadeInElapsed;
        private double startedAt;

        public LobbyPlayerCameraBinder(
            NetworkRunnerService network,
            IHighlightTransitionView entryCover,
            ILoadingOverlay loading = null)
        {
            this.network = network ?? throw new ArgumentNullException(nameof(network));
            this.entryCover = entryCover ?? throw new ArgumentNullException(nameof(entryCover));
            this.loading = loading;
        }

        public void Start()
        {
            startedAt = Time.realtimeSinceStartupAsDouble;
            if (!IsWaitingForLocalHighlight()) TryBind();
        }

        public void Tick()
        {
            if (IsWaitingForLocalHighlight())
            {
                UpdateEntryTransition(false, Time.frameCount);
                return;
            }

            TryBind();
            var motor = boundAvatar != null ? boundAvatar.GetComponent<NetworkPlayerMotor>() : null;
            var ready = network.IsRuntimeReady && boundAvatar != null && boundAvatar.PlayerId != null &&
                        boundAvatar.IsOwner && motor != null && motor.IsScenePlacementReady &&
                        boundRig != null && boundRig.isActiveAndEnabled &&
                        boundRig.FollowTarget == boundAvatar.transform;
            UpdateEntryTransition(!network.HasRoomSession || ready, Time.frameCount, Time.unscaledDeltaTime);
            CoverForMatchStart();
        }

        internal void UpdateEntryTransition(bool ready, int frame, float deltaSeconds = 0f)
        {
            if (entryComplete) return;
            // The IMGUI fade renders over the loading canvas. Keep it clear
            // through every readiness phase, not only while waiting for an
            // avatar, or the loading art flashes black and then reappears.
            var loadingPresented = loading != null && loading.IsPresented;
            if (!ready)
            {
                readyFrame = -1;
                fadeInElapsed = 0f;
                if (loadingPresented)
                {
                    entryCover.SetOpacity(0f);
                }
                else
                {
                    entryCover.SetOpacity(1f);
                }

                return;
            }
            if (readyFrame < 0) readyFrame = frame;
            // KCC Render, camera LateUpdate and Cinemachine must see the placed
            // target before revealing it. Lost readiness restarts this wait.
            if (frame - readyFrame < 2)
            {
                entryCover.SetOpacity(loadingPresented ? 0f : 1f);
                return;
            }

            fadeInElapsed += Mathf.Max(0f, deltaSeconds);
            entryCover.SetOpacity(loadingPresented ? 0f : LobbySceneFade.FadeInOpacity(fadeInElapsed));
            if (!LobbySceneFade.IsComplete(fadeInElapsed))
            {
                return;
            }

            entryComplete = true;
            entryCover.SetOpacity(0f);
            loading?.Hide();
            var covers = UnityEngine.Object.FindObjectsByType<HighlightTransitionView>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (var cover in covers) cover.SetOpacity(0f);
            var output = Camera.main;
            Debug.Log(
                $"[SceneTiming] Lobby local player ready, " +
                $"elapsedSinceBinderStart={Time.realtimeSinceStartupAsDouble - startedAt:F3}s, " +
                $"camera={(output == null ? "none" : output.gameObject.scene.name + "/" + output.name)}, " +
                $"coversCleared={covers.Length}.");
        }

        private void CoverForMatchStart()
        {
            if (!entryComplete)
            {
                return;
            }

            if (loading != null)
            {
                return;
            }

            var remaining = network.StartCountdownRemaining;
            if (remaining <= 0d || remaining > LobbySceneFade.DurationSeconds)
            {
                return;
            }

            entryCover.SetOpacity(
                LobbySceneFade.FadeOutOpacity(LobbySceneFade.DurationSeconds - (float)remaining));
        }

        public void Dispose()
        {
            if (!entryComplete)
            {
                entryCover.SetOpacity(0f);
                loading?.HideImmediate();
            }
        }

        private bool IsWaitingForLocalHighlight() =>
            network.IsHighlightInProgress && !network.IsLocalHighlightComplete;

        private void TryBind()
        {
            if (!network.IsRuntimeReady ||
                (boundRig != null && boundAvatar != null && boundAvatar.PlayerId != null && boundAvatar.IsOwner &&
                 boundRig.FollowTarget == boundAvatar.transform))
            {
                return;
            }

            var cameraRig = UnityEngine.Object.FindFirstObjectByType<PlayerCameraController>(
                FindObjectsInactive.Include);
            if (cameraRig == null)
            {
                return;
            }

            var avatars = network.PlayerAvatars;

            for (var i = 0; i < avatars.Count; i++)
            {
                if (avatars[i] == null || avatars[i].PlayerId == null || !avatars[i].IsOwner)
                {
                    continue;
                }
                var motor = avatars[i].GetComponent<NetworkPlayerMotor>();
                if (motor == null || !motor.IsScenePlacementReady) continue;

                motor.LocalPresentationInputBlocked = false;
                cameraRig.SetFollowTarget(avatars[i].transform,
                    boundRig == cameraRig && !ReferenceEquals(boundAvatar, null));
                boundAvatar = avatars[i];
                boundRig = cameraRig;
                return;
            }
        }
    }

    internal sealed class LobbyPlayerAnimationBinder : ITickable
    {
        private readonly NetworkRunnerService network;

        public LobbyPlayerAnimationBinder(NetworkRunnerService network)
        {
            this.network = network ?? throw new ArgumentNullException(nameof(network));
        }

        public void Tick()
        {
            var avatars = network.PlayerAvatars;

            for (var i = 0; i < avatars.Count; i++)
            {
                var avatar = avatars[i];
                if (avatar == null || !avatar.isActiveAndEnabled)
                {
                    continue;
                }

                var motor = avatar.GetComponent<NetworkPlayerMotor>();
                if (motor == null)
                {
                    continue;
                }

                avatar.GetComponent<PlayerMovement>()?.ApplyNetworkPosture(motor.Posture);
                avatar.GetComponent<PlayerAnimationDriver>()?.ApplyNetworkState(
                    motor.AnimationSpeed,
                    motor.AnimationGrounded,
                    motor.AttackSequence,
                    new Vector2(motor.AnimationMoveX, motor.AnimationMoveZ),
                    motor.AnimationCarrying);
            }
        }
    }
}
