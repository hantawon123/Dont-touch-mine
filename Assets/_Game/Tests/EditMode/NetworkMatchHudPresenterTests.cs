using System;
using System.Collections.Generic;
using Game.Bootstrap;
using Game.Client.Match;
using Game.Core.Lobby;
using Game.Core.Match;
using Game.Core.Rooms;
using Game.Network.Match;
using Game.SOAP.Config;
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
            room.SetLocalPlayer("client");

            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();
            using var presenter = new NetworkMatchHudPresenter(
                network,
                network,
                room,
                rules,
                view);
            presenter.Start();

            network.Publish(new MatchStateSnapshot(MatchPhase.Searching, 40d));
            network.PublishItemAssignment("Soda_01");
            presenter.Tick();
            Assert.That(view.Phase, Is.EqualTo(MatchPhase.Searching));
            Assert.That(view.RemainingSeconds, Is.EqualTo(30d));
            Assert.That(view.AssignedItem, Is.EqualTo("탄산음료"));

            network.Publish(new[]
            {
                new PlayerInteractionStateSnapshot(0, 0d, 5),
                new PlayerInteractionStateSnapshot(1, 0d, 3),
            });
            Assert.That(view.RemainingDestructionUses, Is.EqualTo(3));

            network.Publish(new PlayerItemDestroyedEvent(1, "SecretItem", 12d));
            Assert.That(view.Notice, Is.EqualTo("민수님이 물건을 파괴했습니다!"));
            Assert.That(view.Notice, Does.Not.Contain("SecretItem"));

            network.ServerTime = 16d;
            presenter.Tick();
            Assert.That(view.NoticeVisible, Is.False);
        }

        /// <summary>
        /// The phase line names whoever the hiding turn is waiting on. Turns are
        /// derived from the phase's end time, so this also pins the derivation:
        /// two players at 30s each means hiding began 60s before it ends.
        /// </summary>
        [Test]
        public void HidingPhase_NamesThePlayerWhoseTurnItIs()
        {
            var network = new FakeNetwork();
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

            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();
            using var presenter = new NetworkMatchHudPresenter(
                network, network, room, rules, view);
            presenter.Start();

            // 끝이 100초, 2명 x 30초 => 40초에 시작.
            network.ServerTime = 50d;
            network.Publish(new MatchStateSnapshot(MatchPhase.Hiding, 100d));
            Assert.That(view.Phase, Is.EqualTo(MatchPhase.Hiding));
            Assert.That(view.HidingPlayerName, Is.EqualTo("방장"));

            network.ServerTime = 75d;
            presenter.Tick();
            Assert.That(view.HidingPlayerName, Is.EqualTo("민수"));

            // 단계가 마지막 턴보다 길어져도 없는 사람을 부르지 않는다.
            network.ServerTime = 130d;
            presenter.Tick();
            Assert.That(view.HidingPlayerName, Is.EqualTo("민수"));

            // 숨기기가 아닌 단계는 아무도 지목하지 않는다.
            network.Publish(new MatchStateSnapshot(MatchPhase.Searching, 200d));
            Assert.That(view.HidingPlayerName, Is.Empty);
        }

        private sealed class FakeView : INetworkMatchHudView
        {
            public MatchPhase Phase { get; private set; }
            public double RemainingSeconds { get; private set; }
            public string Notice { get; private set; }
            public bool NoticeVisible { get; private set; }
            public string AssignedItem { get; private set; }
            public int RemainingDestructionUses { get; private set; } = -1;

            public string HidingPlayerName { get; private set; }

            public void SetPhase(MatchPhase phase, string hidingPlayerName)
            {
                Phase = phase;
                HidingPlayerName = hidingPlayerName;
            }
            public void SetRemainingSeconds(double value) => RemainingSeconds = value;
            public void SetAssignedItem(string displayName) => AssignedItem = displayName;
            public void SetRemainingDestructionUses(int value) =>
                RemainingDestructionUses = value;

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
            public event Action<string> ItemAssignmentReceived;
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
            public void PublishItemAssignment(string itemId) =>
                ItemAssignmentReceived?.Invoke(itemId);
            public void Publish(PlayerItemDestroyedEvent value) =>
                ItemDestroyedReceived?.Invoke(value);
            public void Publish(IReadOnlyList<PlayerInteractionStateSnapshot> value) =>
                PlayerInteractionStatesReceived?.Invoke(value);
        }
    }
}
