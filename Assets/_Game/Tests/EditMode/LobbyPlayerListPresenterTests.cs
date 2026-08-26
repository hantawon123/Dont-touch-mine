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
            var view = new FakePlayerListView();
            using var presenter = new LobbyPlayerListPresenter(list, view);

            presenter.Start();

            Assert.That(view.Participants.Count, Is.EqualTo(2));
            Assert.That(view.Participants[0].DisplayName, Is.EqualTo("방장"));
            Assert.That(view.Participants[0].IsHost, Is.True);
            Assert.That(view.UpdateCount, Is.EqualTo(1));
        }

        [Test]
        public void Replace_UpdatesViewImmediately()
        {
            var list = new LobbyParticipantList(new[]
            {
                new LobbyParticipant("1", "방장", true),
            });
            var view = new FakePlayerListView();
            using var presenter = new LobbyPlayerListPresenter(list, view);
            presenter.Start();

            list.Replace(new[]
            {
                new LobbyParticipant("1", "방장", true),
                new LobbyParticipant("2", "신규", false),
            });

            Assert.That(view.Participants.Count, Is.EqualTo(2));
            Assert.That(view.Participants[1].DisplayName, Is.EqualTo("신규"));
            Assert.That(view.UpdateCount, Is.EqualTo(2));
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

        private sealed class FakePlayerListView : ILobbyPlayerListView
        {
            public IReadOnlyList<LobbyParticipant> Participants { get; private set; }
            public int UpdateCount { get; private set; }

            public void SetParticipants(IReadOnlyList<LobbyParticipant> participants)
            {
                Participants = participants;
                UpdateCount++;
            }
        }
    }
}
