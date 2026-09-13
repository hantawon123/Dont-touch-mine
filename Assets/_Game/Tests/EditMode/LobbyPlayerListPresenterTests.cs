using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Client.Lobby;
using Game.Core.Backend;
using Game.Core.Home;
using Game.Core.Lobby;
using Game.Core.Ports;
using NUnit.Framework;
using R3;

namespace Game.Tests.EditMode
{
    public sealed class LobbyPlayerListPresenterTests
    {
        [Test]
        public void Start_PushesCurrentParticipantsToView()
        {
            var list = new LobbyParticipantList(new[]
            {
                new LobbyParticipant("1", "방장", true),
                new LobbyParticipant("2", "게스트", false),
            });
            var host = CreateHostSession(true);
            var view = new FakePlayerListView();
            var count = new FakeCountView();
            using var presenter = new LobbyPlayerListPresenter(
                list,
                host,
                new FriendListSystem(),
                new FakeInviteGateway(),
                new FakeReportGateway(),
                view,
                count,
                new FakeConfirmView());

            presenter.Start();

            Assert.That(view.Participants.Count, Is.EqualTo(2));
            Assert.That(view.LocalIsHost, Is.True);
            Assert.That(view.UpdateCount, Is.EqualTo(1));
            Assert.That(count.Current, Is.EqualTo(2));
            Assert.That(count.Max, Is.EqualTo(6));
        }

        [Test]
        public void Start_PushesFriendsFromTheSharedStore()
        {
            var list = new LobbyParticipantList(Array.Empty<LobbyParticipant>());
            var host = CreateHostSession(true);
            var friends = new FriendListSystem();
            friends.ReplaceFriends(new[]
            {
                new FriendSummary("f-1", "온라인친구", FriendPresence.Online),
                new FriendSummary("f-2", "오프라인친구", FriendPresence.Offline),
            });
            var view = new FakePlayerListView();
            using var presenter = new LobbyPlayerListPresenter(
                list,
                host,
                friends,
                new FakeInviteGateway(),
                new FakeReportGateway(),
                view,
                new FakeCountView(),
                new FakeConfirmView());

            presenter.Start();

            Assert.That(view.Friends.Count, Is.EqualTo(1));
            Assert.That(view.Friends[0].Nickname, Is.EqualTo("온라인친구"));

            friends.ReplaceFriends(new[]
            {
                new FriendSummary("f-3", "새친구", FriendPresence.InLobby),
            });
            Assert.That(view.Friends.Count, Is.EqualTo(1));
            Assert.That(view.Friends[0].Nickname, Is.EqualTo("새친구"));
        }

        [Test]
        public void Start_HidesFriendsWhoAreAlreadyInTheRoom()
        {
            var list = new LobbyParticipantList(new[]
            {
                new LobbyParticipant("host-1", "방장", true),
                new LobbyParticipant("f-1", "이미참가", false),
            });
            var friends = new FriendListSystem();
            friends.ReplaceFriends(new[]
            {
                new FriendSummary("f-1", "이미참가", FriendPresence.InLobby),
                new FriendSummary("f-2", "아직안옴", FriendPresence.Online),
            });
            var view = new FakePlayerListView();
            using var presenter = new LobbyPlayerListPresenter(
                list,
                CreateHostSession(true),
                friends,
                new FakeInviteGateway(),
                new FakeReportGateway(),
                view,
                new FakeCountView(),
                new FakeConfirmView());

            presenter.Start();

            Assert.That(PlayerIdsOf(view.Friends), Is.EqualTo(new[] { "f-2" }));

            list.Replace(new[] { new LobbyParticipant("host-1", "방장", true) });
            Assert.That(PlayerIdsOf(view.Friends), Is.EquivalentTo(new[] { "f-1", "f-2" }));

            list.Replace(new[]
            {
                new LobbyParticipant("host-1", "방장", true),
                new LobbyParticipant("f-2", "아직안옴", false),
            });
            Assert.That(PlayerIdsOf(view.Friends), Is.EqualTo(new[] { "f-1" }));
        }

        [Test]
        public void Start_HidesFriendsWhoseAccountIsAlreadyInTheRoom()
        {
            var list = new LobbyParticipantList(new[]
            {
                new LobbyParticipant("seat-7", "이미참가", false, "account-1"),
            });
            var friends = new FriendListSystem();
            friends.ReplaceFriends(new[]
            {
                new FriendSummary("account-1", "이미참가", FriendPresence.Online),
                new FriendSummary("account-2", "아직안옴", FriendPresence.Online),
            });
            var view = new FakePlayerListView();
            using var presenter = new LobbyPlayerListPresenter(
                list,
                CreateHostSession(true),
                friends,
                new FakeInviteGateway(),
                new FakeReportGateway(),
                view,
                new FakeCountView(),
                new FakeConfirmView());

            presenter.Start();

            Assert.That(PlayerIdsOf(view.Friends), Is.EqualTo(new[] { "account-2" }));
        }

