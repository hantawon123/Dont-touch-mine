using System;
using Game.Bootstrap;
using Game.Core.Flow;
using Game.Core.Match;
using Game.Network.Match;
using Game.Server.Match;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    public sealed class NetworkMatchFlowIntegrationTests
    {
        [Test]
        public void TwoPeers_FollowConfirmedMatchFlowToResult()
        {
            var network = new FakeNetworkMatchEvents();
            var hostFlow = CreateLobbyFlow();
            var clientFlow = CreateLobbyFlow();
            using var host = new NetworkMatchFlowSynchronizer(network, hostFlow);
            using var client = new NetworkMatchFlowSynchronizer(network, clientFlow);
            host.Start();
            client.Start();

            network.Publish(new MatchStateSnapshot(MatchPhase.Hiding, 180d));
            AssertPeersAt(hostFlow, clientFlow, AppFlowState.InGame);

            network.Publish(new MatchStateSnapshot(MatchPhase.Searching, 540d));
            AssertPeersAt(hostFlow, clientFlow, AppFlowState.InGame);

            network.Publish(new MatchStateSnapshot(MatchPhase.Highlight, 570d));
            AssertPeersAt(hostFlow, clientFlow, AppFlowState.Highlight);

            network.Publish(new MatchResult(
                MatchEndReason.TimeExpired,
                570d,
                new[] { 0, 1 }));
            AssertPeersAt(hostFlow, clientFlow, AppFlowState.Result);
        }

        [Test]
        public void LastPlayerStanding_ReturnsBothPeersDirectlyToLobby()
        {
            var network = new FakeNetworkMatchEvents();
            var hostFlow = CreateLobbyFlow();
            var clientFlow = CreateLobbyFlow();
            using var host = new NetworkMatchFlowSynchronizer(network, hostFlow);
            using var client = new NetworkMatchFlowSynchronizer(network, clientFlow);
            host.Start();
            client.Start();

            network.Publish(new MatchStateSnapshot(MatchPhase.Hiding, 180d));
            network.Publish(new MatchResult(
                MatchEndReason.LastPlayerStanding,
                90d,
                new[] { 0 }));

            AssertPeersAt(hostFlow, clientFlow, AppFlowState.Lobby);
        }

        private static AppFlowSystem CreateLobbyFlow()
        {
            var flow = new AppFlowSystem();
            Assert.That(flow.TryTransitionTo(AppFlowState.RoomBrowser), Is.True);
            Assert.That(flow.TryTransitionTo(AppFlowState.Lobby), Is.True);
            return flow;
        }

        private static void AssertPeersAt(
            AppFlowSystem host,
            AppFlowSystem client,
            AppFlowState expected)
        {
            Assert.That(host.CurrentState, Is.EqualTo(expected));
            Assert.That(client.CurrentState, Is.EqualTo(expected));
        }

        private sealed class FakeNetworkMatchEvents : INetworkMatchEvents
        {
            public event Action<MatchStateSnapshot> MatchStateReceived;
            public event Action<System.Collections.Generic.IReadOnlyList<
                PlayerInteractionStateSnapshot>> PlayerInteractionStatesReceived;
            public event Action<MatchResult> MatchResultReceived;

            public void Publish(MatchStateSnapshot snapshot)
            {
                MatchStateReceived?.Invoke(snapshot);
            }

            public void Publish(MatchResult result)
            {
                MatchResultReceived?.Invoke(result);
            }
        }
    }
}
