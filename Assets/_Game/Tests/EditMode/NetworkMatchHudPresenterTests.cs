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
        [TestCase(false)]
        [TestCase(true)]
        public void GameEnd_CountsDownBeforeBlackTransition(bool phaseFirst)
        {
            var network = new FakeNetwork { ServerTime = 100d };
            var view = new FakeView();
            var transition = new FakeTransition();
            using var room = new RoomBrowserSystem();
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();
            try
            {
                using var playback = new NetworkHighlightPlaybackController(network, room, network, transition);
                using var presenter = new NetworkMatchHudPresenter(network, network, room, rules, view, playback);
                playback.Start();
                presenter.Start();
                // State callbacks can arrive while the replacement runner is starting.
                network.IsRuntimeReady = false;
                if (phaseFirst) network.Publish(new MatchStateSnapshot(MatchPhase.Highlight, 0d));
                network.Publish(new MatchResult(MatchEndReason.TimeExpired, 100d, new[] { 0 }));
                if (!phaseFirst) network.Publish(new MatchStateSnapshot(MatchPhase.Highlight, 0d));
                Assert.DoesNotThrow(() => presenter.Tick());
                Assert.DoesNotThrow(() => playback.Tick());
                network.IsRuntimeReady = true;
                for (var second = 0; second < 3; second++)
                {
                    network.ServerTime = 100d + second;
                    presenter.Tick();
                    playback.Tick();
                    Assert.That(view.NoticeVisible, Is.True);
                    Assert.That(view.Notice, Is.EqualTo("게임이 종료되었습니다!"));
                    Assert.That(view.EndCountdown, Is.EqualTo(3 - second));
                    Assert.That(transition.Opacity, Is.Zero);
                }
                network.ServerTime = 103d;
                presenter.Tick();
                playback.Tick();
                Assert.That(view.NoticeVisible, Is.False);
                Assert.That(transition.Opacity, Is.EqualTo(1f));
                Assert.That(view.EndCountdown, Is.Zero);
                network.Publish(new MatchStateSnapshot(MatchPhase.Result, 0d));
                Assert.That(transition.Opacity, Is.EqualTo(1f));
                playback.Dispose();
                Assert.That(transition.Opacity, Is.EqualTo(1f));
            }
            finally { UnityEngine.Object.DestroyImmediate(rules); }
        }

        private sealed class FakeTransition : IHighlightTransitionView
        {
            public float Opacity { get; private set; }
            public void SetOpacity(float opacity) => Opacity = opacity;
        }

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
            network.Publish(new MatchStateSnapshot(MatchPhase.Highlight, 0d));
            presenter.UpdateReplayNotice(11.9d);
            Assert.That(view.NoticeVisible, Is.False);
            presenter.UpdateReplayNotice(12d);
            Assert.That(view.Notice, Is.EqualTo("민수님이 물건을 파괴했습니다!"));
            Assert.That(view.NoticeVisible, Is.True);
            presenter.UpdateReplayNotice(15d);
            Assert.That(view.NoticeVisible, Is.False);
            // A later montage may revisit the same destruction.
            presenter.UpdateReplayNotice(12.5d);
            Assert.That(view.NoticeVisible, Is.True);
            presenter.UpdateReplayNotice(null);
            Assert.That(view.NoticeVisible, Is.False);
            network.Publish(new MatchStateSnapshot(MatchPhase.Result, 0d));
            presenter.Tick();
            Assert.That(view.NoticeVisible, Is.False);
            network.Publish(new MatchStateSnapshot(MatchPhase.Hiding, 100d));
            presenter.UpdateReplayNotice(12.5d);
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

            network.IsRuntimeReady = false;
            network.ServerTime = 75d;
            Assert.DoesNotThrow(() => presenter.Tick());
            Assert.That(view.HidingPlayerName, Is.EqualTo("방장"));
            network.IsRuntimeReady = true;

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

        [Test]
        public void Start_UsesCachedItemStatuses_AndDisposeUnsubscribes()
        {
            var network = new FakeNetwork
            {
                LatestPlayerItemStatuses = new[]
                {
                    new PlayerItemStatusSnapshot("Soda_01", false),
                    new PlayerItemStatusSnapshot("Burger_01", true),
                },
            };
            var view = new FakeView();
            using var room = new RoomBrowserSystem();
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();
            try
            {
                var presenter = new NetworkMatchHudPresenter(
                    network, network, room, rules, view);
                presenter.Start();
                Assert.That(view.PlayerItemStatuses, Has.Count.EqualTo(2));
                Assert.That(view.PlayerItemStatuses[1].IsDestroyed, Is.True);

                network.PublishPlayerItemStatuses(new[]
                {
                    new PlayerItemStatusSnapshot("Pineapple_01", false),
                });
                Assert.That(view.PlayerItemStatuses, Has.Count.EqualTo(1));
                Assert.That(view.PlayerItemStatuses[0].ItemId, Is.EqualTo("Pineapple_01"));

                presenter.Dispose();
                network.PublishPlayerItemStatuses(new[]
                {
                    new PlayerItemStatusSnapshot("Cup1_C3", true),
                });
                Assert.That(view.PlayerItemStatuses, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rules);
            }
        }

        private sealed class FakeView : INetworkMatchHudView
        {
            public MatchPhase Phase { get; private set; }
            public double RemainingSeconds { get; private set; }
            public double EndCountdown { get; private set; }
            public void SetEndCountdown(double value) => EndCountdown = value;
            public string Notice { get; private set; }
            public bool NoticeVisible { get; private set; }
            public string AssignedItem { get; private set; }
            public IReadOnlyList<PlayerItemStatusSnapshot> PlayerItemStatuses { get; private set; } =
                Array.Empty<PlayerItemStatusSnapshot>();
            public int RemainingDestructionUses { get; private set; } = -1;

            public string HidingPlayerName { get; private set; }

            public void SetPhase(MatchPhase phase, string hidingPlayerName)
            {
                Phase = phase;
                HidingPlayerName = hidingPlayerName;
            }
            public void SetRemainingSeconds(double value) => RemainingSeconds = value;
            public void SetHighlightTitle(string title) { }
            public void SetAssignedItem(string displayName) => AssignedItem = displayName;
            public void SetPlayerItemStatuses(IReadOnlyList<PlayerItemStatusSnapshot> statuses) =>
                PlayerItemStatuses = statuses == null
                    ? Array.Empty<PlayerItemStatusSnapshot>()
                    : new List<PlayerItemStatusSnapshot>(statuses);
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
            public IReadOnlyList<PlayerItemStatusSnapshot> LatestPlayerItemStatuses { get; set; } =
                Array.Empty<PlayerItemStatusSnapshot>();
            public bool IsRuntimeReady { get; set; } = true;
            private double serverTime;
            public double ServerTime
            {
                get => IsRuntimeReady ? serverTime : throw new InvalidOperationException("Runner is unavailable.");
                set => serverTime = value;
            }
            public event Action<MatchStateSnapshot> MatchStateReceived;
            public event Action<string> ItemAssignmentReceived;
            public event Action<PlayerItemDestroyedEvent> ItemDestroyedReceived;
            public event Action<IReadOnlyList<PlayerItemStatusSnapshot>> PlayerItemStatusesReceived;
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
            public void Publish(MatchResult value) => MatchResultReceived?.Invoke(value);
            public void PublishItemAssignment(string itemId) =>
                ItemAssignmentReceived?.Invoke(itemId);
            public void PublishPlayerItemStatuses(IReadOnlyList<PlayerItemStatusSnapshot> value)
            {
                LatestPlayerItemStatuses = value ?? Array.Empty<PlayerItemStatusSnapshot>();
                PlayerItemStatusesReceived?.Invoke(value);
            }
            public void Publish(PlayerItemDestroyedEvent value) =>
                ItemDestroyedReceived?.Invoke(value);
            public void Publish(IReadOnlyList<PlayerInteractionStateSnapshot> value) =>
                PlayerInteractionStatesReceived?.Invoke(value);
        }
    }
}
