using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Backend;
using Game.Core.Home;
using Game.Core.Lobby;
using Game.Core.Ports;
using R3;
using UnityEngine;
using VContainer.Unity;

namespace Game.Client.Lobby
{
    public sealed class LobbyPlayerListPresenter : IStartable, IDisposable
    {
        private readonly ILobbyParticipantList participantList;
        private readonly ILobbyHostSession hostSession;
        private readonly FriendListSystem friends;
        private readonly IInviteGateway invites;
        private readonly IReportGateway reports;
        private readonly ILobbyPlayerListView view;
        private readonly ILobbyPlayerCountView countView;
        private readonly ILobbyConfirmView confirmView;

        private readonly CancellationTokenSource lifetime = new();
        private Game.Core.Settings.InterfacePresentation presentation;

        [VContainer.Inject]
        public void BindPresentation(Game.Core.Settings.InterfacePresentation value) =>
            presentation = value;

        [VContainer.Inject]
        public void BindVoice(IVoiceControl value) => voice = value;

        private IVoiceControl voice;
        private IDisposable refreshSubscription;
        private IDisposable muteSubscription;
        private IDisposable talkSubscription;

        /// <summary>
        /// The last thing the room said about itself, so that a name arriving
        /// on its own can redraw the rows without waiting for the room to
        /// change too.
        /// </summary>
        private (IReadOnlyList<LobbyParticipant> Participants, bool IsLocalHost, PlaySettingsDraft Settings)? latest;
        private string pendingPlayerId;
        private PendingConfirm pending;

        public LobbyPlayerListPresenter(
            ILobbyParticipantList participantList,
            ILobbyHostSession hostSession,
            FriendListSystem friends,
            IInviteGateway invites,
            IReportGateway reports,
            ILobbyPlayerListView view,
            ILobbyPlayerCountView countView,
            ILobbyConfirmView confirmView)
        {
            this.participantList = participantList
                ?? throw new ArgumentNullException(nameof(participantList));
            this.hostSession = hostSession ?? throw new ArgumentNullException(nameof(hostSession));
            this.friends = friends ?? throw new ArgumentNullException(nameof(friends));
            this.invites = invites ?? throw new ArgumentNullException(nameof(invites));
            this.reports = reports ?? throw new ArgumentNullException(nameof(reports));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.countView = countView ?? throw new ArgumentNullException(nameof(countView));
            this.confirmView = confirmView
                ?? throw new ArgumentNullException(nameof(confirmView));
        }

        public void Start()
        {
            confirmView.Hide();

            if (presentation != null) presentation.Changed += OnPresentationChanged;
            view.KickClicked += OnKickClicked;
            view.InviteClicked += OnInviteClicked;
            view.ReportClicked += OnReportClicked;
            confirmView.Confirmed += ConfirmPending;
            confirmView.Cancelled += CancelPending;
            friends.FriendsChanged += BindFriends;
            if (voice != null)
            {
                muteSubscription = voice.IsMuted.Subscribe(_ => Draw());
                talkSubscription = voice.IsTransmitting.Subscribe(_ => Draw());
            }

            refreshSubscription = Observable.CombineLatest(
                    participantList.Participants,
                    hostSession.IsLocalHost,
                    hostSession.Settings,
                    (participants, isLocalHost, settings) =>
                        (Participants: participants, IsLocalHost: isLocalHost, Settings: settings))
                .Subscribe(state =>
                {
                    latest = state;
                    Draw();
                });
        }

        /// <summary>
        /// What a name change means here: the rows say who everybody is, so
        /// they have to be written again.
        /// </summary>
        /// <remarks>
        /// A player's name arrives a moment after they do — the owner has to
        /// say whether it is their own or a pseudonym, and until they have,
        /// there is no name to show. Without this the list keeps the blank it
        /// was drawn with, which is what a joining player used to see for the
        /// whole time they were in the room.
        /// <para>
        /// This event is not only a name change. Friend presence is polled
        /// every few seconds while the roster is open, and that refresh is
        /// broadcast as the same <c>Changed</c>. Closing a kick confirm here
        /// made the panel vanish with nobody touching it.
        /// </para>
        /// </remarks>
        private void OnPresentationChanged()
        {
            Draw();
        }

        private void Draw()
        {
            if (!latest.HasValue)
            {
                return;
            }

            var state = latest.Value;
            var people = WithLocalVoice(state.Participants ?? Array.Empty<LobbyParticipant>());
            var namesReady = presentation == null || presentation.InitialVisibilityReady;
            view.SetParticipants(
                namesReady ? people : Array.Empty<LobbyParticipant>(),
                state.IsLocalHost,
                hostSession.LocalPlayerId,
                namesReady);
            countView.SetCount(people.Count, state.Settings.MaxPlayers);
            BindFriends();
        }

        public void Dispose()
        {
            if (presentation != null) presentation.Changed -= OnPresentationChanged;
            view.KickClicked -= OnKickClicked;
            view.InviteClicked -= OnInviteClicked;
            view.ReportClicked -= OnReportClicked;
            lifetime.Cancel();
            lifetime.Dispose();
            confirmView.Confirmed -= ConfirmPending;
            confirmView.Cancelled -= CancelPending;
            friends.FriendsChanged -= BindFriends;
            muteSubscription?.Dispose();
            talkSubscription?.Dispose();
            refreshSubscription?.Dispose();
        }

