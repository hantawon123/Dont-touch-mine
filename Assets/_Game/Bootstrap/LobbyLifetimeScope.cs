using System;
using System.Collections.Generic;
using Game.Client.Lobby;
using Game.Core.Lobby;
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

            builder.RegisterComponent(hudView);
            builder.RegisterComponent(keyGuideView).As<IKeyGuideView>();
            builder.RegisterComponent(playerListView).As<ILobbyPlayerListView>();
            builder.RegisterComponent(playSettingsView).As<IPlaySettingsView>();
            builder.RegisterComponent(kickConfirmView).As<IKickConfirmView>();
            builder.RegisterComponent(transferConfirmView).As<IHostTransferConfirmView>();
            builder.RegisterInstance<IReadOnlyList<ControlKeyBinding>>(ControlKeyGuide.Bindings);
            builder.RegisterInstance(CreateSampleParticipantList()).As<ILobbyParticipantList>();
            builder.RegisterInstance(CreateSampleHostSession()).As<ILobbyHostSession>();
            builder.RegisterEntryPoint<KeyGuidePresenter>();
            builder.RegisterEntryPoint<LobbyPlayerListPresenter>();
            builder.RegisterEntryPoint<LobbyHostChromePresenter>();
            builder.RegisterEntryPoint<PlaySettingsPresenter>();
        }

        private static LobbyParticipantList CreateSampleParticipantList()
        {
            return new LobbyParticipantList(new[]
            {
                new LobbyParticipant("host-1", "김말갈", true),
                new LobbyParticipant("player-2", "김명행", false),
                new LobbyParticipant("player-3", "보리우유", false),
            });
        }

        private static LobbyHostSession CreateSampleHostSession()
        {
            var settings = new PlaySettingsDraft(
                "초보방",
                "K7M2QF",
                true,
                "1234",
                6,
                5,
                "market-01");
            return new LobbyHostSession("host-1", true, settings);
        }
    }
}
