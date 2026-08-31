using System;
using System.Collections.Generic;
using Game.Core.Flow;
using Game.Core.Items;
using Game.Core.Lobby;
using Game.Core.Match;
using Game.Network.Match;
using Game.Server.Items;
using Game.Server.Match;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    public sealed class NetworkMatchRuntimeConfiguration
    {
        public NetworkMatchRuntimeConfiguration(
            IPlacementValidator placementValidator,
            IReadOnlyList<Pose> spawnPoints,
            IReadOnlyList<ItemDefinition> itemDefinitions,
            IReadOnlyList<WorldObjectState> initialWorldObjects,
            Pose shredderEjectionPose,
            IReadOnlyList<Pose> hidingWaitingSpawnPoints = null)
        {
            PlacementValidator = placementValidator ??
                throw new ArgumentNullException(nameof(placementValidator));
            SpawnPoints = spawnPoints ?? throw new ArgumentNullException(nameof(spawnPoints));
            ItemDefinitions = itemDefinitions ??
                throw new ArgumentNullException(nameof(itemDefinitions));
            InitialWorldObjects = initialWorldObjects ??
                throw new ArgumentNullException(nameof(initialWorldObjects));
            ShredderEjectionPose = shredderEjectionPose;
            HidingWaitingSpawnPoints = hidingWaitingSpawnPoints ?? spawnPoints;
            if (HidingWaitingSpawnPoints.Count < spawnPoints.Count)
            {
                throw new ArgumentException(
                    "A waiting point is required for every match spawn point.",
                    nameof(hidingWaitingSpawnPoints));
            }
        }

        public IPlacementValidator PlacementValidator { get; }
        public IReadOnlyList<Pose> SpawnPoints { get; }
        public IReadOnlyList<ItemDefinition> ItemDefinitions { get; }
        public IReadOnlyList<WorldObjectState> InitialWorldObjects { get; }
        public Pose ShredderEjectionPose { get; }
        public IReadOnlyList<Pose> HidingWaitingSpawnPoints { get; }
    }

    /// <summary>
    /// Owns the authority-side match runtime for one network session.
    /// </summary>
    public sealed class NetworkMatchRuntimeCoordinator :
        IStartable,
        IDisposable
    {
        private readonly INetworkMatchAuthority network;
        private readonly MatchRuntimeFactory factory;
        private readonly IMatchRuntimeContext sceneContext;
        private readonly AppFlowSystem appFlow;
        private readonly NetworkMatchRuntimeConfiguration configuration;
        private readonly RoomBrowserSystem roomState;

        private MatchSessionComposition composition;
        private MatchRuntimeController runtime;
        private MatchParticipant[] pendingLineUp;
        private MatchStateSnapshot lastPublishedSnapshot;
        private bool[] synchronizedControls = Array.Empty<bool>();
        private bool[] initializedAssignments = Array.Empty<bool>();
        private readonly PlayerItemAssignment[] assignmentBuffer =
            new PlayerItemAssignment[1];
        private MatchPhase synchronizedPhase = (MatchPhase)(-1);
        private int synchronizedHidingTurn = -1;
        private bool hidingInitialPlacementDone;
        private bool hasSynchronizedPlayers;
        private bool hasPublishedSnapshot;
        private bool hasPublishedHighlightReplay;
        private bool waitingForHighlightReady;
        private double highlightReadyDeadline;
        private bool started;

        public NetworkMatchRuntimeCoordinator(
            INetworkMatchAuthority network,
            MatchRuntimeFactory factory,
            IMatchRuntimeContext sceneContext,
            AppFlowSystem appFlow,
            NetworkMatchRuntimeConfiguration configuration,
            RoomBrowserSystem roomState)
        {
            this.network = network ?? throw new ArgumentNullException(nameof(network));
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
            this.sceneContext = sceneContext ??
                throw new ArgumentNullException(nameof(sceneContext));
            this.appFlow = appFlow ?? throw new ArgumentNullException(nameof(appFlow));
            this.configuration = configuration ??
                throw new ArgumentNullException(nameof(configuration));
            this.roomState = roomState ?? throw new ArgumentNullException(nameof(roomState));
        }

        public void Start()
        {
            if (started)
            {
                return;
            }

            started = true;
            network.LineUpReceived += OnLineUpReceived;
            network.SimulationTick += OnSimulationTick;
            network.SceneLoaded += OnSceneLoaded;

            var currentLineUp = roomState.MatchParticipants.CurrentValue;
            if (currentLineUp.Count > 0)
            {
                OnLineUpReceived(currentLineUp);
            }
        }

        public void Dispose()
        {
            if (started)
            {
                started = false;
                network.LineUpReceived -= OnLineUpReceived;
                network.SimulationTick -= OnSimulationTick;
                network.SceneLoaded -= OnSceneLoaded;
            }

            StopRuntime();
        }

        private void OnLineUpReceived(IReadOnlyList<MatchParticipant> participants)
        {
            StopRuntime();

            if (!network.IsServer || participants == null || participants.Count == 0)
            {
                return;
            }

            pendingLineUp = new MatchParticipant[participants.Count];
            for (var index = 0; index < participants.Count; index++)
            {
                pendingLineUp[index] = participants[index];
            }
        }

        private void OnSimulationTick()
        {
            if (!network.IsRuntimeReady) return;
            if (!network.IsServer)
            {
                StopRuntime();
                return;
            }

            if (pendingLineUp != null)
            {
                var participants = pendingLineUp;
                pendingLineUp = null;
                StartRuntime(participants);
            }

            if (runtime == null)
            {
                return;
            }

            runtime.Tick();
            SynchronizePlayers();
            PublishSnapshotIfChanged();
        }

        private void StartRuntime(IReadOnlyList<MatchParticipant> participants)
        {
            var networkContext = new NetworkMatchRuntimeContext(
                network,
                sceneContext,
                participants);
            var created = factory.CreateSessionFromParticipants(
                participants,
                configuration.PlacementValidator,
                configuration.SpawnPoints,
                configuration.ItemDefinitions,
                new System.Random(),
                configuration.InitialWorldObjects,
                network.DestructionLimit);

            try
            {
                var createdRuntime = new MatchRuntimeController(
                    created.Session,
                    networkContext,
                    appFlow);

                if (!network.BindMatchSession(
                        created.Session,
                        configuration.ShredderEjectionPose) ||
                    !createdRuntime.StartMatch())
                {
                    throw new InvalidOperationException(
                        "The authority could not initialize the network match runtime.");
                }

                composition = created;
                runtime = createdRuntime;
                PublishSnapshotIfChanged();
            }
            catch
            {
                network.UnbindMatchSession(created.Session);
                created.Dispose();
                composition = null;
                runtime = null;
                hasPublishedSnapshot = false;
                throw;
            }
        }

        // 씬 로드 직후 스포너가 전원을 씬 스폰 위치로 재배치해 경기 배치를 덮어쓴다.
        // 숨기기 초기 배치를 다음 틱에 다시 적용하도록 표시한다.
        private void OnSceneLoaded()
        {
            hidingInitialPlacementDone = false;
        }

        private void SynchronizePlayers()
        {
            var session = composition.Session;
            var now = network.ServerTime;
            var phase = session.CurrentPhase;
            var hidingTurn = phase == MatchPhase.Hiding
                ? session.GetCurrentHidingTurnIndex(now)
                : -1;
            var phaseChanged = phase != synchronizedPhase;

            if (initializedAssignments.Length != session.Assignments.Count)
            {
                initializedAssignments = new bool[session.Assignments.Count];
            }

            if (phase != MatchPhase.Hiding)
            {
                hidingInitialPlacementDone = false;
            }

            if (phase == MatchPhase.Hiding && hidingTurn >= 0 &&
                (!hidingInitialPlacementDone || hidingTurn != synchronizedHidingTurn) &&
                session.TryGetCurrentHidingSpawnPose(
                    hidingTurn,
                    now,
                    out var hidingPose))
            {
                // 페이즈 진입 틱에 아바타가 아직 준비되지 않았을 수 있으므로,
                // 초기 배치는 "한 번 성공할 때까지" 재시도한다. (일회성 감지 금지)
                if (!hidingInitialPlacementDone)
                {
                    // 숨기기 페이즈 진입: 대기자를 먼저 밖으로 비운 뒤
                    // 현재 턴 플레이어를 집 안으로 옮겨 캐릭터 겹침을 막는다.
                    for (var playerIndex = 0;
                         playerIndex < session.Players.Players.Count;
                         playerIndex++)
                    {
                        if (!session.Players.IsActive(playerIndex) ||
                            playerIndex == hidingTurn)
                        {
                            continue;
                        }

                        var pose = configuration.HidingWaitingSpawnPoints[playerIndex];
                        var teleported = network.TryTeleportPlayer(playerIndex, pose);
                        var player = session.Players.GetPlayer(playerIndex);
                        Debug.Log(
                            $"[HidingSpawn] turn={hidingTurn}, " +
                            $"playerIndex={playerIndex}, playerId={player.PlayerId}, " +
                            $"role=waiting, " +
                            $"target={pose.position}, success={teleported}.");
                        if (!teleported)
                        {
                            throw new InvalidOperationException(
                                $"The authority could not position waiting player {playerIndex}.");
                        }
                    }

                    var hiderTeleported = network.TryTeleportPlayer(hidingTurn, hidingPose);
                    var hider = session.Players.GetPlayer(hidingTurn);
                    Debug.Log(
                        $"[HidingSpawn] turn={hidingTurn}, " +
                        $"playerIndex={hidingTurn}, playerId={hider.PlayerId}, " +
                        $"role=hider, target={hidingPose.position}, " +
                        $"success={hiderTeleported}.");
                    if (!hiderTeleported)
                    {
                        throw new InvalidOperationException(
                            $"The authority could not position hiding player {hidingTurn}.");
                    }

                    hidingInitialPlacementDone = true;
                }
                else
                {
                    // 턴 교대: 새로 숨기는 사람만 집 안으로, 직전에 숨긴 사람만 대기 구역으로.
                    // 나머지는 있던 자리를 유지한다.
                    var hiderTeleported = network.TryTeleportPlayer(hidingTurn, hidingPose);
                    var hider = session.Players.GetPlayer(hidingTurn);
                    Debug.Log(
                        $"[HidingSpawn] turn={hidingTurn}, " +
                        $"playerIndex={hidingTurn}, playerId={hider.PlayerId}, " +
                        $"role=hider, target={hidingPose.position}, " +
                        $"success={hiderTeleported}.");
                    if (!hiderTeleported)
                    {
                        throw new InvalidOperationException(
                            $"The authority could not position hiding player {hidingTurn}.");
                    }

                    var previousTurn = synchronizedHidingTurn;
                    if (previousTurn >= 0 && previousTurn != hidingTurn &&
                        session.Players.IsActive(previousTurn))
                    {
                        var waitingPose =
                            configuration.HidingWaitingSpawnPoints[previousTurn];
                        var waitingTeleported =
                            network.TryTeleportPlayer(previousTurn, waitingPose);
                        var waiting = session.Players.GetPlayer(previousTurn);
                        Debug.Log(
                            $"[HidingSpawn] turn={hidingTurn}, " +
                            $"playerIndex={previousTurn}, playerId={waiting.PlayerId}, " +
                            $"role=waiting, target={waitingPose.position}, " +
                            $"success={waitingTeleported}.");
                        if (!waitingTeleported)
                        {
                            throw new InvalidOperationException(
                                $"The authority could not position waiting player {previousTurn}.");
                        }
                    }
                }

                InitializeCurrentAssignment(session, hidingTurn);
            }

            if (phase == MatchPhase.Searching && phaseChanged)
            {
                for (var playerIndex = 0;
                     playerIndex < session.Players.Players.Count;
                     playerIndex++)
                {
                    if (session.Players.IsActive(playerIndex) &&
                        !network.TryTeleportPlayer(
                            playerIndex,
                            session.GetSearchingSpawnPose(playerIndex)))
                    {
                        throw new InvalidOperationException(
                            $"The authority could not position searching player {playerIndex}.");
                    }
                }
            }

            if (synchronizedControls.Length != session.Players.Players.Count)
            {
                synchronizedControls = new bool[session.Players.Players.Count];
                hasSynchronizedPlayers = false;
            }

            for (var playerIndex = 0;
                 playerIndex < synchronizedControls.Length;
                 playerIndex++)
            {
                // Departed avatars have already been despawned by the network.
                if (!session.Players.IsActive(playerIndex))
                {
                    synchronizedControls[playerIndex] = false;
                    continue;
                }

                // 숨기기 대기자도 로비처럼 밖에서 이동하고 공격 모션을
                // 사용할 수 있다. 실제 기절 판정은 MatchSessionCoordinator가
                // 찾기 페이즈에만 적용한다.
                var enabled = phase == MatchPhase.Hiding ||
                               (phase == MatchPhase.Searching &&
                                !session.IsPlayerStunned(playerIndex, now));
                if (hasSynchronizedPlayers &&
                    synchronizedControls[playerIndex] == enabled)
                {
                    continue;
                }

                if (!network.TrySetPlayerControls(playerIndex, enabled))
                {
                    throw new InvalidOperationException(
                        $"The authority could not set controls for player {playerIndex}.");
                }

                synchronizedControls[playerIndex] = enabled;
            }

            synchronizedPhase = phase;
            synchronizedHidingTurn = hidingTurn;
            hasSynchronizedPlayers = true;
        }

        private void InitializeCurrentAssignment(
            MatchSessionCoordinator session,
            int playerIndex)
        {
            if (initializedAssignments[playerIndex])
            {
                return;
            }

            assignmentBuffer[0] = session.Assignments[playerIndex];
            if (!network.TryInitializeAssignedItems(assignmentBuffer) ||
                !network.TryPublishItemAssignments(assignmentBuffer))
            {
                throw new InvalidOperationException(
                    $"The authority could not initialize the assigned item for player {playerIndex}.");
            }

            initializedAssignments[playerIndex] = true;
        }

        private void PublishSnapshotIfChanged()
        {
            var session = composition.Session;
            if (session.CurrentPhase == MatchPhase.Highlight &&
                (!hasPublishedSnapshot || lastPublishedSnapshot.Phase != MatchPhase.Highlight))
            {
                session.WaitForHighlightPlayback();
                waitingForHighlightReady = true;
                highlightReadyDeadline = network.ServerTime + 30d;
            }
            var snapshot = composition.Session.CaptureStateSnapshot();
            if (snapshot.Phase == MatchPhase.Highlight && !hasPublishedHighlightReplay &&
                composition.Session.TryGetResult(out var result) &&
                network.ServerTime >= result.EndedAt + MatchSessionCoordinator.HighlightPostRollSeconds)
            {
                if (!composition.Session.TryCaptureHighlightReplay(out var replay) ||
                    !network.TryPublishHighlightReplay(replay))
                    throw new InvalidOperationException("The authority could not publish the highlight replay.");
                hasPublishedHighlightReplay = true;
            }
            if (snapshot.Phase == MatchPhase.Highlight && waitingForHighlightReady)
            {
                if (hasPublishedHighlightReplay && network.IsHighlightReplayReady)
                {
                    session.ScheduleHighlightPlayback(network.ServerTime + HighlightPresentationTiming.ReadyLeadSeconds);
                    waitingForHighlightReady = false;
                }
                else if (network.ServerTime >= highlightReadyDeadline)
                {
                    Debug.LogWarning("[Highlight] Replay preparation timed out; skipping to results.");
                    while (session.CompleteCurrentHighlight()) { }
                    waitingForHighlightReady = false;
                }
                snapshot = session.CaptureStateSnapshot();
            }
            if (hasPublishedSnapshot &&
                snapshot.Phase == lastPublishedSnapshot.Phase &&
                snapshot.PhaseEndsAt == lastPublishedSnapshot.PhaseEndsAt)
            {
                return;
            }

            if (!network.TryPublishMatchState(snapshot))
            {
                throw new InvalidOperationException(
                    "The authority could not publish the match state.");
            }

            lastPublishedSnapshot = snapshot;
            hasPublishedSnapshot = true;
        }

        private void StopRuntime()
        {
            if (composition != null)
            {
                // Result disables every avatar. The room-level avatars survive
                // the scene change, so restore them before returning to Lobby.
                if (network.IsServer)
                {
                    var players = composition.Session.Players;
                    for (var playerIndex = 0;
                         playerIndex < players.Players.Count;
                         playerIndex++)
                    {
                        if (players.IsActive(playerIndex))
                        {
                            network.TrySetPlayerControls(playerIndex, true);
                        }
                    }
                }

                network.UnbindMatchSession(composition.Session);
                composition.Dispose();
            }

            composition = null;
            runtime = null;
            pendingLineUp = null;
            synchronizedControls = Array.Empty<bool>();
            initializedAssignments = Array.Empty<bool>();
            synchronizedPhase = (MatchPhase)(-1);
            synchronizedHidingTurn = -1;
            hidingInitialPlacementDone = false;
            hasSynchronizedPlayers = false;
            hasPublishedSnapshot = false;
            hasPublishedHighlightReplay = false;
            waitingForHighlightReady = false;
        }
    }
}
