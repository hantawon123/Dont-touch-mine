using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Client.Lobby;
using Game.Core.Backend;
using Game.Core.Lobby;
using Game.Core.Ports;
using R3;
using UnityEngine;
using VContainer.Unity;

namespace Game.Client.Match
{
    /// <summary>
    /// Keeps the in-match 2-key roster in step with the room. Friends, kick
    /// and invite stay off this screen; reporting is the same as the lobby.
    /// </summary>
    public sealed class MatchPlayerListPresenter : IStartable, IDisposable
    {
        private readonly ILobbyParticipantList participantList;
        private readonly RoomBrowserSystem room;
        private readonly ILobbyPlayerListView view;
        private readonly IReportGateway reports;
        private readonly IReportContext reportContext;
        private readonly ILobbyConfirmView confirmView;
        private readonly CancellationTokenSource lifetime = new();
        private IDisposable refreshSubscription;
        private IDisposable muteSubscription;
        private IDisposable talkSubscription;
        private IDisposable listenSubscription;
        private readonly IVoiceControl voice;
        private Game.Core.Settings.InterfacePresentation presentation;
        private Game.Core.Settings.UiLocale locale;
        private IReadOnlyList<LobbyParticipant> latest = Array.Empty<LobbyParticipant>();
        private string pendingUserId;

        public MatchPlayerListPresenter(
            ILobbyParticipantList participantList,
            RoomBrowserSystem room,
            ILobbyPlayerListView view,
            IReportGateway reports,
            IReportContext reportContext,
            ILobbyConfirmView confirmView,
            IVoiceControl voice = null)
        {
            this.participantList = participantList
                ?? throw new ArgumentNullException(nameof(participantList));
            this.room = room ?? throw new ArgumentNullException(nameof(room));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.reports = reports ?? throw new ArgumentNullException(nameof(reports));
            this.reportContext = reportContext
                ?? throw new ArgumentNullException(nameof(reportContext));
            this.confirmView = confirmView
                ?? throw new ArgumentNullException(nameof(confirmView));
            this.voice = voice;
        }

        [VContainer.Inject]
        public void BindPresentation(Game.Core.Settings.InterfacePresentation value) =>
            presentation = value;

        [VContainer.Inject]
        public void BindLocale(Game.Core.Settings.UiLocale value) => locale = value;

        public void Start()
        {
            confirmView.Hide();
            if (presentation != null)
            {
                presentation.Changed += Draw;
            }

            view.ReportClicked += OnReportClicked;
            confirmView.Confirmed += ConfirmPending;
            confirmView.Cancelled += CancelPending;
            if (locale != null)
            {
                locale.Changed += OnLocaleChanged;
            }

            view.ShowChrome(locale);

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

            view.ReportClicked -= OnReportClicked;
            confirmView.Confirmed -= ConfirmPending;
            confirmView.Cancelled -= CancelPending;
            if (locale != null)
            {
                locale.Changed -= OnLocaleChanged;
            }

            lifetime.Cancel();
            lifetime.Dispose();
            muteSubscription?.Dispose();
            talkSubscription?.Dispose();
            listenSubscription?.Dispose();
            refreshSubscription?.Dispose();
        }

        private void OnLocaleChanged()
        {
            view.ShowChrome(locale);
            Draw();
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

        private void OnReportClicked(string userId, string displayName)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            pendingUserId = userId;
            confirmView.Show(
                LobbyPlayerListView.FormatReportTitle(
                    displayName,
                    locale != null ? locale.LanguageCode : "ko"),
                LobbyPlayerListView.ReportConfirmLabel,
                chooseReason: true);
        }

        private void ConfirmPending()
        {
            if (string.IsNullOrWhiteSpace(pendingUserId))
            {
                CancelPending();
                return;
            }

            ReportAsync(
                pendingUserId,
                confirmView.SelectedReason,
                confirmView.Note,
                lifetime.Token).Forget();
            CancelPending();
        }

        private async UniTaskVoid ReportAsync(
            string userId, ReportReason reason, string note, CancellationToken cancellation)
        {
            var result = await reports.ReportAsync(
                userId, reason, note, reportContext.CurrentKey, cancellation);
            if (result.Ok || result.Failure == BackendFailure.Cancelled)
            {
                return;
            }

            if (result.Failure == BackendFailure.ReportAlreadySent)
            {
                Debug.Log($"[Report] {userId} was already reported in this match.");
                return;
            }

            Debug.LogWarning(
                $"[Report] Reporting {userId} did not land: {result.Failure}.");
        }

        private void CancelPending()
        {
            pendingUserId = null;
            confirmView.Hide();
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
