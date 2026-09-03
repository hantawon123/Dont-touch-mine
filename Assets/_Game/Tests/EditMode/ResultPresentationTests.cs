using System;
using System.Collections.Generic;
using Game.Bootstrap;
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
        [Test]
        public void HighlightPhase_ShowsResultBeforePreparingLobby()
        {
            using var room = CreateRoom();
            var network = new FakeNetwork();
            using var controller = new NetworkResultLobbyReturnController(network, network, room);
            controller.Start();

            network.Publish(new MatchResult(MatchEndReason.TimeExpired, 0d, new[] { 1 }));
            network.Publish(new MatchStateSnapshot(MatchPhase.Highlight, 100d));
            controller.Tick(0d);
            controller.Tick(HighlightPresentationTiming.PostRollSeconds - 0.01d);
            Assert.That(network.LoadCalls, Is.Zero);
            Assert.That(network.PrepareLobbyCalls, Is.Zero);

            controller.Tick(HighlightPresentationTiming.PostRollSeconds);
            Assert.That(network.LoadCalls, Is.EqualTo(1));
            network.IsResultSceneLoaded = true;
            controller.Tick(HighlightPresentationTiming.PostRollSeconds);
            controller.Tick(
                HighlightPresentationTiming.PostRollSeconds +
                NetworkResultLobbyReturnController.ResultDisplaySeconds - 0.01d);
            Assert.That(network.PrepareLobbyCalls, Is.Zero);
            controller.Tick(
                HighlightPresentationTiming.PostRollSeconds +
                NetworkResultLobbyReturnController.ResultDisplaySeconds);

            Assert.That(network.PrepareLobbyCalls, Is.EqualTo(1));
            Assert.That(network.ReturnCalls, Is.Zero);
        }

        [Test]
        public void ResultPhase_ReturnsAuthorityDirectlyToLobbyOnce()
        {
            using var room = CreateRoom();
            var network = new FakeNetwork();
            using var controller = new NetworkResultLobbyReturnController(network, network, room);
            controller.Start();
            var result = new MatchResult(MatchEndReason.TimeExpired, 100, new[] { 1 });
            network.Publish(result);
            network.Publish(new MatchStateSnapshot(MatchPhase.Result, 0));
            controller.Tick(0);
            Assert.That(network.LoadCalls, Is.Zero);
            Assert.That(network.ReturnCalls, Is.EqualTo(1));
            controller.Tick(1);
            Assert.That(network.ReturnCalls, Is.EqualTo(1));
        }

        [Test]
        public void Migration_NewHostWaitsForReadinessBeforeReturning()
        {
            using var room = CreateRoom();
            var network = new FakeNetwork { IsServer = false };
            using var controller = new NetworkResultLobbyReturnController(network, network, room);
            controller.Start();
            network.Publish(new MatchResult(MatchEndReason.TimeExpired, 100, new[] { 1 }));
            network.Publish(new MatchStateSnapshot(MatchPhase.Result, 0));
            controller.Tick(0);
            network.IsServer = true;
            network.IsRuntimeReady = false;
            controller.Tick(10);
            controller.Tick(100);
            Assert.That(network.LoadCalls, Is.Zero);
            Assert.That(network.ReturnCalls, Is.Zero);
            network.IsRuntimeReady = true;
            controller.Tick(101);
            Assert.That(network.ReturnCalls, Is.EqualTo(1));
            Assert.That(network.LoadCalls, Is.Zero);
        }

        [Test]
        public void ReturnAuthorization_UsesRetainedParticipantsWithoutMatchSession()
        {
            var playing = new[] { new MatchParticipant("host", 0), new MatchParticipant("client", 1) };
            Assert.That(MatchStarter.IsReturnParticipant(playing, "host"), Is.True);
            Assert.That(MatchStarter.IsReturnParticipant(playing, "client"), Is.True);
            Assert.That(MatchStarter.IsReturnParticipant(playing, "stranger"), Is.False);
            Assert.That(MatchStarter.IsReturnParticipant(playing, null), Is.False);
        }

        [Test]
        public void MatchChatAuthorization_UsesFrozenLineUp()
        {
            var playing = new[] { new MatchParticipant("host", 0), new MatchParticipant("client", 1) };

            Assert.That(MatchStarter.IsMatchChatParticipant(playing, "host"), Is.True);
            Assert.That(MatchStarter.IsMatchChatParticipant(playing, "client"), Is.True);
            Assert.That(MatchStarter.IsMatchChatParticipant(playing, "late-joiner"), Is.False);
            Assert.That(MatchStarter.IsMatchChatParticipant(playing, ""), Is.False);
            Assert.That(MatchStarter.IsMatchChatParticipant(null, "host"), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ResultAndPhase_ReturnAuthorityDirectlyToLobby(bool phaseFirst)
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
            Assert.That(network.ReturnCalls, Is.EqualTo(1));
            Assert.That(network.LoadCalls, Is.Zero);
            Assert.That(controller.ResultText.CurrentValue,
                Does.Contain("승리").And.Contain("승자: 민수").And.Contain("제한 시간 종료"));
            var displayedResult = controller.ResultText.CurrentValue;
            network.Publish(new MatchStateSnapshot(MatchPhase.Waiting, 0));
            Assert.That(controller.ResultText.CurrentValue, Is.EqualTo(displayedResult));
            controller.Tick(107);
            Assert.That(network.ReturnCalls, Is.EqualTo(1));
            network.Publish(new MatchStateSnapshot(MatchPhase.Hiding, 0));
            Assert.That(controller.ResultText.CurrentValue,
                Is.EqualTo("표시할 경기 결과가 없습니다."));
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
            var displayedResult = controller.ResultText.CurrentValue;
            network.Publish(new MatchStateSnapshot(MatchPhase.Waiting, 0));
            Assert.That(controller.ResultText.CurrentValue, Is.EqualTo(displayedResult));
        }

        [Test]
        public void MissingResultData_ShowsFallbackAndReturnsToLobby()
        {
            using var room = CreateRoom();
            var network = new FakeNetwork { IsResultSceneLoaded = true };
            using var controller = new NetworkResultLobbyReturnController(network, network, room);
            controller.Start();

            network.Publish(new MatchStateSnapshot(MatchPhase.Result, 0d));
            controller.Tick(0d);
            controller.Tick(NetworkMatchFlowSynchronizer.ResultDataGraceSeconds - 0.01d);
            Assert.That(network.ReturnCalls, Is.Zero);

            controller.Tick(NetworkMatchFlowSynchronizer.ResultDataGraceSeconds);
            Assert.That(controller.ResultText.CurrentValue,
                Does.Contain("경기 결과 데이터를 받지 못했습니다"));
            Assert.That(network.ReturnCalls, Is.EqualTo(1));
        }

        [Test]
        public void LastPlayerStanding_DoesNotFallbackOnStaleResultPhase()
        {
            using var room = CreateRoom();
            var network = new FakeNetwork { IsResultSceneLoaded = true };
            using var controller = new NetworkResultLobbyReturnController(network, network, room);
            controller.Start();

            network.Publish(new MatchResult(MatchEndReason.LastPlayerStanding, 100d, new[] { 0 }));
            network.Publish(new MatchStateSnapshot(MatchPhase.Result, 0d));
            controller.Tick(0d);
            controller.Tick(NetworkMatchFlowSynchronizer.ResultDataGraceSeconds + 5d);

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

        [Test]
        public void FormatterUsesPlayerIndexWhenParticipantDataIsMissing()
        {
            using var room = new RoomBrowserSystem();
            var text = NetworkResultLobbyReturnController.FormatResult(
                new MatchResult(MatchEndReason.TimeExpired, 100, new[] { 3 }),
                room);

            Assert.That(text, Does.Contain("승자: 플레이어 4"));
            Assert.That(text, Does.Contain("제한 시간 종료"));
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

        private sealed class FakeNetwork : INetworkMatchEvents, INetworkResultNavigation
        {
            public IReadOnlyList<PlayerItemStatusSnapshot> LatestPlayerItemStatuses { get; } =
                Array.Empty<PlayerItemStatusSnapshot>();
            public bool IsServer { get; set; } = true;
            public bool IsRuntimeReady { get; set; } = true;
            public bool IsResultSceneLoaded { get; set; }
            public int LoadCalls { get; private set; }
            public int PrepareLobbyCalls { get; private set; }
            public int CompleteHighlightCalls { get; private set; }
            public int ReturnCalls { get; private set; }
            public bool EnterResultScene() { LoadCalls++; return true; }
            public bool PrepareLobbyForHighlights() { PrepareLobbyCalls++; return true; }
            public bool CompleteLocalHighlightViewing() { CompleteHighlightCalls++; return true; }
            public bool RequestReturnToLobby() { ReturnCalls++; return true; }
            public event Action<MatchStateSnapshot> MatchStateReceived;
            public event Action<Game.Core.Lobby.LobbyChatMessage> MatchChatReceived;
            public event Action<MatchResult> MatchResultReceived;
            public event Action<string> ItemAssignmentReceived;
            public event Action<PlayerItemDestroyedEvent> ItemDestroyedReceived;
            public event Action<IReadOnlyList<PlayerItemStatusSnapshot>> PlayerItemStatusesReceived;
            public event Action<IReadOnlyList<PlayerInteractionStateSnapshot>> PlayerInteractionStatesReceived;
            public event Action<IReadOnlyList<HighlightReplayData>> HighlightReplayReceived;
            public void Publish(MatchStateSnapshot value) => MatchStateReceived?.Invoke(value);
            public void Publish(MatchResult value) => MatchResultReceived?.Invoke(value);
        }
    }
}
