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
        private IReadOnlyList<Game.Core.Items.ItemDefinition> previousCatalog;

        [SetUp]
        public void PrepareCatalog()
        {
            previousCatalog = Game.Core.Items.ItemCatalog.Definitions;
            Game.Core.Items.ItemCatalog.Configure(new[]
            {
                new Game.Core.Items.ItemDefinition("Soda_01", "food", "탄산음료"),
                new Game.Core.Items.ItemDefinition("Burger_01", "food", "햄버거")
            });
        }

        [TearDown]
        public void RestoreCatalog() => Game.Core.Items.ItemCatalog.Configure(previousCatalog);

        [TestCase(false)]
        [TestCase(true)]
        public void GameEnd_FadesOutThenCoversBeforeResult(bool phaseFirst)
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
                presenter.Tick();
                playback.Tick();
                Assert.That(view.EndHeadline, Is.Null.Or.Empty);
                Assert.That(view.EndCountdown, Is.Zero);
                Assert.That(transition.Opacity, Is.Zero);

                network.ServerTime = 100d + (HighlightPresentationTiming.FadeSeconds * 0.5d);
                presenter.Tick();
                playback.Tick();
                Assert.That(transition.Opacity, Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(view.EndHeadline, Is.Null.Or.Empty);

                network.ServerTime = 100d + HighlightPresentationTiming.FadeSeconds;
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

        [Test]
        public void ReturnToLobby_KeepsTransitionCoveredUntilLobbyTakesOwnership()
        {
            var network = new FakeNetwork();
            var transition = new FakeTransition();
            using var room = new RoomBrowserSystem();
            using var playback = new NetworkHighlightPlaybackController(network, room, network, transition);
            playback.Start();

            network.Publish(new MatchStateSnapshot(MatchPhase.Highlight, 10d));
            transition.SetOpacity(0.5f);
            network.Publish(new MatchStateSnapshot(MatchPhase.Result, 0d));
            Assert.That(transition.Opacity, Is.EqualTo(1f),
                "The live camera must not be restored under a partial fade.");
            network.Publish(new MatchStateSnapshot(MatchPhase.Waiting, 0d));
            Assert.That(transition.Opacity, Is.EqualTo(1f));

            network.Publish(new MatchStateSnapshot(MatchPhase.Hiding, 0d));
            Assert.That(transition.Opacity, Is.Zero);
        }

        [Test]
        public void HighlightSkip_AffectsOnlyTheLocalPlayback()
        {
            var firstNetwork = new FakeNetwork { ServerTime = 10d };
            var secondNetwork = new FakeNetwork { ServerTime = 10d };
            using var room = new RoomBrowserSystem();
            using var first = new NetworkHighlightPlaybackController(
                firstNetwork, room, firstNetwork, new FakeTransition());
            using var second = new NetworkHighlightPlaybackController(
                secondNetwork, room, secondNetwork, new FakeTransition());
            first.Start();
            second.Start();

            Assert.That(first.SkipCurrent(), Is.False);
            Assert.That(first.SkipAll(), Is.False);

            var replay = CreateReplay(HighlightType.FirstBlood, HighlightType.FinalMoment);
            var duration = 20d + 2d * HighlightPresentationTiming.OverheadSeconds;
            firstNetwork.Publish(new MatchResult(MatchEndReason.TimeExpired, 0d, new[] { 0 }));
            firstNetwork.PublishReplay(replay);
            firstNetwork.Publish(new MatchStateSnapshot(MatchPhase.Highlight, 10d + duration));
            secondNetwork.Publish(new MatchResult(MatchEndReason.TimeExpired, 0d, new[] { 0 }));
            secondNetwork.PublishReplay(replay);
            secondNetwork.Publish(new MatchStateSnapshot(MatchPhase.Highlight, 10d + duration));

            Assert.That(first.SkipCurrent(), Is.True);
            Assert.That(second.SkipCurrent(), Is.True,
                "One player's skip must not move another player's playback.");
            Assert.That(first.SkipAll(), Is.True);
            Assert.That(firstNetwork.CompleteHighlightCalls, Is.EqualTo(1));
            Assert.That(secondNetwork.CompleteHighlightCalls, Is.Zero,
                "One player's skip-all must not complete another player's playback.");
            Assert.That(first.SkipCurrent(), Is.False);
            Assert.That(second.SkipCurrent(), Is.True,
                "One player's skip-all must not stop another player's playback.");
            Assert.That(second.SkipAll(), Is.True);
            Assert.That(secondNetwork.CompleteHighlightCalls, Is.EqualTo(1));
            Assert.That(second.SkipCurrent(), Is.False);
        }

        [Test]
        public void SwitchingHighlight_DisposesPreviousReplayCameraBeforeSkipAll()
        {
            var network = new FakeNetwork { ServerTime = 10d };
            using var room = new RoomBrowserSystem();
            using var playback = new NetworkHighlightPlaybackController(network, room, network, new FakeTransition());
            var output = new GameObject("Replay output");
            HighlightCameraDirector director = null;
            try
            {
                output.AddComponent<Camera>();
                output.AddComponent(Type.GetType("Unity.Cinemachine.CinemachineBrain, Unity.Cinemachine"));
                playback.Start();
                network.Publish(new MatchResult(MatchEndReason.TimeExpired, 0d, new[] { 0 }));
                network.PublishReplay(CreateReplay(HighlightType.FirstBlood, HighlightType.FinalMoment));
                network.Publish(new MatchStateSnapshot(MatchPhase.Highlight,
                    30d + 2d * HighlightPresentationTiming.OverheadSeconds));
                director = new HighlightCameraDirector(output.transform, output.transform,
                    Array.Empty<Transform>(), Array.Empty<SceneWorldObjectReference>());
                var oldCamera = GameObject.Find("[Highlight Cinemachine Rig]");
                Assert.That(oldCamera, Is.Not.Null);
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic;
                var type = typeof(NetworkHighlightPlaybackController);
                type.GetField("cameraDirector", flags).SetValue(playback, director);
                type.GetField("replayPlayer", flags).SetValue(playback,
                    new HighlightReplayPlayer(Array.Empty<Transform>(), Array.Empty<SceneWorldObjectReference>()));
                type.GetField("readinessConfirmed", flags).SetValue(playback, true);

                Assert.That(playback.SkipCurrent(), Is.True);
                network.ServerTime += 0.1d; // Next rendered frame after the skip.
                playback.Tick();
                Assert.That(oldCamera == null || !oldCamera.activeInHierarchy, Is.True,
                    "The previous priority-100 camera must not survive the next highlight.");
                playback.SkipAll();
                Assert.That(GameObject.Find("[Highlight Cinemachine Rig]"), Is.Null);
            }
            finally
            {
                director?.Dispose();
                UnityEngine.Object.DestroyImmediate(output);
            }
        }

        [Test]
        public void AcknowledgedHighlight_DoesNotReclaimLobbyTransition()
        {
            var network = new FakeNetwork
            {
                ServerTime = 10d,
                CompleteHighlightResult = false,
            };
            var transition = new FakeTransition();
            using var room = new RoomBrowserSystem();
            using var playback = new NetworkHighlightPlaybackController(
                network, room, network, transition);
            playback.Start();

            network.Publish(new MatchResult(MatchEndReason.TimeExpired, 0d, new[] { 0 }));
            network.PublishReplay(CreateReplay(HighlightType.FirstBlood));
            network.Publish(new MatchStateSnapshot(MatchPhase.Highlight,
                10d + HighlightPresentationTiming.OverheadSeconds));

            Assert.That(playback.SkipAll(), Is.True);
            Assert.That(transition.Opacity, Is.EqualTo(1f));
            Assert.That(network.CompleteHighlightCalls, Is.EqualTo(1));

            // Simulate an acknowledgement arriving before LobbyLifetimeScope's
            // Update, with the playback controller ticking later that frame.
            network.CompleteHighlightResult = true;
            transition.SetOpacity(0f);
            playback.Tick();

            Assert.That(transition.Opacity, Is.Zero);
            Assert.That(network.CompleteHighlightCalls, Is.EqualTo(2));
            playback.Tick();
            Assert.That(network.CompleteHighlightCalls, Is.EqualTo(2));
        }

        [Test]
        public void EmptyHighlightReplay_ConfirmsReadinessWithoutStartingPlayback()
        {
            var network = new FakeNetwork { ServerTime = 3d };
            var transition = new FakeTransition();
            using var room = new RoomBrowserSystem();
            using var playback = new NetworkHighlightPlaybackController(
                network, room, network, transition);
            playback.Start();

            network.Publish(new MatchResult(MatchEndReason.TimeExpired, 0d, new[] { 0 }));
            network.Publish(new MatchStateSnapshot(MatchPhase.Highlight, 0d));
            network.PublishReplay(Array.Empty<HighlightReplayData>());
            playback.Tick();

            Assert.That(network.HighlightReadyCalls, Is.EqualTo(1));
            Assert.That(playback.PlaybackSourceTime, Is.Null);
            Assert.That(transition.Opacity, Is.EqualTo(1f));
        }

        private static HighlightReplayData[] CreateReplay(params HighlightType[] types)
        {
            var result = new HighlightReplayData[types.Length];
            for (var index = 0; index < types.Length; index++)
            {
                var segment = new HighlightSegment(0d, 10d);
                result[index] = new HighlightReplayData(
                    new HighlightCandidate(types[index], new[] { segment }, $"item-{index}"),
                    new[]
                    {
                        new HighlightReplayClip(segment, Array.Empty<HighlightReplayFrame>())
                    });
            }

            return result;
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
            presenter.Tick();
            Assert.That(view.RemainingSeconds, Is.EqualTo(20d));

            network.IsRuntimeReady = false;
            network.ServerTime = 75d;
            Assert.DoesNotThrow(() => presenter.Tick());
            Assert.That(view.HidingPlayerName, Is.EqualTo("방장"));
            network.IsRuntimeReady = true;

            network.ServerTime = 75d;
            presenter.Tick();
            Assert.That(view.HidingPlayerName, Is.EqualTo("민수"));
            Assert.That(view.RemainingSeconds, Is.EqualTo(25d));

            // 단계가 마지막 턴보다 길어져도 없는 사람을 부르지 않는다.
            network.ServerTime = 130d;
            presenter.Tick();
            Assert.That(view.HidingPlayerName, Is.EqualTo("민수"));

            // 숨기기가 아닌 단계는 아무도 지목하지 않는다.
            network.Publish(new MatchStateSnapshot(MatchPhase.Searching, 200d));
            Assert.That(view.HidingPlayerName, Is.Empty);
        }

        [Test]
        public void HidingTimer_ResetsForEachTurnUsingConfiguredDuration()
        {
            Assert.That(
                MatchRuleSettings.TryCreate(60, 5, 1f, 3, null, out var matchRules, out _),
                Is.True);
            var network = new FakeNetwork { MatchRules = matchRules };
            var view = new FakeView();
            using var room = new RoomBrowserSystem();
            room.MatchStarted(new[]
            {
                new MatchParticipant("host", 0),
                new MatchParticipant("client", 1),
            });
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();
            try
            {
                using var presenter = new NetworkMatchHudPresenter(
                    network, network, room, rules, view);
                presenter.Start();

                network.ServerTime = 10d;
                network.Publish(new MatchStateSnapshot(MatchPhase.Hiding, 120d));
                presenter.Tick();
                Assert.That(view.RemainingSeconds, Is.EqualTo(50d));

                network.ServerTime = 60d;
                presenter.Tick();
                Assert.That(view.RemainingSeconds, Is.EqualTo(60d));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rules);
            }
        }

        [TestCase(MatchPhase.Hiding, 60d, 30d)]
        [TestCase(MatchPhase.Searching, 300d, 300d)]
        public void Intro_ReportsOnlyAfterPresentationAndWaitsForSharedDeadline(MatchPhase phase, double duration, double displayedTime)
        {
            var network = new FakeNetwork { ServerTime = 100d };
            var view = new FakeView { IntroPresented = false };
            using var room = new RoomBrowserSystem();
            room.MatchStarted(new[] { new MatchParticipant("host", 0), new MatchParticipant("client", 1) });
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();
            try
            {
                using var presenter = new NetworkMatchHudPresenter(network, network, room, rules, view);
                presenter.Start();
                network.Publish(new MatchStateSnapshot(phase, 0d));
                presenter.Tick();
                Assert.That(network.IntroReadyReports, Is.Empty);
                network.ServerTime = 120d;
                network.PublishItemAssignment("Soda_01");
                presenter.Tick();
                Assert.That(network.IntroReadyReports, Is.Empty, "Showing a view is not proof that it was presented.");
                view.IntroPresented = true;
                presenter.Tick();
                presenter.Tick();
                Assert.That(network.IntroReadyReports, Is.EqualTo(new[] { phase }));
                network.ServerTime = 150d;
                presenter.Tick();
                Assert.That(view.IsPhaseIntroPresented(phase), Is.True);
                Assert.That(presenter.BlocksGameplayInput, Is.True);
                Assert.That(view.RemainingSeconds, Is.EqualTo(displayedTime));
                network.Publish(new MatchStateSnapshot(phase, 153d + duration));
                network.ServerTime = 152.9d;
                presenter.Tick();
                Assert.That(view.IsPhaseIntroPresented(phase), Is.True);
                Assert.That(presenter.BlocksGameplayInput, Is.True);
                network.ServerTime = 153d;
                presenter.Tick();
                Assert.That(view.IsPhaseIntroPresented(phase), Is.False);
                Assert.That(presenter.BlocksGameplayInput, Is.False);
                Assert.That(view.RemainingSeconds, Is.EqualTo(displayedTime));
                network.ServerTime = 154d;
                presenter.Tick();
                Assert.That(view.RemainingSeconds, Is.EqualTo(displayedTime - 1d));
            }
            finally { UnityEngine.Object.DestroyImmediate(rules); }
        }

        [Test]
        public void HidingStart_ShowsEachPlayersAssignedItemThenHides()
        {
            var network = new FakeNetwork { ServerTime = 40d };
            var view = new FakeView();
            using var room = new RoomBrowserSystem();
            room.MatchStarted(new[]
            {
                new MatchParticipant("host", 0),
                new MatchParticipant("client", 1),
            });
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();
            try
            {
                using var presenter = new NetworkMatchHudPresenter(
                    network, network, room, rules, view);
                presenter.Start();

                network.PublishItemAssignment("Soda_01");
                network.Publish(new MatchStateSnapshot(MatchPhase.Hiding, 103d));
                presenter.Tick();
                Assert.That(view.HidingIntroVisible, Is.True);
                Assert.That(view.HidingIntroItem, Is.EqualTo("탄산음료"));
                Assert.That(view.RemainingSeconds, Is.EqualTo(30d));

                network.ServerTime = 42.9d;
                presenter.Tick();
                Assert.That(view.HidingIntroVisible, Is.True);

                network.ServerTime = 43d;
                presenter.Tick();
                Assert.That(view.HidingIntroVisible, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rules);
            }
        }

        [Test]
        public void HidingIntro_WaitsForAssignmentInsideTheOpeningWindow()
        {
            var network = new FakeNetwork { ServerTime = 41d };
            var view = new FakeView();
            using var room = new RoomBrowserSystem();
            room.MatchStarted(new[]
            {
                new MatchParticipant("host", 0),
                new MatchParticipant("client", 1),
            });
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();
            try
            {
                using var presenter = new NetworkMatchHudPresenter(
                    network, network, room, rules, view);
                presenter.Start();

                network.Publish(new MatchStateSnapshot(MatchPhase.Hiding, 103d));
                presenter.Tick();
                Assert.That(view.HidingIntroVisible, Is.False);

                network.PublishItemAssignment("Burger_01");
                presenter.Tick();
                Assert.That(view.HidingIntroVisible, Is.True);
                Assert.That(view.HidingIntroItem, Is.EqualTo("햄버거"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rules);
            }
        }

        [Test]
        public void HidingIntro_DoesNotOpenAfterTheOpeningWindow()
        {
            var network = new FakeNetwork { ServerTime = 44d };
            var view = new FakeView();
            using var room = new RoomBrowserSystem();
            room.MatchStarted(new[]
            {
                new MatchParticipant("host", 0),
                new MatchParticipant("client", 1),
            });
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();
            try
            {
                using var presenter = new NetworkMatchHudPresenter(
                    network, network, room, rules, view);
                presenter.Start();

                network.PublishItemAssignment("Soda_01");
                network.Publish(new MatchStateSnapshot(MatchPhase.Hiding, 103d));
                presenter.Tick();
                Assert.That(view.HidingIntroVisible, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rules);
            }
        }

        [Test]
        public void HidingTurnStart_ShowsForLocalPlayerThenRevealsTopHud()
        {
            var network = new FakeNetwork { ServerTime = 70d };
            var view = new FakeView();
            using var room = new RoomBrowserSystem();
            room.MatchStarted(new[]
            {
                new MatchParticipant("host", 0),
                new MatchParticipant("client", 1),
            });
            room.SetLocalPlayer("client");
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();
            try
            {
                using var presenter = new NetworkMatchHudPresenter(
                    network, network, room, rules, view);
                presenter.Start();

                network.Publish(new MatchStateSnapshot(MatchPhase.Hiding, 100d));
                presenter.Tick();
                Assert.That(view.HidingTurnStartVisible, Is.True);
                Assert.That(view.HidingWaitHudVisible, Is.False);
                Assert.That(view.TopHudVisible, Is.False);
                Assert.That(view.MatchChatVisible, Is.False);
                Assert.That(view.PlayerStatusVisible, Is.False);
                Assert.That(view.HidingTurnStartSeconds, Is.EqualTo(30d));
                Assert.That(view.HidingTurnStartBanner, Is.EqualTo(HidingTurnStartView.BannerText));

                network.ServerTime = 70.9d;
                presenter.Tick();
                Assert.That(view.HidingTurnStartVisible, Is.True);
                Assert.That(view.TopHudVisible, Is.False);

                network.ServerTime = 71d;
                presenter.Tick();
                Assert.That(view.HidingTurnStartVisible, Is.False);
                Assert.That(view.HidingActiveHudVisible, Is.True);
                Assert.That(view.HidingActiveTopPromptVisible, Is.True);
                Assert.That(view.HidingCompleteGuideVisible, Is.True);
                Assert.That(view.TopHudVisible, Is.False);
                Assert.That(view.MatchChatVisible, Is.False);
                Assert.That(view.PlayerStatusVisible, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rules);
            }
        }

        [Test]
        public void HidingTurnStart_ShowsAfterIntroForFirstPlayer()
        {
            var network = new FakeNetwork { ServerTime = 43d };
            var view = new FakeView();
            using var room = new RoomBrowserSystem();
            room.MatchStarted(new[]
            {
                new MatchParticipant("host", 0),
                new MatchParticipant("client", 1),
            });
            room.SetLocalPlayer("host");
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();
            try
            {
                using var presenter = new NetworkMatchHudPresenter(
                    network, network, room, rules, view);
                presenter.Start();

                network.Publish(new MatchStateSnapshot(MatchPhase.Hiding, 103d));
                presenter.Tick();
                Assert.That(view.HidingTurnStartVisible, Is.True);
                Assert.That(view.TopHudVisible, Is.False);

                network.ServerTime = 44d;
                presenter.Tick();
                Assert.That(view.HidingTurnStartVisible, Is.False);
                Assert.That(view.HidingActiveHudVisible, Is.True);
                Assert.That(view.HidingActiveTopPromptVisible, Is.True);
                Assert.That(view.TopHudVisible, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rules);
            }
        }

        [Test]
        public void HidingWaitHud_ShowsOrderAndChatForWaitingPlayer()
        {
            var network = new FakeNetwork { ServerTime = 50d };
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
            try
            {
                using var presenter = new NetworkMatchHudPresenter(
                    network, network, room, rules, view);
                presenter.Start();

                network.Publish(new MatchStateSnapshot(MatchPhase.Hiding, 100d));
                presenter.Tick();
                Assert.That(view.HidingWaitHudVisible, Is.True);
                Assert.That(view.HidingWaitCompleted, Is.EqualTo(1));
                Assert.That(view.HidingWaitTotal, Is.EqualTo(2));
                Assert.That(view.HidingWaitName, Is.EqualTo("방장"));
                Assert.That(view.HidingWaitPlayers.Count, Is.EqualTo(2));
                Assert.That(view.HidingWaitPlayers[0].Current, Is.True);
                Assert.That(view.HidingWaitPlayers[1].Completed, Is.False);
                Assert.That(view.HidingWaitNextTurn, Is.True);
                Assert.That(view.HidingWaitRemaining, Is.EqualTo(20d).Within(0.001d));
                Assert.That(view.MatchChatVisible, Is.True);
                Assert.That(view.MatchChatMode, Is.EqualTo(MatchChatHudMode.Full));
                Assert.That(view.HidingActiveTopPromptVisible, Is.False);
                Assert.That(view.TopHudVisible, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rules);
            }
        }

        [Test]
        public void HidingTurnStart_DoesNotShowForOtherPlayers()
        {
            var network = new FakeNetwork { ServerTime = 70d };
            var view = new FakeView();
            using var room = new RoomBrowserSystem();
            room.MatchStarted(new[]
            {
                new MatchParticipant("host", 0),
                new MatchParticipant("client", 1),
            });
            room.SetLocalPlayer("host");
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();
            try
            {
                using var presenter = new NetworkMatchHudPresenter(
                    network, network, room, rules, view);
                presenter.Start();

                network.Publish(new MatchStateSnapshot(MatchPhase.Hiding, 100d));
                presenter.Tick();
                Assert.That(view.HidingTurnStartVisible, Is.False);
                Assert.That(view.HidingActiveHudVisible, Is.True);
                Assert.That(view.HidingCompleteGuideVisible, Is.False);
                Assert.That(view.HidingActiveTopPromptVisible, Is.False);
                Assert.That(view.HidingWaitHudVisible, Is.True);
                Assert.That(view.HidingWaitCompleted, Is.EqualTo(2));
                Assert.That(view.HidingWaitTotal, Is.EqualTo(2));
                Assert.That(view.HidingWaitNextTurn, Is.False);
                Assert.That(view.TopHudVisible, Is.False);
                Assert.That(view.MatchChatVisible, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rules);
            }
        }

        [Test]
        public void SearchingStart_ShowsEachPlayersAssignedItemThenHides()
        {
            var network = new FakeNetwork { ServerTime = 100d };
            var view = new FakeView();
            using var room = new RoomBrowserSystem();
            room.MatchStarted(new[]
            {
                new MatchParticipant("host", 0),
                new MatchParticipant("client", 1),
            });
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();
            try
            {
                using var presenter = new NetworkMatchHudPresenter(
                    network, network, room, rules, view);
                presenter.Start();

                network.PublishItemAssignment("Soda_01");
                network.Publish(new MatchStateSnapshot(MatchPhase.Searching, 403d));
                presenter.Tick();
                Assert.That(view.SearchingIntroVisible, Is.True);
                Assert.That(view.SearchingIntroItem, Is.EqualTo("탄산음료"));
                Assert.That(view.RemainingSeconds, Is.EqualTo(300d));
                Assert.That(view.HidingTurnStartVisible, Is.False);

                network.ServerTime = 102.9d;
                presenter.Tick();
                Assert.That(view.SearchingIntroVisible, Is.True);

                network.ServerTime = 103d;
                presenter.Tick();
                Assert.That(view.SearchingIntroVisible, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rules);
            }
        }

        [Test]
        public void SearchingIntro_WaitsForAssignmentInsideTheOpeningWindow()
        {
            var network = new FakeNetwork { ServerTime = 101d };
            var view = new FakeView();
            using var room = new RoomBrowserSystem();
            room.MatchStarted(new[]
            {
                new MatchParticipant("host", 0),
                new MatchParticipant("client", 1),
            });
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();
            try
            {
                using var presenter = new NetworkMatchHudPresenter(
                    network, network, room, rules, view);
                presenter.Start();

                network.Publish(new MatchStateSnapshot(MatchPhase.Searching, 403d));
                presenter.Tick();
                Assert.That(view.SearchingIntroVisible, Is.False);

                network.PublishItemAssignment("Burger_01");
                presenter.Tick();
                Assert.That(view.SearchingIntroVisible, Is.True);
                Assert.That(view.SearchingIntroItem, Is.EqualTo("햄버거"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rules);
            }
        }

        [Test]
        public void SearchingIntro_DoesNotOpenAfterTheOpeningWindow()
        {
            var network = new FakeNetwork { ServerTime = 104d };
            var view = new FakeView();
            using var room = new RoomBrowserSystem();
            room.MatchStarted(new[]
            {
                new MatchParticipant("host", 0),
                new MatchParticipant("client", 1),
            });
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();
            try
            {
                using var presenter = new NetworkMatchHudPresenter(
                    network, network, room, rules, view);
                presenter.Start();

                network.PublishItemAssignment("Soda_01");
                network.Publish(new MatchStateSnapshot(MatchPhase.Searching, 403d));
                presenter.Tick();
                Assert.That(view.SearchingIntroVisible, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rules);
            }
        }

        [Test]
        public void SearchingFinalWarning_ReusesHidingTurnStartOverlay()
        {
            var network = new FakeNetwork { ServerTime = 370d };
            var view = new FakeView();
            using var room = new RoomBrowserSystem();
            room.MatchStarted(new[]
            {
                new MatchParticipant("host", 0),
                new MatchParticipant("client", 1),
            });
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();
            try
            {
                using var presenter = new NetworkMatchHudPresenter(
                    network, network, room, rules, view);
                presenter.Start();

                network.Publish(new MatchStateSnapshot(MatchPhase.Searching, 400d));
                presenter.Tick();
                Assert.That(view.HidingTurnStartVisible, Is.True);
                Assert.That(view.HidingTurnStartSeconds, Is.EqualTo(30d));
                Assert.That(
                    view.HidingTurnStartBanner,
                    Is.EqualTo(HidingTurnStartView.FinalWarningBannerText));
                Assert.That(view.TopHudVisible, Is.False);

                network.ServerTime = 370.9d;
                presenter.Tick();
                Assert.That(view.HidingTurnStartVisible, Is.True);
                Assert.That(view.HidingTurnStartSeconds, Is.EqualTo(29.1d).Within(0.001d));

                network.ServerTime = 371d;
                presenter.Tick();
                Assert.That(view.HidingTurnStartVisible, Is.False);
                Assert.That(view.TopHudVisible, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rules);
            }
        }

        [Test]
        public void SearchingFinalWarning_DoesNotOpenAfterTheOpeningWindow()
        {
            var network = new FakeNetwork { ServerTime = 372d };
            var view = new FakeView();
            using var room = new RoomBrowserSystem();
            room.MatchStarted(new[]
            {
                new MatchParticipant("host", 0),
                new MatchParticipant("client", 1),
            });
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();
            try
            {
                using var presenter = new NetworkMatchHudPresenter(
                    network, network, room, rules, view);
                presenter.Start();

                network.Publish(new MatchStateSnapshot(MatchPhase.Searching, 400d));
                presenter.Tick();
                Assert.That(view.HidingTurnStartVisible, Is.False);
                Assert.That(view.TopHudVisible, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rules);
            }
        }

        [Test]
        public void Searching_RefreshesStaminaBarFromLocalPlayer()
        {
            var network = new FakeNetwork
            {
                ServerTime = 100d,
                HasLocalStamina = true,
                LocalStamina = 100f,
                LocalMaxStamina = 100f,
            };
            var view = new FakeView();
            using var room = new RoomBrowserSystem();
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();
            try
            {
                using var presenter = new NetworkMatchHudPresenter(
                    network, network, room, rules, view);
                presenter.Start();
                Assert.That(view.VitalsVisible, Is.False);

                network.Publish(new MatchStateSnapshot(MatchPhase.Searching, 460d));
                Assert.That(view.VitalsVisible, Is.True);
                Assert.That(view.VitalsStamina, Is.EqualTo(100f));
                Assert.That(view.VitalsMaxStamina, Is.EqualTo(100f));
                Assert.That(view.VitalsHits, Is.EqualTo(3));
                Assert.That(view.VitalsMaxHits, Is.EqualTo(3));

                network.LocalStamina = 40f;
                presenter.Tick();
                Assert.That(view.VitalsStamina, Is.EqualTo(40f));

                network.LocalStamina = 75f;
                presenter.Tick();
                Assert.That(view.VitalsStamina, Is.EqualTo(75f));
                Assert.That(view.VitalsExhausted, Is.False);

                network.LocalStamina = 12f;
                network.LocalStaminaExhausted = true;
                presenter.Tick();
                Assert.That(view.VitalsExhausted, Is.True);

                network.Publish(new MatchStateSnapshot(MatchPhase.Hiding, 520d));
                Assert.That(view.VitalsVisible, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rules);
            }
        }

        [Test]
        public void Searching_RefreshesHitBarFromLocalHitCount()
        {
            var network = new FakeNetwork
            {
                ServerTime = 100d,
                HasLocalStamina = true,
                LocalStamina = 100f,
                LocalMaxStamina = 100f,
            };
            var view = new FakeView();
            using var room = new RoomBrowserSystem();
            room.MatchStarted(new[]
            {
                new MatchParticipant("host", 0),
                new MatchParticipant("client", 1),
            });
            room.SetLocalPlayer("client");
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();
            try
            {
                using var presenter = new NetworkMatchHudPresenter(
                    network, network, room, rules, view);
                presenter.Start();
                network.Publish(new MatchStateSnapshot(MatchPhase.Searching, 460d));
                Assert.That(view.VitalsHits, Is.EqualTo(3));
                Assert.That(view.VitalsMaxHits, Is.EqualTo(3));

                network.Publish(new[]
                {
                    new PlayerInteractionStateSnapshot(0, 0d, 5, 0),
                    new PlayerInteractionStateSnapshot(1, 0d, 5, 1),
                });
                Assert.That(view.VitalsHits, Is.EqualTo(2));
                Assert.That(view.VitalsMaxHits, Is.EqualTo(3));

                network.Publish(new[]
                {
                    new PlayerInteractionStateSnapshot(0, 0d, 5, 0),
                    new PlayerInteractionStateSnapshot(1, 0d, 5, 2),
                });
                Assert.That(view.VitalsHits, Is.EqualTo(1));

                network.Publish(new[]
                {
                    new PlayerInteractionStateSnapshot(0, 0d, 5, 0),
                    new PlayerInteractionStateSnapshot(1, 202d, 5, 0),
                });
                Assert.That(view.VitalsHits, Is.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rules);
            }
        }

        [Test]
        public void MatchChat_ReturnsWhenSearchingStarts()
        {
            var network = new FakeNetwork { ServerTime = 100d };
            var view = new FakeView();
            using var room = new RoomBrowserSystem();
            room.MatchStarted(new[]
            {
                new MatchParticipant("host", 0),
                new MatchParticipant("client", 1),
            });
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();
            try
            {
                using var presenter = new NetworkMatchHudPresenter(
                    network, network, room, rules, view);
                presenter.Start();
                Assert.That(view.MatchChatVisible, Is.False);
                Assert.That(view.PlayerStatusVisible, Is.False);

                network.Publish(new MatchStateSnapshot(MatchPhase.Searching, 460d));
                presenter.Tick();
                Assert.That(view.MatchChatVisible, Is.True);
                Assert.That(view.MatchChatMode, Is.EqualTo(MatchChatHudMode.Searching));
                Assert.That(view.PlayerStatusVisible, Is.True);
                Assert.That(view.DestroyedItemPlayerCount, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rules);
            }
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

        [Test]
        public void DestroyedItems_PassLocalAssignmentAndDestructionOrder()
        {
            var network = new FakeNetwork();
            var view = new FakeView();
            using var room = new RoomBrowserSystem();
            room.MatchStarted(new[]
            {
                new MatchParticipant("host", 0),
                new MatchParticipant("client", 1),
                new MatchParticipant("guest", 2),
            });
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();
            try
            {
                using var presenter = new NetworkMatchHudPresenter(
                    network, network, room, rules, view);
                presenter.Start();
                network.PublishItemAssignment("Soda_01");
                network.Publish(new PlayerItemDestroyedEvent(1, "Burger_01", 10d));
                network.Publish(new PlayerItemDestroyedEvent(2, "Pineapple_01", 11d));
                network.PublishPlayerItemStatuses(new[]
                {
                    new PlayerItemStatusSnapshot("Soda_01", false),
                    new PlayerItemStatusSnapshot("Burger_01", true),
                    new PlayerItemStatusSnapshot("Pineapple_01", true),
                });

                Assert.That(view.DestroyedItemPlayerCount, Is.EqualTo(3));
                Assert.That(view.DestroyedLocalItemId, Is.EqualTo("Soda_01"));
                Assert.That(view.DestroyedItemOrder, Is.EqualTo(new[]
                {
                    "Burger_01",
                    "Pineapple_01",
                }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rules);
            }
        }

        private sealed class FakeView : INetworkMatchHudView
        {
            public bool IntroPresented { get; set; } = true;
            public bool IsPhaseIntroPresented(MatchPhase phase) => IntroPresented &&
                (phase == MatchPhase.Hiding ? HidingIntroVisible : phase == MatchPhase.Searching && SearchingIntroVisible);
            public MatchPhase Phase { get; private set; }
            public double RemainingSeconds { get; private set; }
            public double EndCountdown { get; private set; }
            public string EndHeadline { get; private set; }
            public string EndSubtitle { get; private set; }

            public void SetEndCountdown(double value)
            {
                EndCountdown = value;
                if (value <= 0d)
                {
                    EndHeadline = null;
                    EndSubtitle = null;
                }
            }

            public void SetEndResult(string headline, string subtitle)
            {
                EndHeadline = headline;
                EndSubtitle = subtitle;
            }
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
            public void SetHighlightHud(bool visible, string subtitle, IReadOnlyList<float> barFills) { }
            public void SetAssignedItem(string displayName) => AssignedItem = displayName;
            public int DestroyedItemPlayerCount { get; private set; }

            public void SetPlayerItemStatuses(IReadOnlyList<PlayerItemStatusSnapshot> statuses) =>
                SetDestroyedItems(statuses == null ? 0 : statuses.Count, statuses);

            public string DestroyedLocalItemId { get; private set; }
            public IReadOnlyList<string> DestroyedItemOrder { get; private set; } =
                Array.Empty<string>();

            public void SetDestroyedItems(
                int playerCount,
                IReadOnlyList<PlayerItemStatusSnapshot> statuses,
                string localItemId = null,
                IReadOnlyList<string> destroyedItemIdsInOrder = null)
            {
                DestroyedItemPlayerCount = playerCount;
                PlayerItemStatuses = statuses == null
                    ? Array.Empty<PlayerItemStatusSnapshot>()
                    : new List<PlayerItemStatusSnapshot>(statuses);
                DestroyedLocalItemId = localItemId;
                DestroyedItemOrder = destroyedItemIdsInOrder == null
                    ? Array.Empty<string>()
                    : new List<string>(destroyedItemIdsInOrder);
            }
            public void SetRemainingDestructionUses(int value) =>
                RemainingDestructionUses = value;

            public void ShowDestructionNotice(string message)
            {
                Notice = message;
                NoticeVisible = true;
            }

            public void HideDestructionNotice() => NoticeVisible = false;
            public void SetShredderMarker(Vector2 screenPosition, bool visible) { }
            public string HidingIntroItem { get; private set; }
            public bool HidingIntroVisible { get; private set; }

            public void ShowHidingIntro(string itemDisplayName, string itemId)
            {
                HidingIntroItem = itemDisplayName;
                HidingIntroVisible = true;
            }

            public void HideHidingIntro() => HidingIntroVisible = false;
            public string SearchingIntroItem { get; private set; }
            public bool SearchingIntroVisible { get; private set; }

            public void ShowSearchingIntro(string itemDisplayName)
            {
                SearchingIntroItem = itemDisplayName;
                SearchingIntroVisible = true;
            }

            public void HideSearchingIntro() => SearchingIntroVisible = false;
            public bool HidingTurnStartVisible { get; private set; }
            public double HidingTurnStartSeconds { get; private set; }
            public bool TopHudVisible { get; private set; } = true;
            public bool MatchChatVisible { get; private set; } = true;

            public string HidingTurnStartBanner { get; private set; }

            public void ShowHidingTurnStart(double remainingSeconds, string bannerText = null)
            {
                HidingTurnStartVisible = true;
                HidingTurnStartSeconds = remainingSeconds;
                HidingTurnStartBanner = string.IsNullOrWhiteSpace(bannerText)
                    ? HidingTurnStartView.BannerText
                    : bannerText;
                TopHudVisible = false;
            }

            public void HideHidingTurnStart()
            {
                HidingTurnStartVisible = false;
            }

            public void SetHidingTurnStartSeconds(double remainingSeconds) =>
                HidingTurnStartSeconds = remainingSeconds;

            public bool HidingActiveHudVisible { get; private set; }
            public bool HidingActiveTopPromptVisible { get; private set; }
            public bool HidingCompleteGuideVisible { get; private set; }

            public void ShowHidingActiveHud(
                double remainingSeconds,
                bool showTopPrompt,
                bool showCompleteGuide)
            {
                HidingActiveHudVisible = true;
                HidingActiveTopPromptVisible = showTopPrompt;
                HidingCompleteGuideVisible = showCompleteGuide;
                HidingTurnStartSeconds = remainingSeconds;
            }

            public void HideHidingActiveHud()
            {
                HidingActiveHudVisible = false;
                HidingActiveTopPromptVisible = false;
                HidingCompleteGuideVisible = false;
            }

            public void SetHidingActiveHudSeconds(double remainingSeconds) =>
                HidingTurnStartSeconds = remainingSeconds;

            public bool HidingWaitHudVisible { get; private set; }
            public int HidingWaitCompleted { get; private set; }
            public int HidingWaitTotal { get; private set; }
            public string HidingWaitName { get; private set; }
            public bool HidingWaitNextTurn { get; private set; }
            public double HidingWaitRemaining { get; private set; }
            public IReadOnlyList<HidingWaitPlayer> HidingWaitPlayers { get; private set; } =
                Array.Empty<HidingWaitPlayer>();

            public void ShowHidingWaitHud(
                int completedCount,
                int totalCount,
                string hidingPlayerName,
                IReadOnlyList<HidingWaitPlayer> players,
                bool showNextTurnNotice,
                double remainingSeconds,
                double turnDurationSeconds)
            {
                HidingWaitHudVisible = true;
                HidingWaitCompleted = completedCount;
                HidingWaitTotal = totalCount;
                HidingWaitName = hidingPlayerName;
                HidingWaitNextTurn = showNextTurnNotice;
                HidingWaitRemaining = remainingSeconds;
                HidingWaitPlayers = players ?? Array.Empty<HidingWaitPlayer>();
            }

            public void HideHidingWaitHud()
            {
                HidingWaitHudVisible = false;
                HidingWaitNextTurn = false;
            }

            public bool VitalsVisible { get; private set; }
            public float VitalsStamina { get; private set; }
            public float VitalsMaxStamina { get; private set; }
            public int VitalsHits { get; private set; }
            public int VitalsMaxHits { get; private set; }
            public bool VitalsExhausted { get; private set; }

            public void ShowVitals(float stamina, float maxStamina, int hits, int maxHits, bool exhausted)
            {
                VitalsVisible = true;
                VitalsStamina = stamina;
                VitalsMaxStamina = maxStamina;
                VitalsHits = hits;
                VitalsMaxHits = maxHits;
                VitalsExhausted = exhausted;
            }

            public void HideVitals() => VitalsVisible = false;

            public void SetTopHudVisible(bool visible) => TopHudVisible = visible;

            public void SetMatchChatVisible(bool visible) =>
                SetMatchChatMode(visible ? MatchChatHudMode.Full : MatchChatHudMode.Hidden);

            public MatchChatHudMode MatchChatMode { get; private set; }

            public void SetMatchChatMode(MatchChatHudMode mode)
            {
                MatchChatMode = mode;
                MatchChatVisible = mode != MatchChatHudMode.Hidden;
            }
            public bool PlayerStatusVisible { get; private set; } = true;
            public void SetPlayerStatusVisible(bool visible) => PlayerStatusVisible = visible;
        }

        private sealed class FakeNetwork :
            INetworkMatchEvents,
            INetworkMatchRuntimeSource,
            INetworkResultNavigation,
            INetworkHighlightReady,
            INetworkPhaseIntroReady
        {
            public List<MatchPhase> IntroReadyReports { get; } = new();
            public bool TryConfirmPhaseIntroReady(MatchPhase phase) { IntroReadyReports.Add(phase); return true; }
            public IReadOnlyList<PlayerItemStatusSnapshot> LatestPlayerItemStatuses { get; set; } =
                Array.Empty<PlayerItemStatusSnapshot>();
            public bool IsRuntimeReady { get; set; } = true;
            public bool IsServer { get; set; }
            public bool IsResultSceneLoaded { get; set; }
            public bool CompleteHighlightResult { get; set; } = true;
            public int CompleteHighlightCalls { get; private set; }
            public int HighlightReadyCalls { get; private set; }
            public MatchRuleSettings MatchRules { get; set; } = MatchRuleSettings.Default;
            private double serverTime;
            public double ServerTime
            {
                get => IsRuntimeReady ? serverTime : throw new InvalidOperationException("Runner is unavailable.");
                set => serverTime = value;
            }
            public event Action<MatchStateSnapshot> MatchStateReceived;
            public event Action<Game.Core.Lobby.LobbyChatMessage> MatchChatReceived;
            public event Action<string> ItemAssignmentReceived;
            public event Action<PlayerItemDestroyedEvent> ItemDestroyedReceived;
            public event Action<IReadOnlyList<PlayerItemStatusSnapshot>> PlayerItemStatusesReceived;
            public event Action<IReadOnlyList<PlayerInteractionStateSnapshot>>
                PlayerInteractionStatesReceived;
            public event Action<IReadOnlyList<HighlightReplayData>> HighlightReplayReceived;
            public event Action<MatchResult> MatchResultReceived;
            public float LocalStamina { get; set; } = MatchVitalsHudView.DefaultStamina;
            public float LocalMaxStamina { get; set; } = MatchVitalsHudView.DefaultStamina;
            public bool LocalStaminaExhausted { get; set; }
            public bool HasLocalStamina { get; set; }

            public bool TryGetPlayerPose(string playerId, out Pose pose)
            {
                pose = default;
                return false;
            }

            public bool TryGetLocalStamina(out float current, out float max, out bool exhausted)
            {
                current = LocalStamina;
                max = LocalMaxStamina;
                exhausted = LocalStaminaExhausted;
                return HasLocalStamina;
            }
            public bool EnterResultScene() => true;
            public bool PrepareLobbyForHighlights() => true;
            public bool TryConfirmHighlightReady()
            {
                HighlightReadyCalls++;
                return true;
            }
            public bool CompleteLocalHighlightViewing()
            {
                CompleteHighlightCalls++;
                return CompleteHighlightResult;
            }
            public bool RequestReturnToLobby() => true;

            public void Publish(MatchStateSnapshot value) => MatchStateReceived?.Invoke(value);
            public void Publish(MatchResult value) => MatchResultReceived?.Invoke(value);
            public void PublishReplay(IReadOnlyList<HighlightReplayData> value) =>
                HighlightReplayReceived?.Invoke(value);
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