        [Test]
        public void KickConfirm_RequestsKickOnHostSession()
        {
            var list = new LobbyParticipantList(new[]
            {
                new LobbyParticipant("host-1", "방장", true),
                new LobbyParticipant("player-2", "게스트", false),
            });
            var host = CreateHostSession(true);
            var kicked = new List<string>();
            host.KickRequested += id => kicked.Add(id);
            var view = new FakePlayerListView();
            var kickConfirm = new FakeConfirmView();
            using var presenter = new LobbyPlayerListPresenter(
                list,
                host,
                new FriendListSystem(),
                new FakeInviteGateway(),
                new FakeReportGateway(),
                view,
                new FakeCountView(),
                kickConfirm);

            presenter.Start();
            view.RaiseKick("player-2", "게스트");

            Assert.That(kickConfirm.Message, Is.EqualTo(KickConfirmView.FormatTitle("게스트")));
            Assert.That(kickConfirm.ChooseReason, Is.False);

            kickConfirm.RaiseConfirm();

            Assert.That(kicked, Is.EqualTo(new[] { "player-2" }));
            Assert.That(kickConfirm.IsVisible, Is.False);
        }

        [Test]
        public void InviteClicked_SendsTheFriendTheCurrentRoomCode()
        {
            var list = new LobbyParticipantList(Array.Empty<LobbyParticipant>());
            var host = CreateHostSession(true);
            var invites = new FakeInviteGateway();
            var view = new FakePlayerListView();
            using var presenter = new LobbyPlayerListPresenter(
                list,
                host,
                new FriendListSystem(),
                invites,
                new FakeReportGateway(),
                view,
                new FakeCountView(),
                new FakeConfirmView());

            presenter.Start();
            view.RaiseInvite("friend-9", "친구닉");

            Assert.That(invites.Sent, Is.EqualTo(new[] { ("friend-9", "CODE") }));
        }

        [Test]
        public void ReportClicked_AsksThenSendsTheReport()
        {
            var list = new LobbyParticipantList(new[]
            {
                new LobbyParticipant("host-1", "방장", true),
                new LobbyParticipant("player-2", "게스트", false),
            });
            var reports = new FakeReportGateway();
            var view = new FakePlayerListView();
            var confirm = new FakeConfirmView();
            using var presenter = new LobbyPlayerListPresenter(
                list,
                CreateHostSession(true),
                new FriendListSystem(),
                new FakeInviteGateway(),
                reports,
                view,
                new FakeCountView(),
                confirm);

            presenter.Start();
            view.RaiseReport("player-2", "게스트");

            Assert.That(confirm.Message, Is.EqualTo(LobbyPlayerListView.FormatReportTitle("게스트")));
            Assert.That(confirm.ConfirmLabel, Is.EqualTo(LobbyPlayerListView.ReportConfirmLabel));
            Assert.That(confirm.ChooseReason, Is.True);
            Assert.That(reports.Sent, Is.Empty);

            confirm.SelectedReason = ReportReason.Cheating;
            confirm.Note = "채팅으로 욕설을 했습니다";
            confirm.RaiseConfirm();

            Assert.That(
                reports.Sent,
                Is.EqualTo(new[] { ("player-2", ReportReason.Cheating, "채팅으로 욕설을 했습니다") }));
            Assert.That(confirm.IsVisible, Is.False);
        }

        [Test]
        public void Participant_RejectsEmptyValues()
        {
            Assert.That(
                () => new LobbyParticipant(" ", "이름", false),
                Throws.ArgumentException);
            Assert.That(
                () => new LobbyParticipant("id", " ", false),
                Throws.ArgumentException);
        }

        private static string[] PlayerIdsOf(IReadOnlyList<FriendSummary> friends)
        {
            var ids = new string[friends.Count];
            for (var index = 0; index < friends.Count; index++)
            {
                ids[index] = friends[index].PlayerId;
            }

            return ids;
        }

        private static FakeHostSession CreateHostSession(bool isHost)
        {
            return new FakeHostSession(
                "host-1",
                isHost,
                new PlaySettingsDraft("방", "CODE", false, string.Empty, 6, 5, "map"));
        }

        private sealed class FakeHostSession : ILobbyHostSession, IDisposable
        {
            private readonly ReactiveProperty<bool> isLocalHost;
            private readonly ReactiveProperty<PlaySettingsDraft> settings;

            public FakeHostSession(
                string localPlayerId,
                bool localIsHost,
                PlaySettingsDraft initialSettings)
            {
                LocalPlayerId = localPlayerId;
                isLocalHost = new ReactiveProperty<bool>(localIsHost);
                settings = new ReactiveProperty<PlaySettingsDraft>(initialSettings);
            }

