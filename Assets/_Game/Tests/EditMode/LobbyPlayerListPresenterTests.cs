using System;
using System.Collections.Generic;
using Game.Client.Lobby;
using Game.Core.Lobby;
using NUnit.Framework;

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
            using var presenter = new LobbyPlayerListPresenter(
                list,
                host,
                view,
                new FakeConfirmView(),
                new FakeConfirmView());

            presenter.Start();

            Assert.That(view.Participants.Count, Is.EqualTo(2));
            Assert.That(view.LocalIsHost, Is.True);
            Assert.That(view.UpdateCount, Is.EqualTo(1));
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
                view,
                kickConfirm,
                new FakeConfirmView());

            presenter.Start();
            view.RaiseKick("player-2", "게스트");
            kickConfirm.RaiseConfirm();

            Assert.That(kicked, Is.EqualTo(new[] { "player-2" }));
            Assert.That(kickConfirm.IsVisible, Is.False);
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

        private static LobbyHostSession CreateHostSession(bool isHost)
        {
            return new LobbyHostSession(
                "host-1",
                isHost,
                new PlaySettingsDraft("방", "CODE", false, string.Empty, 6, 5, "map"));
        }

        private sealed class FakePlayerListView : ILobbyPlayerListView
        {
            public IReadOnlyList<LobbyParticipant> Participants { get; private set; }
            public bool LocalIsHost { get; private set; }
            public int UpdateCount { get; private set; }

            public event Action<string, string> KickClicked;
            public event Action<string, string> TransferClicked;

            public void SetParticipants(
                IReadOnlyList<LobbyParticipant> participants,
                bool localIsHost,
                string localPlayerId)
            {
                Participants = participants;
                LocalIsHost = localIsHost;
                UpdateCount++;
            }

            public void RaiseKick(string id, string name) => KickClicked?.Invoke(id, name);
        }

        private sealed class FakeConfirmView : IKickConfirmView, IHostTransferConfirmView
        {
            public bool IsVisible { get; private set; }
            public event Action Confirmed;
            public event Action Cancelled;

            public void Show(string message) => IsVisible = true;

            public void Hide() => IsVisible = false;

            public void RaiseConfirm() => Confirmed?.Invoke();
        }
    }
}
