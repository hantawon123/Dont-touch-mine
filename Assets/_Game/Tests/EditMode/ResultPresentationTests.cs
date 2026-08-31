using System;
using System.Collections.Generic;
using Game.Bootstrap;
using Game.Client.Match;
using Game.Core.Items;
using Game.Core.Lobby;
using Game.Core.Match;
using Game.Core.Rooms;
using Game.Network.Match;
using Game.Server.Match;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    public sealed class ResultPresentationTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void ResultSurvivesSceneChange_AndReturnWaitsForSceneLoad(bool phaseFirst)
        {
            using var room = CreateRoom();
            var network = new FakeNetwork();
            using var controller = new NetworkResultLobbyReturnController(network, network, room);
            controller.Start();
            var result = new MatchResult(MatchEndReason.TimeExpired, 100, new[] { 1 });
            if (phaseFirst) network.Publish(new MatchStateSnapshot(MatchPhase.Result, 0));
            network.Publish(result);
            controller.Tick(10);
            if (!phaseFirst)
            {
                Assert.That(network.LoadCalls, Is.Zero);
                network.Publish(new MatchStateSnapshot(MatchPhase.Result, 0));
            }
            controller.Tick(11);
            Assert.That(network.LoadCalls, Is.EqualTo(1));
            controller.Tick(100);
            Assert.That(network.ReturnCalls, Is.Zero);
            var view = new FakeView();
            using var presenter = new ResultPresenter(controller, view);
            presenter.Start();
            Assert.That(view.Text, Does.Contain("승리").And.Contain("승자: 민수").And.Contain("제한 시간 종료"));
            network.IsResultSceneLoaded = true;
            controller.Tick(100);
            controller.Tick(104.9);
            Assert.That(network.ReturnCalls, Is.Zero);
            controller.Tick(105);
            controller.Tick(106);
            Assert.That(network.ReturnCalls, Is.EqualTo(1));
            network.Publish(new MatchStateSnapshot(MatchPhase.Waiting, 0));
            Assert.That(view.Text, Is.EqualTo("표시할 경기 결과가 없습니다."));
        }

        [Test]
        public void ParticipantDisplaysResult_ButNeverLoadsOrReturnsTheRoom()
        {
            using var room = CreateRoom();
            var network = new FakeNetwork { IsServer = false };
            using var controller = new NetworkResultLobbyReturnController(network, network, room);
            controller.Start();
            network.Publish(new MatchResult(MatchEndReason.AllPlayerItemsDestroyed, 100, new[] { 0 }));
            network.Publish(new MatchStateSnapshot(MatchPhase.Result, 0));
            controller.Tick(100);
            Assert.That(controller.ResultText.CurrentValue, Does.Contain("패배").And.Contain("방장"));
            Assert.That(network.LoadCalls, Is.Zero);
            Assert.That(network.ReturnCalls, Is.Zero);
        }

        [Test]
        public void FormatterHandlesJointWinnersAndNoWinner()
        {
            using var room = CreateRoom();
            Assert.That(NetworkResultLobbyReturnController.FormatResult(
                new MatchResult(MatchEndReason.TimeExpired, 100, new[] { 0, 1 }), room),
                Does.Contain("승자: 방장, 민수"));
            Assert.That(NetworkResultLobbyReturnController.FormatResult(
                new MatchResult(MatchEndReason.TimeExpired, 100, new int[0]), room),
                Does.Contain("승자 없음"));
        }

        private static RoomBrowserSystem CreateRoom()
        {
            var room = new RoomBrowserSystem();
            room.SetParticipants(new[] { new RoomParticipant("host", 0, true, "방장"),
                new RoomParticipant("client", 1, false, "민수") });
            room.MatchStarted(new[] { new MatchParticipant("host", 0), new MatchParticipant("client", 1) });
            room.SetLocalPlayer("client");
            return room;
        }

        private sealed class FakeView : IResultView
        {
            public string Text { get; private set; }
            public void SetText(string value) => Text = value;
        }

        private sealed class FakeNetwork : INetworkMatchEvents, INetworkResultNavigation
        {
            public bool IsServer { get; set; } = true;
            public bool IsResultSceneLoaded { get; set; }
            public int LoadCalls { get; private set; }
            public int ReturnCalls { get; private set; }
            public bool EnterResultScene() { LoadCalls++; return true; }
            public bool RequestReturnToLobby() { ReturnCalls++; return true; }
            public event Action<MatchStateSnapshot> MatchStateReceived;
            public event Action<MatchResult> MatchResultReceived;
            public event Action<string> ItemAssignmentReceived;
            public event Action<PlayerItemDestroyedEvent> ItemDestroyedReceived;
            public event Action<IReadOnlyList<PlayerInteractionStateSnapshot>> PlayerInteractionStatesReceived;
            public event Action<IReadOnlyList<HighlightReplayData>> HighlightReplayReceived;
            public void Publish(MatchStateSnapshot value) => MatchStateReceived?.Invoke(value);
            public void Publish(MatchResult value) => MatchResultReceived?.Invoke(value);
        }
    }
}
