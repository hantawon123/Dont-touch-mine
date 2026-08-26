using System;
using Game.Core.Lobby;
using R3;
using VContainer.Unity;

namespace Game.Client.Lobby
{
    public sealed class LobbyPlayerListPresenter : IStartable, IDisposable
    {
        private readonly ILobbyParticipantList participantList;
        private readonly ILobbyPlayerListView view;
        private IDisposable subscription;

        public LobbyPlayerListPresenter(
            ILobbyParticipantList participantList,
            ILobbyPlayerListView view)
        {
            this.participantList = participantList
                ?? throw new ArgumentNullException(nameof(participantList));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
        }

        public void Start()
        {
            subscription = participantList.Participants.Subscribe(view.SetParticipants);
        }

        public void Dispose()
        {
            subscription?.Dispose();
        }
    }
}
