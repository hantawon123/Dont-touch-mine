using System;
using System.Collections.Generic;
using Game.Client.Cameras;
using Game.Client.Home;
using Game.Client.Lobby;
using Game.Client.Players;
using Game.Core.Home;
using Game.Core.Lobby;
using Game.Core.Maps;
using Game.Core.Rooms;
using Game.Network.Players;
using Game.Network.Session;
using R3;
using UnityEngine;
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
        private PlayerCameraController cameraRigPrefab;

        protected override void Configure(IContainerBuilder builder)
        {
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
            builder.RegisterComponent(playerListView).As<ILobbyPlayerListView>();
            builder.RegisterComponent(playSettingsView).As<IPlaySettingsView>();
            builder.RegisterComponent(kickConfirmView).As<IKickConfirmView>();
            builder.RegisterComponent(transferConfirmView).As<IHostTransferConfirmView>();
            builder.RegisterComponent(chatView).As<ILobbyChatView>();
            builder.RegisterComponent(chatBubbleView)
                .AsSelf()
                .As<ILobbyChatBubbleView>();
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
            builder.RegisterEntryPoint<LobbyHostChromePresenter>();
            builder.RegisterEntryPoint<PlaySettingsPresenter>();
            builder.RegisterEntryPoint<LobbyChatPresenter>();
            builder.RegisterEntryPoint<LobbyChatBubbleBinder>();
            builder.RegisterEntryPoint<LobbyPlayerCameraBinder>();
            builder.RegisterEntryPoint<LobbyPlayerAnimationBinder>();
            // AsSelf so the bridge below can take the leave request off it.
            builder.RegisterEntryPoint<LobbyExitPresenter>().AsSelf();
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
                BindLocalPlayerCamera();
            });
        }

        private void BindLocalPlayerCamera()
        {
            var cameraRig = FindFirstObjectByType<PlayerCameraController>(FindObjectsInactive.Include);
            if (cameraRig == null)
            {
                cameraRig = Instantiate(cameraRigPrefab);
            }

            // The lobby is a screen the player clicks through, so the mouse
            // belongs to its UI whether or not the avatar has arrived yet. This
            // used to sit inside the search below, which meant a late avatar
            // left the cursor captured — and a captured cursor cannot press
            // Leave, because it reports from the centre of the screen and the
            // click reached the combat input instead of the button.
            cameraRig.SetCursorCaptureEnabled(false);

            // The follow target is not looked for here. On a client the avatar
            // is a replicated object that has not arrived yet at this point in
            // the scene load — measured as zero avatars present, while the host,
            // which spawns its own locally, always found one. LobbyPlayerCamera
            // Binder waits for it instead.
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
    /// the whole visit. The participant list changes as characters register
    /// themselves, which makes it the signal to look again — the same one the
    /// chat bubbles bind on.
    /// </remarks>
    internal sealed class LobbyPlayerCameraBinder : IStartable, IDisposable
    {
        private readonly RoomBrowserSystem room;
        private IDisposable subscription;
        private bool isBound;

        public LobbyPlayerCameraBinder(RoomBrowserSystem room)
        {
            this.room = room ?? throw new ArgumentNullException(nameof(room));
        }

        public void Start()
        {
            subscription = room.Participants.Subscribe(_ => TryBind());
        }

        public void Dispose()
        {
            subscription?.Dispose();
        }

        private void TryBind()
        {
            if (isBound)
            {
                return;
            }

            var cameraRig = UnityEngine.Object.FindFirstObjectByType<PlayerCameraController>(
                FindObjectsInactive.Include);
            if (cameraRig == null)
            {
                return;
            }

            // Inactive included: a character can register before Unity has
            // finished bringing its object up.
            var avatars = UnityEngine.Object.FindObjectsByType<PlayerAvatar>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (var i = 0; i < avatars.Length; i++)
            {
                if (!avatars[i].IsOwner)
                {
                    continue;
                }

                cameraRig.SetFollowTarget(avatars[i].transform);
                isBound = true;
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
        public void Tick()
        {
            var avatars = UnityEngine.Object.FindObjectsByType<PlayerAvatar>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            for (var i = 0; i < avatars.Length; i++)
            {
                var avatar = avatars[i];
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