            public string LocalPlayerId { get; }
            public ReadOnlyReactiveProperty<bool> IsLocalHost => isLocalHost;
            public ReadOnlyReactiveProperty<PlaySettingsDraft> Settings => settings;

            public event Action StartRequested;
            public event Action<string> KickRequested;
            public event Action<string> HostTransferRequested;
            public event Action<PlaySettingsDraft> SettingsApplyRequested;

            public void SetLocalHost(bool value) => isLocalHost.Value = value;
            public void ReplaceSettings(PlaySettingsDraft value) => settings.Value = value;
            public void RequestStart() => StartRequested?.Invoke();
            public void RequestKick(string playerId) => KickRequested?.Invoke(playerId);
            public void RequestHostTransfer(string playerId) => HostTransferRequested?.Invoke(playerId);
            public void RequestApplySettings(PlaySettingsDraft value) =>
                SettingsApplyRequested?.Invoke(value);

            public void Dispose()
            {
                isLocalHost.Dispose();
                settings.Dispose();
            }
        }

        private sealed class FakePlayerListView : ILobbyPlayerListView
        {
            public IReadOnlyList<LobbyParticipant> Participants { get; private set; }
            public IReadOnlyList<FriendSummary> Friends { get; private set; } =
                Array.Empty<FriendSummary>();
            public bool LocalIsHost { get; private set; }
            public int UpdateCount { get; private set; }

            public event Action<string, string> KickClicked;
            public event Action<string, string> InviteClicked;
            public event Action<string, string> ReportClicked;

            public void SetParticipants(
                IReadOnlyList<LobbyParticipant> participants,
                bool localIsHost,
                string localPlayerId)
            {
                Participants = participants;
                LocalIsHost = localIsHost;
                UpdateCount++;
            }

            public void SetFriends(IReadOnlyList<FriendSummary> friends)
            {
                Friends = friends;
            }

            public void RaiseKick(string id, string name) => KickClicked?.Invoke(id, name);

            public void RaiseInvite(string id, string name) => InviteClicked?.Invoke(id, name);

            public void RaiseReport(string id, string name) => ReportClicked?.Invoke(id, name);
        }

        private sealed class FakeInviteGateway : IInviteGateway
        {
            public List<(string PlayerId, string RoomCode)> Sent { get; } = new();

            public UniTask<BackendResult> SendAsync(
                string playerId, string roomCode, CancellationToken cancellation)
            {
                Sent.Add((playerId, roomCode));
                return UniTask.FromResult(BackendResult.Success());
            }

            public UniTask<BackendResult<IReadOnlyList<RoomInvitation>>> ListAsync(
                CancellationToken cancellation)
            {
                return UniTask.FromResult(
                    BackendResult<IReadOnlyList<RoomInvitation>>.Success(
                        Array.Empty<RoomInvitation>()));
            }

            public UniTask<BackendResult> DeclineAsync(
                string playerId, CancellationToken cancellation)
            {
                return UniTask.FromResult(BackendResult.Success());
            }
        }

        private sealed class FakeReportGateway : IReportGateway
        {
            public List<(string PlayerId, ReportReason Reason, string Note)> Sent { get; } = new();

            public UniTask<BackendResult> ReportAsync(
                string playerId,
                ReportReason reason,
                string note,
                CancellationToken cancellation)
            {
                Sent.Add((playerId, reason, note));
                return UniTask.FromResult(BackendResult.Success());
            }
        }

        private sealed class FakeCountView : ILobbyPlayerCountView
        {
            public int Current { get; private set; }
            public int Max { get; private set; }

            public void SetCount(int current, int max)
            {
                Current = current;
                Max = max;
            }
        }

        private sealed class FakeConfirmView : ILobbyConfirmView
        {
            public bool IsVisible { get; private set; }
            public bool ChooseReason { get; private set; }
            public string Message { get; private set; }
            public string ConfirmLabel { get; private set; }
            public ReportReason SelectedReason { get; set; } = ReportReason.Other;
            public string Note { get; set; } = string.Empty;
            public event Action Confirmed;
            public event Action Cancelled;

            public void Show(string message, string confirmLabel) =>
                Show(message, confirmLabel, false);

            public void Show(string message, string confirmLabel, bool chooseReason)
            {
                Message = message;
                ConfirmLabel = confirmLabel;
                ChooseReason = chooseReason;
                SelectedReason = chooseReason ? ReportReason.Abuse : ReportReason.Other;
                IsVisible = true;
            }

            public void Hide() => IsVisible = false;

            public void RaiseConfirm() => Confirmed?.Invoke();
        }
    }
}
