using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Client.Lobby;
using Game.Client.Match;
using Game.Core.Backend;
using Game.Core.Home;
using Game.Core.Lobby;
using Game.Core.Ports;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class MatchPlayerListPresenterTests
    {
        [Test]
        public void Start_PushesParticipantsWithoutHostKickOrFriends()
        {
            var list = new LobbyParticipantList(new[]
            {
                new LobbyParticipant("host-1", "방장", true),
                new LobbyParticipant("player-2", "게스트", false),
            });
            using var room = new RoomBrowserSystem();
            room.SetLocalPlayer("player-2");
            var view = new FakePlayerListView();
            using var presenter = new MatchPlayerListPresenter(
                list, room, view, new FakeReportGateway(), new FakeReportContext(), new FakeConfirmView());

            presenter.Start();

            Assert.That(view.Participants.Count, Is.EqualTo(2));
            Assert.That(view.LocalIsHost, Is.False);
            Assert.That(view.LocalPlayerId, Is.EqualTo("player-2"));
            Assert.That(view.Friends, Is.Empty);
        }

        [Test]
        public void Replace_RedrawsTheMatchRoster()
        {
            var list = new LobbyParticipantList(new[]
            {
                new LobbyParticipant("host-1", "방장", true),
            });
            using var room = new RoomBrowserSystem();
            room.SetLocalPlayer("host-1");
            var view = new FakePlayerListView();
            using var presenter = new MatchPlayerListPresenter(
                list, room, view, new FakeReportGateway(), new FakeReportContext(), new FakeConfirmView());
            presenter.Start();

            list.Replace(new[]
            {
                new LobbyParticipant("host-1", "방장", true),
                new LobbyParticipant("player-2", "게스트", false),
            });

            Assert.That(view.Participants.Count, Is.EqualTo(2));
            Assert.That(view.LocalIsHost, Is.False);
        }

        [Test]
        public void ReportClicked_AsksThenSendsTheReport()
        {
            var list = new LobbyParticipantList(new[]
            {
                new LobbyParticipant("host-1", "방장", true),
                new LobbyParticipant("player-2", "게스트", false),
            });
            using var room = new RoomBrowserSystem();
            room.SetLocalPlayer("host-1");
            var reports = new FakeReportGateway();
            var view = new FakePlayerListView();
            var confirm = new FakeConfirmView();
            var context = new FakeReportContext { CurrentKey = "ROOM#1" };
            using var presenter = new MatchPlayerListPresenter(
                list, room, view, reports, context, confirm);

            presenter.Start();
            view.RaiseReport("account-2", "게스트");

            Assert.That(confirm.Message, Is.EqualTo(LobbyPlayerListView.FormatReportTitle("게스트")));
            Assert.That(confirm.ConfirmLabel, Is.EqualTo(LobbyPlayerListView.ReportConfirmLabel));
            Assert.That(confirm.ChooseReason, Is.True);
            Assert.That(reports.Sent, Is.Empty);

            confirm.SelectedReason = ReportReason.Cheating;
            confirm.Note = "채팅으로 욕설을 했습니다";
            confirm.RaiseConfirm();

            Assert.That(
                reports.Sent,
                Is.EqualTo(new[] { ("account-2", ReportReason.Cheating, "채팅으로 욕설을 했습니다", "ROOM#1") }));
            Assert.That(confirm.IsVisible, Is.False);

            context.CurrentKey = "ROOM#2";
            view.RaiseReport("account-2", "게스트");
            confirm.RaiseConfirm();
            Assert.That(reports.Sent[1].ContextKey, Is.EqualTo("ROOM#2"));
        }

        private sealed class FakePlayerListView : ILobbyPlayerListView
        {
            public IReadOnlyList<LobbyParticipant> Participants { get; private set; } =
                Array.Empty<LobbyParticipant>();
            public IReadOnlyList<FriendSummary> Friends { get; private set; } =
                Array.Empty<FriendSummary>();
            public bool LocalIsHost { get; private set; }
            public string LocalPlayerId { get; private set; }

            public event Action<string, string> KickClicked;
            public event Action<string, string> InviteClicked;
            public event Action<string, string> ReportClicked;

            public void SetParticipants(
                IReadOnlyList<LobbyParticipant> participants,
                bool localIsHost,
                string localPlayerId,
                bool namesReady = true)
            {
                Participants = participants ?? Array.Empty<LobbyParticipant>();
                LocalIsHost = localIsHost;
                LocalPlayerId = localPlayerId;
            }

            public void SetFriends(IReadOnlyList<FriendSummary> friends)
            {
                Friends = friends ?? Array.Empty<FriendSummary>();
            }

            public void RaiseReport(string id, string name) => ReportClicked?.Invoke(id, name);
        }

        private sealed class FakeReportContext : IReportContext
        {
            public string CurrentKey { get; set; } = "ROOM#1";
        }

        private sealed class FakeReportGateway : IReportGateway
        {
            public List<(string PlayerId, ReportReason Reason, string Note, string ContextKey)> Sent { get; } = new();

            public UniTask<BackendResult> ReportAsync(
                string playerId,
                ReportReason reason,
                string note,
                string contextKey,
                CancellationToken cancellation)
            {
                Sent.Add((playerId, reason, note, contextKey));
                return UniTask.FromResult(BackendResult.Success());
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
