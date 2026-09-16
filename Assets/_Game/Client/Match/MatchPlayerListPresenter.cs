using System;
using System.Collections.Generic;
using Game.Client.Lobby;
using Game.Core.Lobby;
using Game.Core.Ports;
using R3;
using VContainer.Unity;

namespace Game.Client.Match
{
    /// <summary>
    /// Keeps the in-match 2-key roster in step with the room. Friends, kick
    /// and invite stay off this screen; it is only who is playing.
    /// </summary>
    public sealed class MatchPlayerListPresenter : IStartable, IDisposable
    {
        private readonly ILobbyParticipantList participantList;
        private readonly RoomBrowserSystem room;
        private readonly ILobbyPlayerListView view;
        private IDisposable refreshSubscription;
        private IDisposable muteSubscription;
        private IDisposable talkSubscription;
        private IDisposable listenSubscription;
        private readonly IVoiceControl voice;
        private Game.Core.Settings.InterfacePresentation presentation;
        private IReadOnlyList<LobbyParticipant> latest = Array.Empty<LobbyParticipant>();

        public MatchPlayerListPresenter(
            ILobbyParticipantList participantList,
            RoomBrowserSystem room,
            ILobbyPlayerListView view,
            IVoiceControl voice = null)
        {
            this.participantList = participantList
                ?? throw new ArgumentNullException(nameof(participantList));
            this.room = room ?? throw new ArgumentNullException(nameof(room));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.voice = voice;
        }

        [VContainer.Inject]
        public void BindPresentation(Game.Core.Settings.InterfacePresentation value) =>
            presentation = value;

        public void Start()
        {
            if (presentation != null)
            {
                presentation.Changed += Draw;
            }

            if (voice != null)
            {
                muteSubscription = voice.IsMuted.Subscribe(_ => Draw());
                talkSubscription = voice.IsTransmitting.Subscribe(_ => Draw());
                listenSubscription = voice.IsListening.Subscribe(_ => Draw());
            }

            refreshSubscription = Observable.CombineLatest(
                    participantList.Participants,
                    room.LocalPlayerId,
                    (participants, _) => participants)
                .Subscribe(participants =>
                {
                    latest = participants ?? Array.Empty<LobbyParticipant>();
                    Draw();
                });
        }

        public void Dispose()
        {
            if (presentation != null)
            {
                presentation.Changed -= Draw;
            }

            muteSubscription?.Dispose();
            talkSubscription?.Dispose();
            listenSubscription?.Dispose();
            refreshSubscription?.Dispose();
        }

        private void Draw()
        {
            var namesReady = presentation == null || presentation.InitialVisibilityReady;
            view.SetParticipants(
                namesReady ? WithLocalVoice(latest) : Array.Empty<LobbyParticipant>(),
                localIsHost: false,
                room.LocalPlayerId.CurrentValue,
                namesReady);
        }

        private IReadOnlyList<LobbyParticipant> WithLocalVoice(
            IReadOnlyList<LobbyParticipant> people)
        {
            var localId = room.LocalPlayerId.CurrentValue;
            if (voice == null || string.IsNullOrEmpty(localId) || people.Count == 0)
            {
                return people;
            }

            var localMuted = voice.IsMuted.CurrentValue;
            var localListening = voice.IsListening.CurrentValue;
            var localTalking = !localMuted && voice.IsTransmitting.CurrentValue;
            for (var index = 0; index < people.Count; index++)
            {
                var person = people[index];
                if (!string.Equals(person.Id, localId, StringComparison.Ordinal)
                    || (person.IsMuted == localMuted
                        && person.IsTalking == localTalking
                        && person.IsListening == localListening))
                {
                    continue;
                }

                var copy = new LobbyParticipant[people.Count];
                for (var write = 0; write < people.Count; write++)
                {
                    copy[write] = people[write];
                }

                copy[index] = new LobbyParticipant(
                    person.Id,
                    person.DisplayName,
                    person.IsHost,
                    person.UserId,
                    localMuted,
                    localTalking,
                    localListening);
                return copy;
            }

            return people;
        }
    }
}