        /// <summary>
        /// The local microphone is the source of truth for this machine. The
        /// roster's copy can lag a frame, or never land if the avatar has not
        /// published yet, and then the owner would not see their own mute or
        /// talk icon.
        /// </summary>
        private IReadOnlyList<LobbyParticipant> WithLocalVoice(
            IReadOnlyList<LobbyParticipant> people)
        {
            var localId = hostSession.LocalPlayerId;
            if (voice == null || string.IsNullOrEmpty(localId) || people.Count == 0)
            {
                return people;
            }

            var localMuted = voice.IsMuted.CurrentValue;
            var localTalking = !localMuted && voice.IsTransmitting.CurrentValue;
            for (var index = 0; index < people.Count; index++)
            {
                var person = people[index];
                if (!string.Equals(person.Id, localId, StringComparison.Ordinal)
                    || (person.IsMuted == localMuted && person.IsTalking == localTalking))
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
                    localTalking);
                return copy;
            }

            return people;
        }

        private void BindFriends()
        {
            var people = participantList.Participants.CurrentValue
                ?? Array.Empty<LobbyParticipant>();
            var inRoom = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < people.Count; index++)
            {
                var person = people[index];
                inRoom.Add(person.Id);
                if (!string.IsNullOrEmpty(person.UserId))
                {
                    inRoom.Add(person.UserId);
                }
            }

            var online = friends.OnlineFriends;
            var invitable = new List<FriendSummary>(online.Count);
            AppendInvitable(invitable, online, inRoom);
            view.SetFriends(invitable);
        }

        private static void AppendInvitable(
            List<FriendSummary> destination,
            IReadOnlyList<FriendSummary> source,
            HashSet<string> inRoom)
        {
            for (var index = 0; index < source.Count; index++)
            {
                var friend = source[index];
                if (inRoom.Contains(friend.PlayerId))
                {
                    continue;
                }

                destination.Add(friend);
            }
        }

        private void OnInviteClicked(string playerId, string _)
        {
            var roomCode = hostSession.Settings.CurrentValue.RoomCode;
            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(roomCode))
            {
                return;
            }

            invites.SendAsync(playerId, roomCode, lifetime.Token).Forget();
        }

        private void OnKickClicked(string playerId, string displayName)
        {
            if (!hostSession.IsLocalHost.CurrentValue)
            {
                return;
            }

            pending = PendingConfirm.Kick;
            pendingPlayerId = playerId;
            confirmView.Show(
                KickConfirmView.FormatTitle(PresentedName(playerId, displayName)),
                KickConfirmView.ConfirmLabel);
        }

        /// <summary>
        /// Kick confirm must say the same thing the list does. The roster still
        /// carries the account nickname, so an anonymous player would otherwise
        /// be named for real on the way out.
        /// </summary>
        private string PresentedName(string playerId, string fallback)
        {
            if (presentation == null || string.IsNullOrEmpty(playerId))
            {
                return fallback;
            }

            var rosterName = fallback;
            if (latest.HasValue)
            {
                var people = latest.Value.Participants;
                if (people != null)
                {
                    for (var index = 0; index < people.Count; index++)
                    {
                        var person = people[index];
                        if (string.Equals(person.Id, playerId, StringComparison.Ordinal))
                        {
                            rosterName = person.DisplayName;
                            break;
                        }
                    }
                }
            }

            var presented = presentation.Name(playerId, rosterName);
            return string.IsNullOrEmpty(presented) ? fallback : presented;
        }

        /// <param name="userId">
        /// The backend account of the reported player, not the Photon player id
        /// the kick path uses. The two were confused once and every report was
        /// answered 404 (S15P21D205-926).
        /// </param>
        private void OnReportClicked(string userId, string displayName)
        {
            // No comparison against LocalPlayerId here. That is a Photon player
            // id and this is an account id, so the check could never match — and
            // a guard that cannot fire reads like protection that is not there.
            // The view already leaves the report off the local player's own row.
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            pending = PendingConfirm.Report;
            pendingPlayerId = userId;
            confirmView.Show(
                LobbyPlayerListView.FormatReportTitle(displayName),
                LobbyPlayerListView.ReportConfirmLabel,
                chooseReason: true);
        }

        private void ConfirmPending()
        {
            if (string.IsNullOrWhiteSpace(pendingPlayerId))
            {
                CancelPending();
                return;
            }

            if (pending == PendingConfirm.Kick)
            {
                hostSession.RequestKick(pendingPlayerId);
            }
            else if (pending == PendingConfirm.Report)
            {
                ReportAsync(
                    pendingPlayerId,
                    confirmView.SelectedReason,
                    confirmView.Note,
                    lifetime.Token).Forget();
            }

            CancelPending();
        }

        /// <summary>
        /// Sends the report and says so when it does not land.
        /// </summary>
        /// <remarks>
        /// The result used to be dropped with <c>Forget</c> on the call itself.
        /// That is how every report could be answered 404 for as long as it was
        /// without anyone noticing: the id was wrong and the failure had nowhere
        /// to go (S15P21D205-926).
        /// <para>
        /// A log line, not a message on screen. Telling the player is a separate
        /// piece of work (S15P21D205-924); this is the part that would have made
        /// the bug visible to whoever was testing.
        /// </para>
        /// </remarks>
        private async UniTaskVoid ReportAsync(
            string userId, ReportReason reason, string note, CancellationToken cancellation)
        {
            var result = await reports.ReportAsync(userId, reason, note, cancellation);

            if (result.Ok || result.Failure == BackendFailure.Cancelled)
            {
                return;
            }

            Debug.LogWarning(
                $"[Report] Reporting {userId} did not land: {result.Failure}.");
        }

        private void CancelPending()
        {
            pending = PendingConfirm.None;
            pendingPlayerId = null;
            confirmView.Hide();
        }

        private enum PendingConfirm
        {
            None,
            Kick,
            Report
        }
    }
}
