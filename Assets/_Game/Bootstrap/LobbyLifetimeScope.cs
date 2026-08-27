using System;
using System.Collections.Generic;
using Game.Client.Cameras;
using Game.Client.Home;
using Game.Client.Lobby;
using Game.Core.Home;
using Game.Core.Lobby;
using Game.Core.Maps;
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

            var avatars = FindObjectsByType<PlayerAvatar>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (var i = 0; i < avatars.Length; i++)
            {
                if (!avatars[i].IsOwner)
                {
                    continue;
                }

                cameraRig.SetFollowTarget(avatars[i].transform);
                cameraRig.SetCursorCaptureEnabled(false);
                return;
            }

            Debug.LogWarning("Lobby camera could not find the local PlayerAvatar.", this);
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

            for (var i = 0; i < avatars.Length; i++)
            {
                var avatar = avatars[i];
                var playerId = PlayerRegistry.IdOf(avatar.Owner);
                var head = avatar.transform.Find("Visual") ?? avatar.transform;
                bubbles.BindPlayer(playerId, head);
            }
        }
    }
}
