using System;
using System.Collections.Generic;
using Game.Bootstrap;
using Game.Client.Match;
using Game.Core.Lobby;
using Game.Core.Match;
using Game.Core.Rooms;
using Game.Network.Match;
using Game.Server.Items;
using Game.Server.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class NetworkMatchHudPresenterTests
    {
        [Test]
        public void ConfirmedEvents_UpdateTimerPhaseAndAnonymousItemNotice()
        {
            var network = new FakeNetwork { ServerTime = 10d };
            var view = new FakeView();
            using var room = new RoomBrowserSystem();
            room.SetParticipants(new[]
            {
                new RoomParticipant("host", 0, true, "방장"),
                new RoomParticipant("client", 1, false, "민수"),
            });
            room.MatchStarted(new[]
            {
                new MatchParticipant("host", 0),
                new MatchParticipant("client", 1),
            });

            using var presenter = new NetworkMatchHudPresenter(
                network,
                network,
                room,
                view);
            presenter.Start();

            network.Publish(new MatchStateSnapshot(MatchPhase.Searching, 40d));
            presenter.Tick();
            Assert.That(view.Phase, Is.EqualTo(MatchPhase.Searching));
            Assert.That(view.RemainingSeconds, Is.EqualTo(30d));

            network.Publish(new PlayerItemDestroyedEvent(1, "SecretItem", 12d));
            Assert.That(view.Notice, Is.EqualTo("민수님이 물건을 파괴했습니다!"));
            Assert.That(view.Notice, Does.Not.Contain("SecretItem"));

            network.ServerTime = 16d;
            presenter.Tick();
            Assert.That(view.NoticeVisible, Is.False);
        }

        private sealed class FakeView : INetworkMatchHudView
        {
            public MatchPhase Phase { get; private set; }
            public double RemainingSeconds { get; private set; }
            public string Notice { get; private set; }
            public bool NoticeVisible { get; private set; }

            public void SetPhase(MatchPhase phase) => Phase = phase;
            public void SetRemainingSeconds(double value) => RemainingSeconds = value;

            public void ShowDestructionNotice(string message)
            {
                Notice = message;
                NoticeVisible = true;
            }

            public void HideDestructionNotice() => NoticeVisible = false;
            public void SetShredderMarker(Vector2 screenPosition, bool visible) { }
        }

        private sealed class FakeNetwork : INetworkMatchEvents, INetworkMatchRuntimeSource
        {
            public double ServerTime { get; set; }
            public event Action<MatchStateSnapshot> MatchStateReceived;
            public event Action<PlayerItemDestroyedEvent> ItemDestroyedReceived;
            public event Action<IReadOnlyList<PlayerInteractionStateSnapshot>>
                PlayerInteractionStatesReceived;
            public event Action<IReadOnlyList<HighlightReplayData>> HighlightReplayReceived;
            public event Action<MatchResult> MatchResultReceived;

            public bool TryGetPlayerPose(string playerId, out Pose pose)
            {
                pose = default;
                return false;
            }

            public void Publish(MatchStateSnapshot value) => MatchStateReceived?.Invoke(value);
            public void Publish(PlayerItemDestroyedEvent value) =>
                ItemDestroyedReceived?.Invoke(value);
        }
    }
}
