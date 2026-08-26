using System;
using System.Collections.Generic;
using Game.Client.Home;
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

        [SerializeField]
        private LobbyChatView chatView;

        [SerializeField]
        private LobbyChatBubbleView chatBubbleView;

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
            builder.RegisterInstance(CreateSampleParticipantList()).As<ILobbyParticipantList>();
            builder.RegisterInstance(CreateSampleHostSession()).As<ILobbyHostSession>();
            builder.RegisterInstance(CreateSampleChatLog()).As<ILobbyChatLog>();
            builder.RegisterEntryPoint<KeyGuidePresenter>();
            builder.RegisterEntryPoint<LobbyPlayerListPresenter>();
            builder.RegisterEntryPoint<LobbyHostChromePresenter>();
            builder.RegisterEntryPoint<PlaySettingsPresenter>();
            builder.RegisterEntryPoint<LobbyChatPresenter>();
            builder.RegisterEntryPoint<LobbyExitPresenter>();
        }

        private static LobbyParticipantList CreateSampleParticipantList()
        {
            return new LobbyParticipantList(new[]
            {
                new LobbyParticipant("host-1", "김말갈", true),
                new LobbyParticipant("player-2", "김명행", false),
                new LobbyParticipant("player-3", "보리우유", false),
                new LobbyParticipant("player-4", "초롱초롱한닉네임테스트용", false),
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
