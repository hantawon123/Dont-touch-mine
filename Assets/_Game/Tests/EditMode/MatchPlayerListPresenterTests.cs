using System;
using System.Collections.Generic;
using Game.Client.Lobby;
using Game.Client.Match;
using Game.Core.Home;
using Game.Core.Lobby;
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
            using var presenter = new MatchPlayerListPresenter(list, room, view);

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
            using var presenter = new MatchPlayerListPresenter(list, room, view);
            presenter.Start();

            list.Replace(new[]
            {
                new LobbyParticipant("host-1", "방장", true),
                new LobbyParticipant("player-2", "게스트", false),
            });

            Assert.That(view.Participants.Count, Is.EqualTo(2));
            Assert.That(view.LocalIsHost, Is.False);
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
        }
    }
}
