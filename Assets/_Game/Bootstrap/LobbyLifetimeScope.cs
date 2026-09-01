using System;
using System.Collections.Generic;
using Game.Client.Cameras;
using Game.Client.Home;
using Game.Client.Lobby;
using Game.Client.Match;
using Game.Client.Players;
using Game.Client.Voice;
using Game.Core.Home;
using Game.Core.Lobby;
using Game.Core.Maps;
using Game.Core.Ports;
using Game.Core.Rooms;
using Game.Network.Players;
using Game.Network.Session;
using R3;
using UnityEngine;
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
        private KeyGuideView keyGuideView;

        [SerializeField]
        private LobbyPauseMenuView pauseMenuView;

        [SerializeField]
        private LobbyPlayerListView playerListView;

        [SerializeField]
        private PlaySettingsView playSettingsView;

        [SerializeField]
        private KickConfirmView kickConfirmView;

        [SerializeField]
        private HostTransferConfirmView transferConfirmView;

        [SerializeField]
        private LobbyChatView chatView;

        [SerializeField]
        private LobbyChatBubbleView chatBubbleView;

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

        protected override void Configure(IContainerBuilder builder)
        {
            var configureStartedAt = Time.realtimeSinceStartupAsDouble;
            if (hudView == null)
            {
                throw new InvalidOperationException("LobbyHudView must be assigned.");
            }

            if (keyGuideView == null)
            {
                keyGuideView = hudView.GetComponent<KeyGuideView>();
            }

            if (keyGuideView == null)
            {
                throw new InvalidOperationException(
                    "KeyGuideView must be assigned. Lobby 씬에서 Game > Lobby > Build HUD Layout 을 실행하세요.");
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

            if (playSettingsView == null || kickConfirmView == null || transferConfirmView == null)
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
            builder.RegisterComponent(keyGuideView).As<IKeyGuideView>();
            builder.RegisterComponent(pauseMenuView).As<ILobbyPauseMenuView>();
            builder.RegisterComponent(playerListView).As<ILobbyPlayerListView>();
            builder.RegisterComponent(playSettingsView).As<IPlaySettingsView>();
            builder.RegisterComponent(kickConfirmView).As<IKickConfirmView>();
            builder.RegisterComponent(transferConfirmView).As<IHostTransferConfirmView>();
            builder.RegisterComponent(chatView).As<ILobbyChatView>();
            builder.RegisterComponent(chatBubbleView)
                .AsSelf()
                .As<ILobbyChatBubbleView>();
            builder.RegisterComponent(voiceView).As<IVoiceView>();
            builder.RegisterInstance(inputActions);

            // An entry point because it mirrors the per-session rig every frame,
            // and a plain registration would never be ticked.
            builder.RegisterEntryPoint<NetworkVoiceControl>().As<IVoiceControl>();
            builder.RegisterInstance<IReadOnlyList<ControlKeyBinding>>(ControlKeyGuide.Bindings);
            builder.Register<NetworkLobbyParticipantList>(Lifetime.Scoped)
                .As<ILobbyParticipantList>();

            // Built by hand because the not-yet-synced half of the settings is a
            // value this scope owns, not a service anything should resolve.
            builder.Register(
                    c => new NetworkLobbyHostSession(
                        c.Resolve<RoomBrowserSystem>(),
                        c.Resolve<NetworkRunnerService>(),
                        CreateUnsyncedSettings()),
                    Lifetime.Scoped)
                .As<ILobbyHostSession>();
            builder.Register(
                    c => CreateChatLog(
                        c.Resolve<RoomBrowserSystem>(),
                        c.Resolve<PlayerProfile>()),
                    Lifetime.Scoped)
                .As<ILobbyChatLog>();
            builder.RegisterEntryPoint<KeyGuidePresenter>();
            builder.RegisterEntryPoint<LobbyPlayerListPresenter>();
            builder.RegisterEntryPoint<LobbyPauseMenuPresenter>();
            builder.RegisterEntryPoint<PlaySettingsPresenter>();
            builder.RegisterEntryPoint<VoicePresenter>();
            builder.RegisterEntryPoint<LobbyChatPresenter>();
            builder.RegisterEntryPoint<LobbyChatBubbleBinder>();
            // Scene-owned: leaving the lobby also removes its entry cover.
            // Do not reuse the project-wide highlight/result transition's state.
            var entryCover = new GameObject("Lobby Entry Transition").AddComponent<HighlightTransitionView>();
            entryCover.transform.SetParent(transform, false);
            // A preloaded additive Lobby must not cover the still-visible Room.
            entryCover.SetOpacity(SceneManager.GetActiveScene() == gameObject.scene ? 1f : 0f);
            builder.RegisterComponent(entryCover).As<IHighlightTransitionView>();
            builder.RegisterEntryPoint<LobbyPlayerCameraBinder>();
            builder.RegisterEntryPoint<LobbyPlayerAnimationBinder>();
            // Voluntary requests reach the project-owned session/exit flow through the bridge.
            builder.Register<LobbyExitPresenter>(Lifetime.Scoped);
            builder.RegisterEntryPoint<NetworkLobbyExitBridge>();

            builder.RegisterBuildCallback(container =>
            {
                var sceneConfiguration =
                    FindAnyObjectByType<MatchSceneConfiguration>();
                if (sceneConfiguration == null)
                {
                    throw new InvalidOperationException(
                        "Lobby requires the same scene spawn configuration used by matches.");
                }

                // The avatar is created in Room before Lobby's floor exists.
                // UI scene changes do not pass through Fusion's scene loader,
                // so hand the scene-owned points over once this scene is ready.
                container.Resolve<NetworkRunnerService>()
                    .RepositionPlayers(sceneConfiguration.CaptureSpawnPoses());
                EnsurePlayerCameraRig();
                Debug.Log(
                    $"[SceneTiming] Lobby scope ready, " +
                    $"elapsed={Time.realtimeSinceStartupAsDouble - configureStartedAt:F3}s.");
            });
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
        private void EnsurePlayerCameraRig()
        {
            var rig = FindFirstObjectByType<PlayerCameraController>(FindObjectsInactive.Include);
            if (rig == null) rig = Instantiate(cameraRigPrefab);
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
        private PlayerAvatar boundAvatar;
        private PlayerCameraController boundRig;
        private readonly int lobbySceneHandle;
        private int readyFrame = -1;
        private bool entryComplete;
        private double startedAt;

        public LobbyPlayerCameraBinder(NetworkRunnerService network, IHighlightTransitionView entryCover)
        {
            this.network = network ?? throw new ArgumentNullException(nameof(network));
            this.entryCover = entryCover ?? throw new ArgumentNullException(nameof(entryCover));
            lobbySceneHandle = entryCover is Component component
                ? component.gameObject.scene.handle
                : -1;
        }

        public void Start()
        {
            startedAt = Time.realtimeSinceStartupAsDouble;
            TryBind();
        }

        public void Tick()
        {
            TryBind();
            if (entryComplete) return;
            var motor = boundAvatar != null ? boundAvatar.GetComponent<NetworkPlayerMotor>() : null;
            var ready = network.IsRuntimeReady && boundAvatar != null && boundAvatar.PlayerId != null &&
                        boundAvatar.IsOwner && motor != null && motor.IsScenePlacementReady &&
                        boundRig != null && boundRig.isActiveAndEnabled &&
                        boundRig.FollowTarget == boundAvatar.transform;
            var lobbyIsActive = lobbySceneHandle < 0 ||
                                SceneManager.GetActiveScene().handle == lobbySceneHandle;
            UpdateEntryTransition(!network.HasRoomSession || ready, lobbyIsActive, Time.frameCount);
        }

        internal void UpdateEntryTransition(bool ready, bool lobbyIsActive, int frame)
        {
            if (entryComplete) return;
            if (!lobbyIsActive)
            {
                readyFrame = -1;
                entryCover.SetOpacity(0f);
                return;
            }
            if (!ready)
            {
                readyFrame = -1;
                return;
            }
            if (readyFrame < 0) readyFrame = frame;
            // KCC Render, camera LateUpdate and Cinemachine must see the placed
            // target before revealing it. Lost readiness restarts this wait.
            if (frame - readyFrame < 2) return;
            entryComplete = true;
            entryCover.SetOpacity(0f);
            Debug.Log(
                $"[SceneTiming] Lobby local player ready, " +
                $"elapsedSinceBinderStart={Time.realtimeSinceStartupAsDouble - startedAt:F3}s.");
        }

        public void Dispose() => entryCover.SetOpacity(0f);

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

                cameraRig.SetFollowTarget(avatars[i].transform,
                    boundRig == cameraRig && !ReferenceEquals(boundAvatar, null));
                boundAvatar = avatars[i];
                boundRig = cameraRig;
                return;
            }
        }
    }

    internal sealed class LobbyChatBubbleBinder : IStartable, IDisposable
    {
        private readonly RoomBrowserSystem room;
        private readonly LobbyChatBubbleView bubbles;
        private IDisposable subscription;

        public LobbyChatBubbleBinder(
            RoomBrowserSystem room,
            LobbyChatBubbleView bubbles)
        {
            this.room = room ?? throw new ArgumentNullException(nameof(room));
            this.bubbles = bubbles ?? throw new ArgumentNullException(nameof(bubbles));
        }

        public void Start()
        {
            subscription = room.Participants.Subscribe(_ => Rebind());
        }

        public void Dispose()
        {
            subscription?.Dispose();
        }

        private void Rebind()
        {
            bubbles.ClearBindings();

            var avatars = UnityEngine.Object.FindObjectsByType<PlayerAvatar>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            Array.Sort(avatars, (left, right) => left.Seat.CompareTo(right.Seat));

            var seated = room.Participants.CurrentValue;

            for (var i = 0; i < avatars.Length; i++)
            {
                var avatar = avatars[i];
                var playerId = PlayerRegistry.IdOf(avatar.Owner);
                var head = avatar.transform.Find("Visual") ?? avatar.transform;
                bubbles.BindPlayer(playerId, head, NicknameOf(seated, playerId));
            }
        }

        /// <summary>
        /// Empty rather than the id when the name has not replicated yet: a
        /// nameplate showing a raw id reads as a bug, so the plate stays hidden
        /// until the next rebind brings a real name.
        /// </summary>
        private static string NicknameOf(
            IReadOnlyList<RoomParticipant> seated, string playerId)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                return string.Empty;
            }

            for (var index = 0; index < seated.Count; index++)
            {
                if (string.Equals(seated[index].PlayerId, playerId, StringComparison.Ordinal))
                {
                    return seated[index].Nickname;
                }
            }

            return string.Empty;
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
                    motor.AttackSequence);
            }
        }
    }
}
