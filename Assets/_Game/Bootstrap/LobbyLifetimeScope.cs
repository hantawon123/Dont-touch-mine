using System;
using System.Collections.Generic;
using Game.Client.Cameras;
using Game.Client.Home;
using Game.Client.Lobby;
using Game.Core.Lobby;
using Game.Network.Players;
using Game.Network.Session;
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
            builder.RegisterComponent(chatBubbleView).As<ILobbyChatBubbleView>();
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
            builder.RegisterInstance(CreateSampleChatLog()).As<ILobbyChatLog>();
            builder.RegisterEntryPoint<KeyGuidePresenter>();
            builder.RegisterEntryPoint<LobbyPlayerListPresenter>();
            builder.RegisterEntryPoint<LobbyHostChromePresenter>();
            builder.RegisterEntryPoint<PlaySettingsPresenter>();
            builder.RegisterEntryPoint<LobbyChatPresenter>();
            // AsSelf so the bridge below can take the leave request off it.
            builder.RegisterEntryPoint<LobbyExitPresenter>().AsSelf();
            builder.RegisterEntryPoint<NetworkLobbyExitBridge>();

            // The character is created while the room screen is still open and
            // can fall before this scene loads. Reset every seat after the
            // lobby's ground exists; the authority's move replicates to guests.
            builder.RegisterBuildCallback(container =>
            {
                container.Resolve<NetworkRunnerService>()
                    .RepositionPlayers(CreateLobbySpawnPoses());

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

        private static IReadOnlyList<Pose> CreateLobbySpawnPoses() => new[]
        {
            new Pose(new Vector3(0f, 0f, 3f), Quaternion.Euler(0f, 180f, 0f)),
            new Pose(new Vector3(-2f, 0f, 3f), Quaternion.Euler(0f, 180f, 0f)),
            new Pose(new Vector3(2f, 0f, 3f), Quaternion.Euler(0f, 180f, 0f)),
            new Pose(new Vector3(-4f, 0f, 3f), Quaternion.Euler(0f, 180f, 0f)),
            new Pose(new Vector3(4f, 0f, 3f), Quaternion.Euler(0f, 180f, 0f)),
            new Pose(new Vector3(0f, 0f, 5f), Quaternion.Euler(0f, 180f, 0f)),
        };

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
                6,
                5,
                "market-01");
        }

        private static LobbyChatLog CreateSampleChatLog()
        {
            return new LobbyChatLog(
                "host-1",
                "김말갈",
                new[]
                {
                    new LobbyChatMessage("player-2", "김명행", "안녕하세요!"),
                    new LobbyChatMessage("player-3", "보리우유", "오늘 한 판 해요"),
                    new LobbyChatMessage("player-4", "초롱초롱한닉네임테스트용", "닉네임 길어도 본문 보여요"),
                    new LobbyChatMessage("host-1", "김말갈", "곧 시작합니다"),
                });
        }
    }
}
