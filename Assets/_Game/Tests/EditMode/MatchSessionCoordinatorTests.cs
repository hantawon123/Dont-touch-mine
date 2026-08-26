using System.Collections.Generic;
using Game.Bootstrap;
using Game.Core.Flow;
using Game.Core.Items;
using Game.Core.Lobby;
using Game.Core.Match;
using Game.Core.Rooms;
using Game.Server.Items;
using Game.Server.Match;
using Game.Server.Players;
using Game.SOAP.Config;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class MatchSessionCoordinatorTests
    {
        private MatchRulesSO rules;
        private MatchState state;
        private MatchSessionCoordinator session;
        private Vector3[] lastKnownPositions;

        [SetUp]
        public void SetUp()
        {
            rules = ScriptableObject.CreateInstance<MatchRulesSO>();
            state = new MatchState();
            var playerIds = CreatePlayerIds();
            var flow = new MatchFlow(rules, state, playerIds.Length);
            var interactions = new PlayerInteractionSystem(rules, playerIds.Length);
            session = new MatchSessionCoordinator(
                rules,
                state,
                flow,
                interactions,
                playerIds,
                new TestPlacementValidator(),
                CreateSpawnPoints(),
                CreateItemDefinitions(),
                new System.Random(1234),
                new[]
                {
                    new WorldObjectState("shelf", new Pose(Vector3.zero, Quaternion.identity))
                });
            lastKnownPositions = new Vector3[MatchRulesSO.PlayerCount];
            for (var playerIndex = 0; playerIndex < lastKnownPositions.Length; playerIndex++)
            {
                lastKnownPositions[playerIndex] = new Vector3(playerIndex, 0f, 0f);
            }
        }

        [TearDown]
        public void TearDown()
        {
            state.Dispose();
            Object.DestroyImmediate(rules);
        }

        [Test]
        public void AdvanceTime_FinalizesEachHidingTurnAndStartsSearching()
        {
            session.Start(10d);
            var manualPose = new Pose(new Vector3(9f, 1f, 2f), Quaternion.identity);

            Assert.That(session.TryRecordItemPlacement(1, manualPose, 20d), Is.False);
            Assert.That(session.TryRecordItemPlacement(0, manualPose, 20d), Is.True);

            session.AdvanceTime(40d, lastKnownPositions);
            Assert.That(session.TryGetItemPlacement(0, out var firstPlacement), Is.True);
            Assert.That(firstPlacement.Pose.position, Is.EqualTo(manualPose.position));
            Assert.That(firstPlacement.WasAutoPlaced, Is.False);

            session.AdvanceTime(190d, lastKnownPositions);
            Assert.That(session.AllItemsPlaced, Is.True);
            Assert.That(state.CurrentPhase.CurrentValue, Is.EqualTo(MatchPhase.Searching));
            Assert.That(session.TryGetItemPlacement(1, out var secondPlacement), Is.True);
            Assert.That(secondPlacement.Pose.position, Is.EqualTo(lastKnownPositions[1]));
            Assert.That(secondPlacement.WasAutoPlaced, Is.True);
        }

        [Test]
        public void HidingPlayer_CanMovePlayerItemsAndMapObjects()
        {
            session.Start(10d);
            var itemId = session.Assignments[0].Item.ItemId;
            var otherItemId = session.Assignments[1].Item.ItemId;
            var firstPose = new Pose(new Vector3(2f, 0f, 1f), Quaternion.identity);
            var invalidPose = new Pose(new Vector3(-1f, 0f, 0f), Quaternion.identity);
            var replacedPose = new Pose(new Vector3(5f, 0f, 3f), Quaternion.identity);

            Assert.That(session.TryRecordItemPlacement(0, firstPose, 20d), Is.True);
            Assert.That(session.TryHoldObject(0, itemId, 20d), Is.True);
            Assert.That(session.TryGetItemPlacement(0, out var previousPlacement), Is.True);
            Assert.That(previousPlacement.Pose.position, Is.EqualTo(firstPose.position));
            Assert.That(session.TryHoldObject(1, itemId, 20d), Is.False);
            Assert.That(session.TryReleaseHeldObject(0, invalidPose, 20d), Is.False);
            Assert.That(session.TryGetHeldObjectId(0, out var heldItemId), Is.True);
            Assert.That(heldItemId, Is.EqualTo(itemId));

            Assert.That(session.TryReleaseHeldObject(0, replacedPose, 20d), Is.True);
            Assert.That(session.TryGetHeldObjectId(0, out _), Is.False);
            Assert.That(session.TryGetItemPlacement(0, out var placement), Is.True);
            Assert.That(placement.Pose.position, Is.EqualTo(replacedPose.position));
            Assert.That(placement.WasAutoPlaced, Is.False);

            Assert.That(session.TryHoldObject(0, otherItemId, 20d), Is.True);
            Assert.That(session.TryReleaseHeldObject(0, firstPose, 20d), Is.True);
            Assert.That(session.TryGetItemPlacement(1, out placement), Is.True);
            Assert.That(placement.Pose.position, Is.EqualTo(firstPose.position));

            Assert.That(session.TryHoldObject(0, "shelf", 20d), Is.True);
            Assert.That(session.TryReleaseHeldObject(0, replacedPose, 20d), Is.True);
            Assert.That(session.TryGetWorldObjectState("shelf", out var mapObject), Is.True);
            Assert.That(mapObject.Pose.position, Is.EqualTo(replacedPose.position));
        }

        [Test]
        public void HidingTurnTimeout_DropsRepickedItemAtLastPlayerPosition()
        {
            session.Start(10d);
            var itemId = session.Assignments[0].Item.ItemId;
            var firstPose = new Pose(new Vector3(2f, 0f, 1f), Quaternion.identity);
            lastKnownPositions[0] = new Vector3(8f, 0f, 4f);

            Assert.That(session.TryRecordItemPlacement(0, firstPose, 20d), Is.True);
            Assert.That(session.TryHoldObject(0, itemId, 25d), Is.True);

            session.AdvanceTime(40d, lastKnownPositions);

            Assert.That(session.TryGetHeldObjectId(0, out _), Is.False);
            Assert.That(session.TryGetItemPlacement(0, out var placement), Is.True);
            Assert.That(placement.Pose.position, Is.EqualTo(lastKnownPositions[0]));
            Assert.That(placement.WasAutoPlaced, Is.True);
        }

        [Test]
        public void PlayerLeavingDuringHiding_DropsHeldOpponentItem()
        {
            session.Start(10d);
            var otherItemId = session.Assignments[1].Item.ItemId;
            var lastPose = new Pose(new Vector3(7f, 0f, 6f), Quaternion.identity);

            Assert.That(session.TryHoldObject(0, otherItemId, 20d), Is.True);
            Assert.That(session.TryHandlePlayerLeft(0, lastPose, 20d), Is.True);

            Assert.That(session.TryGetHeldObjectId(0, out _), Is.False);
            Assert.That(session.TryGetItemPlacement(1, out var droppedItem), Is.True);
            Assert.That(droppedItem.Pose.position, Is.EqualTo(lastPose.position));
            Assert.That(droppedItem.WasAutoPlaced, Is.True);
            Assert.That(session.TryGetItemPlacement(0, out var ownItem), Is.True);
            Assert.That(ownItem.Pose.position, Is.EqualTo(lastPose.position));
        }

        [Test]
        public void PlayerLeavingDuringHiding_DropsHeldMapObject()
        {
            session.Start(10d);
            var lastPose = new Pose(new Vector3(4f, 0f, 9f), Quaternion.identity);

            Assert.That(session.TryHoldObject(0, "shelf", 20d), Is.True);
            Assert.That(session.TryHandlePlayerLeft(0, lastPose, 20d), Is.True);

            Assert.That(session.TryGetHeldObjectId(0, out _), Is.False);
            Assert.That(session.TryGetWorldObjectState("shelf", out var mapObject), Is.True);
            Assert.That(mapObject.Pose.position, Is.EqualTo(lastPose.position));
        }

        [Test]
        public void Session_PreservesLobbyParticipantOrder()
        {
            for (var playerIndex = 0; playerIndex < MatchRulesSO.PlayerCount; playerIndex++)
            {
                var player = session.Players.GetPlayer(playerIndex);

                Assert.That(player.PlayerIndex, Is.EqualTo(playerIndex));
                Assert.That(player.PlayerId, Is.EqualTo($"player-{playerIndex}"));
                Assert.That(
                    session.Assignments[playerIndex].PlayerIndex,
                    Is.EqualTo(player.PlayerIndex));
            }
        }

        [Test]
        public void SixPlayerScenario_CompletesAllMatchPhases()
        {
            const double startedAt = 10d;
            Assert.That(session.Start(startedAt), Is.True);

            for (var playerIndex = 0; playerIndex < MatchRulesSO.PlayerCount - 1; playerIndex++)
            {
                var turnTime = startedAt +
                               (playerIndex * rules.HidingTurnDurationSeconds) +
                               1d;
                var placementPose = new Pose(
                    new Vector3(10f + playerIndex, 0f, playerIndex),
                    Quaternion.identity);

                Assert.That(session.GetCurrentHidingTurnIndex(turnTime), Is.EqualTo(playerIndex));
                Assert.That(
                    session.TryRecordItemPlacement(playerIndex, placementPose, turnTime),
                    Is.True);
            }

            session.AdvanceTime(190d, lastKnownPositions);

            Assert.That(session.CurrentPhase, Is.EqualTo(MatchPhase.Searching));
            Assert.That(session.AllItemsPlaced, Is.True);
            for (var playerIndex = 0; playerIndex < MatchRulesSO.PlayerCount; playerIndex++)
            {
                Assert.That(session.TryGetItemPlacement(playerIndex, out var placement), Is.True);
                Assert.That(
                    placement.WasAutoPlaced,
                    Is.EqualTo(playerIndex == MatchRulesSO.PlayerCount - 1));
            }

            var survivingItemId = session.Assignments[0].Item.ItemId;
            var destroyedItemId = session.Assignments[2].Item.ItemId;
            Assert.That(session.TryHoldObject(0, survivingItemId, 200d), Is.True);
            Assert.That(session.TryHoldObject(1, destroyedItemId, 201d), Is.True);
            Assert.That(session.TryDestroyHeldPlayerItem(1, 201d), Is.True);
            Assert.That(session.DestroyedPlayerItemCount, Is.EqualTo(1));
            Assert.That(session.SetHighlightCandidates(new[]
            {
                Candidate(HighlightType.FirstBlood, "first"),
                Candidate(HighlightType.TteTanMulgun, "popular"),
                Candidate(HighlightType.FinalMoment, "final")
            }), Is.True);

            session.AdvanceTime(520d, lastKnownPositions);
            Assert.That(session.CurrentPhase, Is.EqualTo(MatchPhase.Searching));
            Assert.That(session.IsFinalPeriod(520d), Is.True);

            session.AdvanceTime(550d, lastKnownPositions);

            Assert.That(session.CurrentPhase, Is.EqualTo(MatchPhase.Highlight));
            Assert.That(session.TryGetResult(out var result), Is.True);
            Assert.That(result.EndReason, Is.EqualTo(MatchEndReason.TimeExpired));
            Assert.That(result.WinnerPlayerIndices, Is.EqualTo(new[] { 0 }));
            Assert.That(session.CaptureDestroyedPlayerItemIds(), Is.EqualTo(new[] { destroyedItemId }));

            foreach (var targetId in new[] { "first", "popular", "final" })
            {
                Assert.That(session.TryGetCurrentHighlight(out var highlight), Is.True);
                Assert.That(highlight.TargetId, Is.EqualTo(targetId));
                Assert.That(session.CompleteCurrentHighlight(), Is.True);
            }

            Assert.That(session.CurrentPhase, Is.EqualTo(MatchPhase.Result));
            Assert.That(session.CompleteCurrentHighlight(), Is.False);
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void VariablePlayerSession_UsesConfiguredPlayerCount(int playerCount)
        {
            var playerIds = CreatePlayerIds(playerCount);
            var matchState = new MatchState();

            try
            {
                var matchSession = new MatchSessionCoordinator(
                    rules,
                    matchState,
                    new MatchFlow(rules, matchState, playerIds.Length),
                    new PlayerInteractionSystem(rules, playerIds.Length),
                    playerIds,
                    new TestPlacementValidator(),
                    CreateSpawnPoints(),
                    CreateItemDefinitions(),
                    new System.Random(1234));
                var positions = new Vector3[playerCount];

                Assert.That(matchSession.Assignments.Count, Is.EqualTo(playerCount));
                Assert.That(matchSession.Start(10d), Is.True);
                var hidingEndsAt = 10d + (playerCount * rules.HidingTurnDurationSeconds);
                Assert.That(matchState.PhaseEndsAt.CurrentValue, Is.EqualTo(hidingEndsAt));

                matchSession.AdvanceTime(hidingEndsAt, positions);

                Assert.That(matchSession.AllItemsPlaced, Is.True);
                Assert.That(
                    matchState.CurrentPhase.CurrentValue,
                    Is.EqualTo(MatchPhase.Searching));
                Assert.That(
                    () => matchSession.GetHitCount(playerCount),
                    Throws.TypeOf<System.ArgumentOutOfRangeException>());
            }
            finally
            {
                matchState.Dispose();
            }
        }

        [Test]
        public void RuntimeController_AdvancesOnlyAfterAuthoritativeStart()
        {
            var context = new TestRuntimeContext
            {
                ServerTime = 10d,
                PlayerPositions = lastKnownPositions,
                PlayerPoses = CreatePlayerPoses(lastKnownPositions),
                ReplayObjects = new WorldObjectState[0]
            };
            var controller = new MatchRuntimeController(
                session,
                context,
                CreateInGameAppFlow());

            controller.Tick();
            Assert.That(state.CurrentPhase.CurrentValue, Is.EqualTo(MatchPhase.Waiting));

            Assert.That(controller.StartMatch(), Is.True);
            Assert.That(controller.StartMatch(), Is.False);
            Assert.That(state.CurrentPhase.CurrentValue, Is.EqualTo(MatchPhase.Hiding));

            context.ServerTime = 40d;
            controller.Tick();
            Assert.That(session.TryGetItemPlacement(0, out var placement), Is.True);
            Assert.That(placement.Pose.position, Is.EqualTo(lastKnownPositions[0]));

            context.ServerTime = 190d;
            controller.Tick();
            Assert.That(state.CurrentPhase.CurrentValue, Is.EqualTo(MatchPhase.Searching));
        }

        [Test]
        public void RuntimeController_EntersResultAfterLastHighlight()
        {
            var appFlow = CreateInGameAppFlow();
            var context = new TestRuntimeContext
            {
                ServerTime = 10d,
                PlayerPositions = lastKnownPositions,
                PlayerPoses = CreatePlayerPoses(lastKnownPositions),
                ReplayObjects = new WorldObjectState[0]
            };
            var controller = new MatchRuntimeController(session, context, appFlow);
            Assert.That(controller.StartMatch(), Is.True);
            context.ServerTime = 190d;
            controller.Tick();
            Assert.That(
                session.SetHighlightCandidates(new[]
                {
                    Candidate(HighlightType.FirstBlood, "first")
                }),
                Is.True);

            context.ServerTime = 550d;
            controller.Tick();

            Assert.That(state.CurrentPhase.CurrentValue, Is.EqualTo(MatchPhase.Highlight));
            Assert.That(appFlow.CurrentState, Is.EqualTo(AppFlowState.Highlight));
            Assert.That(session.CompleteCurrentHighlight(), Is.True);
            Assert.That(state.CurrentPhase.CurrentValue, Is.EqualTo(MatchPhase.Result));
            Assert.That(appFlow.CurrentState, Is.EqualTo(AppFlowState.Highlight));

            context.ServerTime = 551d;
            controller.Tick();

            Assert.That(appFlow.CurrentState, Is.EqualTo(AppFlowState.Result));
        }

        [Test]
        public void RuntimeController_PreservesAppFlowOrderWhenHighlightsAreEmpty()
        {
            var appFlow = CreateInGameAppFlow();
            var changedStates = new List<AppFlowState>();
            appFlow.StateChanged += changedStates.Add;
            var context = new TestRuntimeContext
            {
                ServerTime = 10d,
                PlayerPositions = lastKnownPositions,
                PlayerPoses = CreatePlayerPoses(lastKnownPositions),
                ReplayObjects = new WorldObjectState[0]
            };
            var controller = new MatchRuntimeController(session, context, appFlow);
            Assert.That(controller.StartMatch(), Is.True);
            context.ServerTime = 190d;
            controller.Tick();
            Assert.That(
                session.SetHighlightCandidates(new HighlightCandidate[0]),
                Is.True);

            context.ServerTime = 550d;
            controller.Tick();

            Assert.That(state.CurrentPhase.CurrentValue, Is.EqualTo(MatchPhase.Result));
            Assert.That(appFlow.CurrentState, Is.EqualTo(AppFlowState.Result));
            Assert.That(
                changedStates,
                Is.EqualTo(new[] { AppFlowState.Highlight, AppFlowState.Result }));
        }

        [Test]
        public void LobbyMatchStart_StartsSessionAndEntersInGameOnlyForHost()
        {
            var lobby = CreateFullLobby();
            var appFlow = new AppFlowSystem();
            Assert.That(appFlow.TryTransitionTo(AppFlowState.Lobby), Is.True);
            var context = new TestRuntimeContext
            {
                ServerTime = 10d,
                PlayerPositions = lastKnownPositions,
                PlayerPoses = CreatePlayerPoses(lastKnownPositions),
                ReplayObjects = new WorldObjectState[0]
            };
            var matchRuntime = new MatchRuntimeController(session, context, appFlow);
            var startCoordinator = new LobbyMatchStartCoordinator(
                lobby,
                matchRuntime,
                appFlow);

            Assert.That(
                startCoordinator.TryStart("guest"),
                Is.EqualTo(RoomStartResult.NotHost));
            Assert.That(state.CurrentPhase.CurrentValue, Is.EqualTo(MatchPhase.Waiting));
            Assert.That(appFlow.CurrentState, Is.EqualTo(AppFlowState.Lobby));

            Assert.That(
                startCoordinator.TryStart("host"),
                Is.EqualTo(RoomStartResult.Started));
            Assert.That(state.CurrentPhase.CurrentValue, Is.EqualTo(MatchPhase.Hiding));
            Assert.That(appFlow.CurrentState, Is.EqualTo(AppFlowState.InGame));
            Assert.That(
                startCoordinator.TryStart("host"),
                Is.EqualTo(RoomStartResult.AlreadyStarted));
        }

        [Test]
        public void LobbyMatchStart_RejectsStartOutsideLobby()
        {
            var lobby = CreateFullLobby();
            var appFlow = new AppFlowSystem();
            var context = new TestRuntimeContext
            {
                ServerTime = 10d,
                PlayerPositions = lastKnownPositions,
                PlayerPoses = CreatePlayerPoses(lastKnownPositions),
                ReplayObjects = new WorldObjectState[0]
            };
            var matchRuntime = new MatchRuntimeController(session, context, appFlow);
            var startCoordinator = new LobbyMatchStartCoordinator(
                lobby,
                matchRuntime,
                appFlow);

            Assert.That(
                () => startCoordinator.TryStart("host"),
                Throws.InvalidOperationException);
            Assert.That(lobby.IsStarted, Is.False);
            Assert.That(state.CurrentPhase.CurrentValue, Is.EqualTo(MatchPhase.Waiting));
        }

        [Test]
        public void Rematch_ReplacesCompletedSessionAndStartsAgainInSameLobby()
        {
            var lobby = CreateFullLobby();
            var appFlow = new AppFlowSystem();
            Assert.That(appFlow.TryTransitionTo(AppFlowState.Lobby), Is.True);
            var context = new TestRuntimeContext
            {
                ServerTime = 10d,
                PlayerPositions = lastKnownPositions,
                PlayerPoses = CreatePlayerPoses(lastKnownPositions),
                ReplayObjects = new WorldObjectState[0]
            };
            var matchRuntime = new MatchRuntimeController(session, context, appFlow);
            var startCoordinator = new LobbyMatchStartCoordinator(
                lobby,
                matchRuntime,
                appFlow);

            Assert.That(
                startCoordinator.TryStart("host"),
                Is.EqualTo(RoomStartResult.Started));
            Assert.That(
                session.SetHighlightCandidates(new HighlightCandidate[0]),
                Is.True);
            context.ServerTime = 550d;
            matchRuntime.Tick();
            Assert.That(appFlow.CurrentState, Is.EqualTo(AppFlowState.Result));

            var nextState = new MatchState();
            try
            {
                var nextSession = CreateSession(nextState, 5678);

                Assert.That(startCoordinator.TryPrepareRematch(nextSession), Is.True);
                Assert.That(appFlow.CurrentState, Is.EqualTo(AppFlowState.Lobby));
                Assert.That(lobby.IsStarted, Is.False);
                Assert.That(session.CurrentPhase, Is.EqualTo(MatchPhase.Result));
                Assert.That(nextSession.CurrentPhase, Is.EqualTo(MatchPhase.Waiting));

                context.ServerTime = 600d;
                Assert.That(
                    startCoordinator.TryStart("host"),
                    Is.EqualTo(RoomStartResult.Started));
                Assert.That(appFlow.CurrentState, Is.EqualTo(AppFlowState.InGame));
                Assert.That(lobby.IsStarted, Is.True);
                Assert.That(nextSession.CurrentPhase, Is.EqualTo(MatchPhase.Hiding));
            }
            finally
            {
                nextState.Dispose();
            }
        }

        [Test]
        public void Rematch_IsRejectedBeforeCompletedResult()
        {
            var lobby = CreateFullLobby();
            var appFlow = new AppFlowSystem();
            Assert.That(appFlow.TryTransitionTo(AppFlowState.Lobby), Is.True);
            var context = new TestRuntimeContext
            {
                ServerTime = 10d,
                PlayerPositions = lastKnownPositions,
                PlayerPoses = CreatePlayerPoses(lastKnownPositions),
                ReplayObjects = new WorldObjectState[0]
            };
            var matchRuntime = new MatchRuntimeController(session, context, appFlow);
            var startCoordinator = new LobbyMatchStartCoordinator(
                lobby,
                matchRuntime,
                appFlow);
            var nextState = new MatchState();

            try
            {
                var nextSession = CreateSession(nextState, 5678);

                Assert.That(startCoordinator.TryPrepareRematch(nextSession), Is.False);
                Assert.That(
                    startCoordinator.TryStart("host"),
                    Is.EqualTo(RoomStartResult.Started));
                Assert.That(startCoordinator.TryPrepareRematch(nextSession), Is.False);
                Assert.That(appFlow.CurrentState, Is.EqualTo(AppFlowState.InGame));
                Assert.That(lobby.IsStarted, Is.True);
                Assert.That(nextSession.CurrentPhase, Is.EqualTo(MatchPhase.Waiting));
            }
            finally
            {
                nextState.Dispose();
            }
        }

        [Test]
        public void FinalWarning_RaisesOnceWhenSearchingEntersLastThirtySeconds()
        {
            session.Start(10d);
            FinalWarningStartedEvent? warningEvent = null;
            var eventCount = 0;
            session.FinalWarningStarted += value =>
            {
                warningEvent = value;
                eventCount++;
            };

            session.AdvanceTime(519d, lastKnownPositions);
            Assert.That(warningEvent.HasValue, Is.False);

            session.AdvanceTime(520d, lastKnownPositions);
            session.AdvanceTime(530d, lastKnownPositions);

            Assert.That(eventCount, Is.EqualTo(1));
            Assert.That(warningEvent.HasValue, Is.True);
            Assert.That(warningEvent.Value.StartedAt, Is.EqualTo(520d));
            Assert.That(warningEvent.Value.EndsAt, Is.EqualTo(550d));
        }

        [Test]
        public void RuntimeController_RecordsSearchingFramesForCurrentHighlightSegments()
        {
            var context = new TestRuntimeContext
            {
                ServerTime = 10d,
                PlayerPositions = lastKnownPositions,
                PlayerPoses = CreatePlayerPoses(lastKnownPositions),
                ReplayObjects = new[]
                {
                    new WorldObjectState("shelf", Pose.identity)
                }
            };
            var controller = new MatchRuntimeController(
                session,
                context,
                CreateInGameAppFlow());
            Assert.That(controller.StartMatch(), Is.True);
            context.ServerTime = 190d;
            controller.Tick();
            context.ServerTime = 200d;
            controller.Tick();
            var itemId = session.Assignments[1].Item.ItemId;
            Assert.That(session.TryHoldObject(0, itemId, 200d), Is.True);
            Assert.That(session.TryDestroyHeldPlayerItem(0, 200d), Is.True);
            Assert.That(session.SetHighlightCandidates(new[]
            {
                new HighlightCandidate(HighlightType.FirstBlood, 193d, 200d, itemId)
            }), Is.True);

            for (var owner = 0; owner < MatchRulesSO.PlayerCount; owner++)
            {
                if (owner == 1)
                {
                    continue;
                }

                var destroyer = (owner + 1) % MatchRulesSO.PlayerCount;
                var remainingItemId = session.Assignments[owner].Item.ItemId;
                Assert.That(session.TryHoldObject(destroyer, remainingItemId, 201d), Is.True);
                Assert.That(session.TryDestroyHeldPlayerItem(destroyer, 201d), Is.True);
            }

            Assert.That(session.TryCaptureCurrentHighlightReplay(out var clips), Is.True);
            Assert.That(clips, Has.Length.EqualTo(1));
            Assert.That(clips[0].Frames.Count, Is.EqualTo(1));
            Assert.That(clips[0].Frames[0].PlayerPoses[0].position, Is.EqualTo(Vector3.zero));
            Assert.That(clips[0].Frames[0].WorldObjects[0].ObjectId, Is.EqualTo("shelf"));
        }

        [Test]
        public void HighlightPlaybackController_PlaysAllCandidatesAndEntersResult()
        {
            StartSearching();
            var playerPoses = CreatePlayerPoses(lastKnownPositions);
            var itemId = session.Assignments[0].Item.ItemId;
            var replayObject = new GameObject("Replay Item");
            var replayPlayers = new GameObject[MatchRulesSO.PlayerCount];
            var camera = new GameObject("Highlight Camera");
            var fallback = new GameObject("Fallback Camera");

            try
            {
                var playerTargets = new Transform[replayPlayers.Length];
                for (var index = 0; index < replayPlayers.Length; index++)
                {
                    replayPlayers[index] = new GameObject($"Replay Player {index}");
                    playerTargets[index] = replayPlayers[index].transform;
                }

                var objectTargets = new[]
                {
                    new SceneWorldObjectReference(itemId, replayObject.transform)
                };
                Assert.That(
                    session.TryRecordReplayFrame(
                        540d,
                        playerPoses,
                        new[] { new WorldObjectState(itemId, Pose.identity) }),
                    Is.True);
                Assert.That(
                    session.TryRecordReplayFrame(
                        550d,
                        playerPoses,
                        new[] { new WorldObjectState(itemId, Pose.identity) }),
                    Is.True);
                Assert.That(session.SetHighlightCandidates(new[]
                {
                    new HighlightCandidate(HighlightType.FirstBlood, 540d, 545d, itemId),
                    new HighlightCandidate(HighlightType.FinalMoment, 545d, 550d, itemId)
                }), Is.True);
                session.AdvanceTime(550d, lastKnownPositions);

                var replayPlayer = new HighlightReplayPlayer(playerTargets, objectTargets);
                var cameraDirector = new HighlightCameraDirector(
                    camera.transform,
                    fallback.transform,
                    playerTargets,
                    objectTargets);
                var controller = new HighlightPlaybackController(
                    session,
                    replayPlayer,
                    cameraDirector);

                controller.Tick(0f);
                Assert.That(controller.IsPlaying, Is.True);
                Assert.That(session.TryGetCurrentHighlight(out var first), Is.True);
                Assert.That(first.Type, Is.EqualTo(HighlightType.FirstBlood));

                controller.Tick(5f);
                Assert.That(session.TryGetCurrentHighlight(out var second), Is.True);
                Assert.That(second.Type, Is.EqualTo(HighlightType.FinalMoment));

                controller.Tick(5f);
                Assert.That(state.CurrentPhase.CurrentValue, Is.EqualTo(MatchPhase.Result));
                Assert.That(controller.IsPlaying, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(replayObject);
                Object.DestroyImmediate(camera);
                Object.DestroyImmediate(fallback);
                foreach (var replayPlayer in replayPlayers)
                {
                    if (replayPlayer != null)
                    {
                        Object.DestroyImmediate(replayPlayer);
                    }
                }
            }
        }

        [Test]
        public void HudStateQueries_ReturnCurrentPublicMatchState()
        {
            session.Start(10d);

            Assert.That(session.GetRemainingSeconds(10d), Is.EqualTo(180d));
            Assert.That(session.GetCurrentHidingTurnIndex(40d), Is.EqualTo(1));

            session.AdvanceTime(190d, lastKnownPositions);
            Assert.That(session.GetRemainingSeconds(190d), Is.EqualTo(360d));
            Assert.That(session.IsFinalPeriod(519d), Is.False);
            Assert.That(session.IsFinalPeriod(520d), Is.True);

            Assert.That(
                session.RegisterHit(0, 1, Vector3.zero, 200d),
                Is.EqualTo(HitResult.Registered));
            Assert.That(session.GetHitCount(1), Is.EqualTo(1));
            Assert.That(session.GetRemainingDestructionUses(0), Is.EqualTo(5));

            var destroyedItemId = session.Assignments[1].Item.ItemId;
            Assert.That(session.TryHoldObject(0, destroyedItemId, 200d), Is.True);
            Assert.That(session.TryDestroyHeldPlayerItem(0, 200d), Is.True);

            Assert.That(session.DestroyedPlayerItemCount, Is.EqualTo(1));
            Assert.That(
                session.CaptureDestroyedPlayerItemIds(),
                Is.EqualTo(new[] { destroyedItemId }));
        }

        [Test]
        public void SpawnPoses_AreUniqueForHidingAndSearching()
        {
            session.Start(10d);
            var hidingPositions = new HashSet<Vector3>();
            var searchingPositions = new HashSet<Vector3>();

            for (var playerIndex = 0; playerIndex < MatchRulesSO.PlayerCount; playerIndex++)
            {
                var turnStartedAt = 10d + (playerIndex * rules.HidingTurnDurationSeconds);
                Assert.That(
                    session.TryGetCurrentHidingSpawnPose(
                        playerIndex,
                        turnStartedAt,
                        out var hidingSpawnPose),
                    Is.True);
                Assert.That(hidingPositions.Add(hidingSpawnPose.position), Is.True);

                var searchingSpawnPose = session.GetSearchingSpawnPose(playerIndex);
                Assert.That(searchingPositions.Add(searchingSpawnPose.position), Is.True);
            }

            Assert.That(session.TryGetCurrentHidingSpawnPose(1, 10d, out _), Is.False);
        }

        [Test]
        public void DestroyingAllPlayerItems_ConsumesUsesAndEndsSearchingEarly()
        {
            StartSearching();

            Assert.That(session.TryDestroyHeldPlayerItem(0, 200d), Is.False);
            Assert.That(session.GetRemainingDestructionUses(0), Is.EqualTo(5));

            for (var itemOwner = 0; itemOwner < MatchRulesSO.PlayerCount; itemOwner++)
            {
                var destroyer = (itemOwner + 1) % MatchRulesSO.PlayerCount;
                var itemId = session.Assignments[itemOwner].Item.ItemId;
                Assert.That(session.TryHoldObject(destroyer, itemId, 200d), Is.True);
                Assert.That(session.TryDestroyHeldPlayerItem(destroyer, 200d), Is.True);
            }

            Assert.That(session.AllPlayerItemsDestroyed, Is.True);
            Assert.That(state.CurrentPhase.CurrentValue, Is.EqualTo(MatchPhase.Highlight));
            Assert.That(state.PhaseEndsAt.CurrentValue, Is.EqualTo(230d));
            Assert.That(session.TryGetCurrentHighlight(out var highlight), Is.True);
            Assert.That(highlight.Type, Is.EqualTo(HighlightType.FirstBlood));
            Assert.That(session.TryGetResult(out var result), Is.True);
            Assert.That(result.EndReason, Is.EqualTo(MatchEndReason.AllPlayerItemsDestroyed));
            Assert.That(result.EndedAt, Is.EqualTo(200d));
            Assert.That(result.WinnerPlayerIndices, Is.Empty);
        }

        [Test]
        public void WorldObjectPose_PersistsBetweenHidingTurns()
        {
            session.Start(10d);
            var firstPose = new Pose(new Vector3(1f, 2f, 3f), Quaternion.identity);
            var secondPose = new Pose(new Vector3(4f, 5f, 6f), Quaternion.identity);

            Assert.That(
                session.TryRecordWorldObjectPose(1, "shelf", firstPose, 20d),
                Is.False);
            Assert.That(
                session.TryRecordWorldObjectPose(0, "shelf", firstPose, 20d),
                Is.True);

            session.AdvanceTime(40d, lastKnownPositions);

            Assert.That(
                session.TryRecordWorldObjectPose(1, "shelf", secondPose, 45d),
                Is.True);
            Assert.That(session.TryGetWorldObjectState("shelf", out var state), Is.True);
            Assert.That(state.Pose.position, Is.EqualTo(secondPose.position));
        }

        [Test]
        public void PlacementValidator_RejectsInvalidPlayerAndMapObjectPoses()
        {
            session.Start(10d);
            var invalidPose = new Pose(new Vector3(-1f, 0f, 0f), Quaternion.identity);
            var validPose = new Pose(new Vector3(1f, 0f, 0f), Quaternion.identity);

            Assert.That(session.TryRecordItemPlacement(0, invalidPose, 20d), Is.False);
            Assert.That(session.TryGetItemPlacement(0, out _), Is.False);
            Assert.That(
                session.TryRecordWorldObjectPose(0, "shelf", invalidPose, 20d),
                Is.False);
            Assert.That(session.TryGetWorldObjectState("shelf", out var state), Is.True);
            Assert.That(state.Pose.position, Is.EqualTo(Vector3.zero));

            Assert.That(session.TryRecordItemPlacement(0, validPose, 20d), Is.True);
            Assert.That(
                session.TryRecordWorldObjectPose(0, "shelf", validPose, 20d),
                Is.True);
        }

        [Test]
        public void SearchingPlacement_ValidatesPreviewAndReleaseForEveryObject()
        {
            StartSearching();
            var playerItemId = session.Assignments[0].Item.ItemId;
            var invalidPose = new Pose(new Vector3(-1f, 0f, 0f), Quaternion.identity);
            var validPose = new Pose(new Vector3(3f, 0f, 2f), Quaternion.identity);

            Assert.That(session.TryHoldObject(0, playerItemId, 200d), Is.True);
            Assert.That(session.CanPlaceHeldObject(0, invalidPose, 200d), Is.False);
            Assert.That(session.TryReleaseHeldObject(0, invalidPose, 200d), Is.False);
            Assert.That(session.TryGetHeldObjectId(0, out var heldObjectId), Is.True);
            Assert.That(heldObjectId, Is.EqualTo(playerItemId));
            Assert.That(session.CanPlaceHeldObject(0, validPose, 200d), Is.True);
            Assert.That(session.TryReleaseHeldObject(0, validPose, 200d), Is.True);
            Assert.That(session.TryGetItemPlacement(0, out var playerPlacement), Is.True);
            Assert.That(playerPlacement.Pose.position, Is.EqualTo(validPose.position));

            Assert.That(session.TryHoldObject(0, "shelf", 200d), Is.True);
            Assert.That(session.CanPlaceHeldObject(0, invalidPose, 200d), Is.False);
            Assert.That(session.TryReleaseHeldObject(0, invalidPose, 200d), Is.False);
            Assert.That(session.CanPlaceHeldObject(0, validPose, 200d), Is.True);
            Assert.That(session.TryReleaseHeldObject(0, validPose, 200d), Is.True);
            Assert.That(session.TryGetWorldObjectState("shelf", out var mapObject), Is.True);
            Assert.That(mapObject.Pose.position, Is.EqualTo(validPose.position));
        }

        [Test]
        public void Shredder_EjectsMapObjectAfterHalfSecondWithoutDestroyingIt()
        {
            StartSearching();
            var ejectionPose = new Pose(new Vector3(3f, 0f, 4f), Quaternion.identity);

            Assert.That(
                session.TryUseShredderOnHeldMapObject(0, ejectionPose, 200d),
                Is.False);
            Assert.That(session.GetRemainingDestructionUses(0), Is.EqualTo(5));
            Assert.That(session.TryHoldObject(0, "shelf", 200d), Is.True);
            Assert.That(
                session.TryUseShredderOnHeldMapObject(0, ejectionPose, 200d),
                Is.True);
            Assert.That(session.GetRemainingDestructionUses(0), Is.EqualTo(4));
            Assert.That(
                session.TryUseShredderOnHeldMapObject(0, ejectionPose, 200.25d),
                Is.False);

            session.AdvanceTime(200.49d, lastKnownPositions);
            Assert.That(session.TryGetWorldObjectState("shelf", out var state), Is.True);
            Assert.That(state.Pose.position, Is.EqualTo(Vector3.zero));

            session.AdvanceTime(200.5d, lastKnownPositions);
            Assert.That(session.TryGetWorldObjectState("shelf", out state), Is.True);
            Assert.That(state.Pose.position, Is.EqualTo(ejectionPose.position));
            Assert.That(session.GetRemainingDestructionUses(0), Is.EqualTo(4));
            Assert.That(session.TryHoldObject(1, "shelf", 200.5d), Is.True);
        }

        [Test]
        public void PlayerAndMapObjects_ShareOneHeldObjectSlot()
        {
            StartSearching();
            var playerItem = session.Assignments[0].Item.ItemId;
            var releasedPose = new Pose(new Vector3(2f, 0f, 3f), Quaternion.identity);

            Assert.That(session.TryHoldObject(0, "shelf", 200d), Is.True);
            Assert.That(session.TryGetHeldObjectId(0, out var heldObjectId), Is.True);
            Assert.That(heldObjectId, Is.EqualTo("shelf"));
            Assert.That(session.TryHoldObject(0, playerItem, 200d), Is.False);
            Assert.That(session.TryHoldObject(1, "shelf", 200d), Is.False);

            Assert.That(session.TryReleaseHeldObject(0, releasedPose, 200d), Is.True);
            Assert.That(session.TryGetHeldObjectId(0, out _), Is.False);
            Assert.That(session.TryGetWorldObjectState("shelf", out var state), Is.True);
            Assert.That(state.Pose.position, Is.EqualTo(releasedPose.position));
            Assert.That(session.TryHoldObject(0, playerItem, 200d), Is.True);
        }

        [Test]
        public void ThrowHeldPlayerItem_ReleasesAndPublishesLaunch()
        {
            StartSearching();
            var itemId = session.Assignments[0].Item.ItemId;
            var releasePose = new Pose(new Vector3(2f, 1f, 3f), Quaternion.identity);
            var initialVelocity = new Vector3(0f, 2f, 8f);
            ObjectThrownEvent? thrownEvent = null;
            session.ObjectThrown += value => thrownEvent = value;

            Assert.That(session.TryHoldObject(0, itemId, 200d), Is.True);
            Assert.That(
                session.TryThrowHeldObject(0, releasePose, Vector3.zero, 200d),
                Is.False);
            Assert.That(session.TryGetHeldObjectId(0, out _), Is.True);
            Assert.That(
                session.TryThrowHeldObject(
                    0,
                    new Pose(new Vector3(float.NaN, 0f, 0f), Quaternion.identity),
                    initialVelocity,
                    200d),
                Is.False);

            Assert.That(
                session.TryThrowHeldObject(0, releasePose, initialVelocity, 200d),
                Is.True);

            Assert.That(session.TryGetHeldObjectId(0, out _), Is.False);
            Assert.That(session.TryGetItemPlacement(0, out var placement), Is.True);
            Assert.That(placement.Pose.position, Is.EqualTo(releasePose.position));
            Assert.That(thrownEvent.HasValue, Is.True);
            Assert.That(thrownEvent.Value.PlayerIndex, Is.Zero);
            Assert.That(thrownEvent.Value.ObjectId, Is.EqualTo(itemId));
            Assert.That(thrownEvent.Value.ReleasePose.position, Is.EqualTo(releasePose.position));
            Assert.That(thrownEvent.Value.InitialVelocity, Is.EqualTo(initialVelocity));
            Assert.That(thrownEvent.Value.ThrownAt, Is.EqualTo(200d));
        }

        [Test]
        public void HidingPlayer_CanThrowHeldMapObject()
        {
            session.Start(10d);
            var releasePose = new Pose(new Vector3(4f, 1f, 5f), Quaternion.identity);
            var initialVelocity = new Vector3(3f, 2f, 6f);

            Assert.That(session.TryHoldObject(0, "shelf", 20d), Is.True);
            Assert.That(
                session.TryThrowHeldObject(0, releasePose, initialVelocity, 20d),
                Is.True);

            Assert.That(session.TryGetHeldObjectId(0, out _), Is.False);
            Assert.That(session.TryGetWorldObjectState("shelf", out var mapObject), Is.True);
            Assert.That(mapObject.Pose.position, Is.EqualTo(releasePose.position));
            Assert.That(session.TryHoldObject(1, "shelf", 20d), Is.False);
        }

        [Test]
        public void SearchTimeout_PreservesMultipleWinnersAndPlaysHighlights()
        {
            StartSearching();
            var playerZeroItem = session.Assignments[0].Item.ItemId;
            var playerTwoItem = session.Assignments[2].Item.ItemId;
            var playerFiveItem = session.Assignments[5].Item.ItemId;
            MatchResult? endedEvent = null;
            var eventCount = 0;
            session.MatchEnded += value =>
            {
                endedEvent = value;
                eventCount++;
            };

            Assert.That(session.TryHoldObject(0, playerZeroItem, 200d), Is.True);
            Assert.That(session.TryHoldObject(1, playerTwoItem, 200d), Is.True);
            Assert.That(session.TryHoldObject(5, playerFiveItem, 200d), Is.True);
            Assert.That(session.SetHighlightCandidates(new[]
            {
                Candidate(HighlightType.FirstBlood, "first"),
                Candidate(HighlightType.FinalMoment, "second")
            }), Is.True);

            session.AdvanceTime(550d, lastKnownPositions);

            Assert.That(state.CurrentPhase.CurrentValue, Is.EqualTo(MatchPhase.Highlight));
            Assert.That(session.GetWinnerPlayerIndices(), Is.EqualTo(new[] { 0, 5 }));
            Assert.That(session.TryGetResult(out var result), Is.True);
            Assert.That(result.EndReason, Is.EqualTo(MatchEndReason.TimeExpired));
            Assert.That(result.EndedAt, Is.EqualTo(550d));
            Assert.That(result.WinnerPlayerIndices, Is.EqualTo(new[] { 0, 5 }));
            Assert.That(eventCount, Is.EqualTo(1));
            Assert.That(endedEvent.HasValue, Is.True);
            Assert.That(endedEvent.Value.EndReason, Is.EqualTo(MatchEndReason.TimeExpired));
            Assert.That(endedEvent.Value.EndedAt, Is.EqualTo(550d));
            Assert.That(endedEvent.Value.WinnerPlayerIndices, Is.EqualTo(new[] { 0, 5 }));
            session.AdvanceTime(551d, lastKnownPositions);
            Assert.That(eventCount, Is.EqualTo(1));
            Assert.That(session.TryGetCurrentHighlight(out var highlight), Is.True);
            Assert.That(highlight.TargetId, Is.EqualTo("first"));
            Assert.That(session.CompleteCurrentHighlight(), Is.True);
            Assert.That(session.CompleteCurrentHighlight(), Is.True);
            Assert.That(state.CurrentPhase.CurrentValue, Is.EqualTo(MatchPhase.Result));
        }

        [Test]
        public void ThirdHit_DropsHeldItemAndBlocksStunnedPlayerActions()
        {
            StartSearching();
            var heldItem = session.Assignments[1].Item.ItemId;
            var otherItem = session.Assignments[2].Item.ItemId;
            var dropPosition = new Vector3(7f, 0f, 8f);
            PlayerStunnedEvent? stunnedEvent = null;
            var eventCount = 0;
            session.PlayerStunned += value =>
            {
                stunnedEvent = value;
                eventCount++;
            };

            Assert.That(session.TryHoldObject(1, heldItem, 200d), Is.True);
            Assert.That(
                session.RegisterHit(0, 1, dropPosition, 200d),
                Is.EqualTo(HitResult.Registered));
            Assert.That(
                session.RegisterHit(0, 1, dropPosition, 200.1d),
                Is.EqualTo(HitResult.Registered));
            Assert.That(stunnedEvent.HasValue, Is.False);
            Assert.That(
                session.RegisterHit(0, 1, dropPosition, 200.2d),
                Is.EqualTo(HitResult.Stunned));

            Assert.That(eventCount, Is.EqualTo(1));
            Assert.That(stunnedEvent.HasValue, Is.True);
            Assert.That(stunnedEvent.Value.AttackerPlayerIndex, Is.Zero);
            Assert.That(stunnedEvent.Value.TargetPlayerIndex, Is.EqualTo(1));
            Assert.That(stunnedEvent.Value.DroppedObjectId, Is.EqualTo(heldItem));
            Assert.That(stunnedEvent.Value.StunnedAt, Is.EqualTo(200.2d));
            Assert.That(stunnedEvent.Value.StunEndsAt, Is.EqualTo(202.2d));
            Assert.That(session.IsPlayerStunned(1, 200.3d), Is.True);
            Assert.That(session.TryHoldObject(1, otherItem, 200.3d), Is.False);
            Assert.That(session.TryDestroyHeldPlayerItem(1, 200.3d), Is.False);
            Assert.That(
                session.TryUseShredderOnHeldMapObject(1, Pose.identity, 200.3d),
                Is.False);
            Assert.That(session.GetRemainingDestructionUses(1), Is.EqualTo(5));
            Assert.That(
                session.RegisterHit(1, 2, Vector3.zero, 200.3d),
                Is.EqualTo(HitResult.Ignored));

            Assert.That(session.TryGetItemPlacement(1, out var placement), Is.True);
            Assert.That(placement.Pose.position, Is.EqualTo(dropPosition));
            Assert.That(session.TryHoldObject(2, heldItem, 200.3d), Is.True);

            Assert.That(session.IsPlayerStunned(1, 202.2d), Is.False);
            Assert.That(session.TryHoldObject(1, otherItem, 202.2d), Is.True);
        }

        [Test]
        public void ThirdHit_DropsHeldMapObject()
        {
            StartSearching();
            var dropPosition = new Vector3(4f, 0f, 6f);
            PlayerStunnedEvent? stunnedEvent = null;
            session.PlayerStunned += value => stunnedEvent = value;

            Assert.That(session.TryHoldObject(1, "shelf", 200d), Is.True);
            Assert.That(
                session.RegisterHit(0, 1, dropPosition, 200d),
                Is.EqualTo(HitResult.Registered));
            Assert.That(
                session.RegisterHit(0, 1, dropPosition, 200.1d),
                Is.EqualTo(HitResult.Registered));
            Assert.That(
                session.RegisterHit(0, 1, dropPosition, 200.2d),
                Is.EqualTo(HitResult.Stunned));

            Assert.That(session.TryGetHeldObjectId(1, out _), Is.False);
            Assert.That(stunnedEvent.HasValue, Is.True);
            Assert.That(stunnedEvent.Value.DroppedObjectId, Is.EqualTo("shelf"));
            Assert.That(session.TryGetWorldObjectState("shelf", out var state), Is.True);
            Assert.That(state.Pose.position, Is.EqualTo(dropPosition));
            Assert.That(session.TryHoldObject(2, "shelf", 200.3d), Is.True);
        }

        [Test]
        public void DestroyHeldPlayerItem_AllowsOwnAndOpponentItems()
        {
            StartSearching();
            var ownItem = session.Assignments[0].Item.ItemId;
            var opponentItem = session.Assignments[1].Item.ItemId;
            PlayerItemDestroyedEvent? destroyedEvent = null;
            session.PlayerItemDestroyed += value => destroyedEvent = value;

            Assert.That(session.TryHoldObject(0, ownItem, 200d), Is.True);
            Assert.That(session.TryDestroyHeldPlayerItem(0, 200d), Is.True);
            Assert.That(session.GetRemainingDestructionUses(0), Is.EqualTo(4));
            Assert.That(destroyedEvent.HasValue, Is.True);
            Assert.That(destroyedEvent.Value.DestroyerPlayerIndex, Is.Zero);
            Assert.That(destroyedEvent.Value.ItemId, Is.EqualTo(ownItem));
            Assert.That(destroyedEvent.Value.DestroyedAt, Is.EqualTo(200d));

            destroyedEvent = null;
            Assert.That(session.TryHoldObject(0, opponentItem, 200d), Is.True);
            Assert.That(session.TryDestroyHeldPlayerItem(0, 200d), Is.True);
            Assert.That(session.GetRemainingDestructionUses(0), Is.EqualTo(3));
            Assert.That(session.TryHoldObject(2, opponentItem, 200d), Is.False);
            Assert.That(destroyedEvent.HasValue, Is.True);
            Assert.That(destroyedEvent.Value.DestroyerPlayerIndex, Is.Zero);
            Assert.That(destroyedEvent.Value.ItemId, Is.EqualTo(opponentItem));
            Assert.That(destroyedEvent.Value.DestroyedAt, Is.EqualTo(200d));
        }

        [Test]
        public void PlayerLeavingDuringHiding_AutoPlacesItemAndBlocksTurnActions()
        {
            session.Start(10d);
            var lastPose = new Pose(new Vector3(8f, 0f, 3f), Quaternion.identity);

            Assert.That(session.TryHandlePlayerLeft(0, lastPose, 20d), Is.True);
            Assert.That(session.TryHandlePlayerLeft(0, lastPose, 20d), Is.False);
            Assert.That(session.Players.GetPlayer(0).IsActive, Is.False);
            Assert.That(session.Players.ActivePlayerCount, Is.EqualTo(5));
            Assert.That(session.TryGetItemPlacement(0, out var placement), Is.True);
            Assert.That(placement.Pose.position, Is.EqualTo(lastPose.position));
            Assert.That(placement.WasAutoPlaced, Is.True);
            Assert.That(session.TryRecordItemPlacement(0, Pose.identity, 21d), Is.False);
            Assert.That(
                session.TryRecordWorldObjectPose(0, "shelf", Pose.identity, 21d),
                Is.False);
            Assert.That(session.TryGetCurrentHidingSpawnPose(0, 21d, out _), Is.False);
        }

        [Test]
        public void PlayerLeavingDuringSearching_DropsItemAndCannotWinOrInteract()
        {
            StartSearching();
            var ownItem = session.Assignments[1].Item.ItemId;
            var otherItem = session.Assignments[2].Item.ItemId;
            var lastPose = new Pose(new Vector3(6f, 0f, 4f), Quaternion.identity);

            Assert.That(session.TryHoldObject(1, ownItem, 200d), Is.True);
            Assert.That(session.TryHandlePlayerLeft(1, lastPose, 201d), Is.True);

            Assert.That(session.TryGetHeldObjectId(1, out _), Is.False);
            Assert.That(session.TryGetItemPlacement(1, out var placement), Is.True);
            Assert.That(placement.Pose.position, Is.EqualTo(lastPose.position));
            Assert.That(session.TryHoldObject(1, otherItem, 202d), Is.False);
            Assert.That(
                session.RegisterHit(1, 2, Vector3.zero, 202d),
                Is.EqualTo(HitResult.Ignored));
            Assert.That(
                session.RegisterHit(0, 1, Vector3.zero, 202d),
                Is.EqualTo(HitResult.Ignored));

            session.AdvanceTime(550d, lastKnownPositions);

            Assert.That(session.GetWinnerPlayerIndices(), Is.Empty);
            Assert.That(session.TryGetResult(out var result), Is.True);
            Assert.That(result.WinnerPlayerIndices, Is.Empty);
        }

        [Test]
        public void PlayerLeavingDuringSearching_DropsHeldMapObject()
        {
            StartSearching();
            var lastPose = new Pose(new Vector3(3f, 0f, 7f), Quaternion.identity);

            Assert.That(session.TryHoldObject(2, "shelf", 200d), Is.True);
            Assert.That(session.TryHandlePlayerLeft(2, lastPose, 201d), Is.True);

            Assert.That(session.TryGetHeldObjectId(2, out _), Is.False);
            Assert.That(session.TryGetWorldObjectState("shelf", out var mapObject), Is.True);
            Assert.That(mapObject.Pose.position, Is.EqualTo(lastPose.position));
            Assert.That(session.TryHoldObject(3, "shelf", 202d), Is.True);
        }

        [Test]
        public void PlayerLeaving_IsIgnoredOutsideActiveMatchPhases()
        {
            Assert.That(session.TryHandlePlayerLeft(0, Pose.identity, 0d), Is.False);
            Assert.That(session.Players.GetPlayer(0).IsActive, Is.True);
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void PlayerLeavingUntilOneRemains_EndsMatchWithSoleWinner(int playerCount)
        {
            var matchState = new MatchState();
            try
            {
                var matchSession = CreateSession(matchState, 1234, playerCount);
                var positions = new Vector3[playerCount];
                for (var playerIndex = 0; playerIndex < playerCount; playerIndex++)
                {
                    positions[playerIndex] = new Vector3(playerIndex, 0f, 0f);
                }

                Assert.That(matchSession.Start(10d), Is.True);
                var searchingStartedAt = 10d + rules.GetHidingDurationSeconds(playerCount);
                matchSession.AdvanceTime(searchingStartedAt, positions);
                Assert.That(matchSession.CurrentPhase, Is.EqualTo(MatchPhase.Searching));

                for (var playerIndex = playerCount - 1; playerIndex >= 1; playerIndex--)
                {
                    var leftAt = searchingStartedAt + (playerCount - playerIndex);
                    Assert.That(
                        matchSession.TryHandlePlayerLeft(
                            playerIndex,
                            new Pose(positions[playerIndex], Quaternion.identity),
                            leftAt),
                        Is.True);

                    if (playerIndex > 1)
                    {
                        Assert.That(
                            matchSession.CurrentPhase,
                            Is.EqualTo(MatchPhase.Searching));
                    }
                }

                Assert.That(matchSession.CurrentPhase, Is.EqualTo(MatchPhase.Result));
                Assert.That(matchSession.Players.ActivePlayerCount, Is.EqualTo(1));
                Assert.That(matchSession.TryGetResult(out var result), Is.True);
                Assert.That(result.EndReason, Is.EqualTo(MatchEndReason.LastPlayerStanding));
                Assert.That(result.WinnerPlayerIndices, Is.EqualTo(new[] { 0 }));
            }
            finally
            {
                matchState.Dispose();
            }
        }

        [Test]
        public void LastPlayerStanding_SkipsHighlightAndResultAndPreparesRematchFromLobby()
        {
            var lobby = CreateFullLobby();
            var appFlow = new AppFlowSystem();
            Assert.That(appFlow.TryTransitionTo(AppFlowState.Lobby), Is.True);
            var changedStates = new List<AppFlowState>();
            appFlow.StateChanged += changedStates.Add;
            var context = new TestRuntimeContext
            {
                ServerTime = 10d,
                PlayerPositions = lastKnownPositions,
                PlayerPoses = CreatePlayerPoses(lastKnownPositions),
                ReplayObjects = new WorldObjectState[0]
            };
            var matchRuntime = new MatchRuntimeController(session, context, appFlow);
            var startCoordinator = new LobbyMatchStartCoordinator(
                lobby,
                matchRuntime,
                appFlow);

            Assert.That(
                startCoordinator.TryStart("host"),
                Is.EqualTo(RoomStartResult.Started));
            context.ServerTime = 190d;
            matchRuntime.Tick();
            for (var playerIndex = MatchRulesSO.MaxPlayerCount - 1;
                 playerIndex >= 1;
                 playerIndex--)
            {
                Assert.That(
                    session.TryHandlePlayerLeft(
                        playerIndex,
                        new Pose(lastKnownPositions[playerIndex], Quaternion.identity),
                        200d + (MatchRulesSO.MaxPlayerCount - playerIndex)),
                    Is.True);
            }

            context.ServerTime = 210d;
            matchRuntime.Tick();

            Assert.That(appFlow.CurrentState, Is.EqualTo(AppFlowState.Lobby));
            Assert.That(
                changedStates,
                Is.EqualTo(new[] { AppFlowState.InGame, AppFlowState.Lobby }));

            var nextState = new MatchState();
            try
            {
                var nextSession = CreateSession(nextState, 5678);
                Assert.That(startCoordinator.TryPrepareRematch(nextSession), Is.True);
                Assert.That(lobby.IsStarted, Is.False);
                Assert.That(appFlow.CurrentState, Is.EqualTo(AppFlowState.Lobby));
            }
            finally
            {
                nextState.Dispose();
            }
        }

        [Test]
        public void LateAdvanceTime_CapturesScheduledSearchEnd()
        {
            session.Start(10d);

            session.AdvanceTime(600d, lastKnownPositions);

            Assert.That(session.TryGetResult(out var result), Is.True);
            Assert.That(result.EndReason, Is.EqualTo(MatchEndReason.TimeExpired));
            Assert.That(result.EndedAt, Is.EqualTo(550d));
        }

        private void StartSearching()
        {
            session.Start(10d);
            session.AdvanceTime(190d, lastKnownPositions);
        }

        private MatchSessionCoordinator CreateSession(
            MatchState matchState,
            int randomSeed,
            int playerCount = MatchRulesSO.MaxPlayerCount)
        {
            var playerIds = CreatePlayerIds(playerCount);
            return new MatchSessionCoordinator(
                rules,
                matchState,
                new MatchFlow(rules, matchState, playerIds.Length),
                new PlayerInteractionSystem(rules, playerIds.Length),
                playerIds,
                new TestPlacementValidator(),
                CreateSpawnPoints(),
                CreateItemDefinitions(),
                new System.Random(randomSeed),
                new[]
                {
                    new WorldObjectState("shelf", new Pose(Vector3.zero, Quaternion.identity))
                });
        }

        private static RoomLobbySystem CreateFullLobby()
        {
            var request = new RoomCreateRequest(
                "테스트방",
                false,
                null,
                MatchRulesSO.PlayerCount,
                "market-01");
            request.TryCreateSettings(
                MatchRulesSO.PlayerCount,
                out var settings,
                out _);
            return new RoomLobbySystem(settings, "host", MatchRulesSO.PlayerCount);
        }

        private static AppFlowSystem CreateInGameAppFlow()
        {
            var appFlow = new AppFlowSystem();
            appFlow.TryTransitionTo(AppFlowState.Lobby);
            appFlow.TryTransitionTo(AppFlowState.InGame);
            return appFlow;
        }

        private static ItemDefinition[] CreateItemDefinitions()
        {
            return new[]
            {
                new ItemDefinition("bear", "toy"),
                new ItemDefinition("ball", "toy"),
                new ItemDefinition("apple", "food"),
                new ItemDefinition("bread", "food"),
                new ItemDefinition("hammer", "tool"),
                new ItemDefinition("wrench", "tool"),
                new ItemDefinition("cup", "kitchen"),
                new ItemDefinition("plate", "kitchen")
            };
        }

        private static string[] CreatePlayerIds(
            int playerCount = MatchRulesSO.MaxPlayerCount)
        {
            var playerIds = new string[playerCount];
            for (var playerIndex = 0; playerIndex < playerIds.Length; playerIndex++)
            {
                playerIds[playerIndex] = $"player-{playerIndex}";
            }

            return playerIds;
        }

        private static HighlightCandidate Candidate(HighlightType type, string targetId)
        {
            return new HighlightCandidate(type, 0d, 10d, targetId);
        }

        private static Pose[] CreateSpawnPoints()
        {
            var spawnPoints = new Pose[8];
            for (var index = 0; index < spawnPoints.Length; index++)
            {
                spawnPoints[index] = new Pose(
                    new Vector3(index * 2f, 0f, index),
                    Quaternion.identity);
            }

            return spawnPoints;
        }

        private static Pose[] CreatePlayerPoses(IReadOnlyList<Vector3> positions)
        {
            var poses = new Pose[positions.Count];
            for (var index = 0; index < positions.Count; index++)
            {
                poses[index] = new Pose(positions[index], Quaternion.identity);
            }

            return poses;
        }

        private sealed class TestPlacementValidator : IPlacementValidator
        {
            public bool IsValid(string objectId, Pose pose)
            {
                return pose.position.x >= 0f;
            }
        }

        private sealed class TestRuntimeContext : IMatchRuntimeContext
        {
            public double ServerTime { get; set; }
            public IReadOnlyList<Vector3> PlayerPositions { get; set; }
            public IReadOnlyList<Pose> PlayerPoses { get; set; }
            public IReadOnlyList<WorldObjectState> ReplayObjects { get; set; }
        }
    }
}
