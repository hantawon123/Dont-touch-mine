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

            builder.RegisterComponent(hudView);
            builder.RegisterComponent(keyGuideView).As<IKeyGuideView>();
            builder.RegisterComponent(playerListView).As<ILobbyPlayerListView>();
            builder.RegisterInstance<IReadOnlyList<ControlKeyBinding>>(ControlKeyGuide.Bindings);
            builder.RegisterInstance(CreateSampleParticipantList()).As<ILobbyParticipantList>();
            builder.RegisterEntryPoint<KeyGuidePresenter>();
            builder.RegisterEntryPoint<LobbyPlayerListPresenter>();
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
    }
}
