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
        [Test]
        public void Migration_NewHostKeepsAlreadyLoadedResultScene()
        {
            using var room = CreateRoom();
            var network = new FakeNetwork { IsServer = false, IsResultSceneLoaded = true };
            using var controller = new NetworkResultLobbyReturnController(network, network, room);
            controller.Start();
            var result = new MatchResult(MatchEndReason.TimeExpired, 100, new[] { 1 });
            network.Publish(result);
            network.Publish(new MatchStateSnapshot(MatchPhase.Result, 0));
            controller.Tick(0);
            network.IsServer = true;
            network.IsRuntimeReady = false;
            controller.Tick(10);
            Assert.That(network.LoadCalls, Is.Zero);
            Assert.That(network.ReturnCalls, Is.Zero);
            network.IsRuntimeReady = true;
            controller.Tick(100);
            network.Publish(new MatchStateSnapshot(MatchPhase.Result, 0));
            network.Publish(result); // Scene state is republished after restoration.
            controller.Tick(104.9);
            Assert.That(network.LoadCalls, Is.Zero);
            Assert.That(network.ReturnCalls, Is.Zero);
            controller.Tick(105);
            controller.Tick(106);
            Assert.That(network.LoadCalls, Is.Zero);
            Assert.That(network.ReturnCalls, Is.EqualTo(1));
        }

        [Test]
        public void Migration_SearchingRollbackClearsOldResultNavigation()
        {
            using var room = CreateRoom();
            var network = new FakeNetwork { IsResultSceneLoaded = true };
            using var controller = new NetworkResultLobbyReturnController(network, network, room);
            controller.Start();
            network.Publish(new MatchResult(MatchEndReason.TimeExpired, 100, new[] { 1 }));
            network.Publish(new MatchStateSnapshot(MatchPhase.Result, 0));
            controller.Tick(0);
            controller.Tick(5); // Old result already returned once.
            Assert.That(network.ReturnCalls, Is.EqualTo(1));
            network.IsResultSceneLoaded = false;
            network.Publish(new MatchStateSnapshot(MatchPhase.Searching, 200));
            controller.Tick(100);
            network.Publish(new MatchStateSnapshot(MatchPhase.Result, 0));
            controller.Tick(200);
            Assert.That(network.LoadCalls, Is.Zero, "The old result must not trigger navigation.");
            network.Publish(new MatchResult(MatchEndReason.TimeExpired, 200, new[] { 0 }));
            controller.Tick(201);
            Assert.That(network.LoadCalls, Is.EqualTo(1));
            network.IsResultSceneLoaded = true;
            controller.Tick(202);
            controller.Tick(207);
            Assert.That(network.ReturnCalls, Is.EqualTo(2), "The old returned flag must not block the restored match.");
        }

        [Test]
        public void Migration_NewHostWaitsForReadinessBeforeResultNavigation()
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
            Assert.That(network.LoadCalls, Is.EqualTo(1));
            controller.Tick(110);
            Assert.That(network.ReturnCalls, Is.Zero);
            network.IsResultSceneLoaded = true;
            controller.Tick(120);
            controller.Tick(124.9);
            Assert.That(network.ReturnCalls, Is.Zero);
            controller.Tick(125);
            Assert.That(network.ReturnCalls, Is.EqualTo(1));
        }

        [Test]
        public void Migration_PausesAnExistingResultCountdownWithoutReloading()
        {
            using var room = CreateRoom();
            var network = new FakeNetwork();
            using var controller = new NetworkResultLobbyReturnController(network, network, room);
            controller.Start();
            network.Publish(new MatchResult(MatchEndReason.TimeExpired, 100, new[] { 1 }));
            network.Publish(new MatchStateSnapshot(MatchPhase.Result, 0));
            controller.Tick(10);
            network.IsResultSceneLoaded = true;
            controller.Tick(10); // Return at 15; three seconds remain when migration starts.
            network.IsRuntimeReady = false;
            controller.Tick(12);
            controller.Tick(100);
            Assert.That(network.ReturnCalls, Is.Zero);
            network.IsRuntimeReady = true;
            controller.Tick(102);
            controller.Tick(104.9);
            Assert.That(network.ReturnCalls, Is.Zero);
            controller.Tick(105);
            controller.Tick(106);
            Assert.That(network.ReturnCalls, Is.EqualTo(1));
            Assert.That(network.LoadCalls, Is.EqualTo(1));
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
            var transition = new FakeTransition();
            using var presenter = new ResultPresenter(controller, view, transition);
            presenter.Start();
            Assert.That(transition.Opacity, Is.EqualTo(1f));
            presenter.Tick(0.15f);
            Assert.That(transition.Opacity, Is.EqualTo(0.5f).Within(0.001f));
            presenter.Tick(0.15f);
            Assert.That(transition.Opacity, Is.Zero);
            Assert.That(view.Text, Does.Contain("승리").And.Contain("승자: 민수").And.Contain("제한 시간 종료"));
            network.IsResultSceneLoaded = true;
            controller.Tick(100);
            controller.Tick(104.9);
            Assert.That(network.ReturnCalls, Is.Zero);
            controller.Tick(105);
            controller.Tick(106);
            Assert.That(network.ReturnCalls, Is.EqualTo(1));
            var displayedResult = view.Text;
            network.Publish(new MatchStateSnapshot(MatchPhase.Waiting, 0));
            Assert.That(view.Text, Is.EqualTo(displayedResult));
            controller.Tick(107);
            Assert.That(network.ReturnCalls, Is.EqualTo(1));
            network.Publish(new MatchStateSnapshot(MatchPhase.Hiding, 0));
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
            Assert.That(network.ReturnCalls, Is.Zero);

            controller.Tick(NetworkMatchFlowSynchronizer.ResultDataGraceSeconds + 5d);
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

        private sealed class FakeView : IResultView
        {
            public string Text { get; private set; }
            public void SetText(string value) => Text = value;
        }

        private sealed class FakeTransition : IHighlightTransitionView
        {
            public float Opacity { get; private set; }
            public void SetOpacity(float value) => Opacity = value;
        }

        private sealed class FakeNetwork : INetworkMatchEvents, INetworkResultNavigation
        {
            public IReadOnlyList<PlayerItemStatusSnapshot> LatestPlayerItemStatuses { get; } =
                Array.Empty<PlayerItemStatusSnapshot>();
            public bool IsServer { get; set; } = true;
            public bool IsRuntimeReady { get; set; } = true;
            public bool IsResultSceneLoaded { get; set; }
            public int LoadCalls { get; private set; }
            public int ReturnCalls { get; private set; }
            public bool EnterResultScene() { LoadCalls++; return true; }
            public bool RequestReturnToLobby() { ReturnCalls++; return true; }
            public event Action<MatchStateSnapshot> MatchStateReceived;
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
