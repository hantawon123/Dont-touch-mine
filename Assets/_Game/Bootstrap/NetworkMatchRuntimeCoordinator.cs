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
            Pose shredderEjectionPose)
        {
            PlacementValidator = placementValidator ??
                throw new ArgumentNullException(nameof(placementValidator));
            SpawnPoints = spawnPoints ?? throw new ArgumentNullException(nameof(spawnPoints));
            ItemDefinitions = itemDefinitions ??
                throw new ArgumentNullException(nameof(itemDefinitions));
            InitialWorldObjects = initialWorldObjects ??
                throw new ArgumentNullException(nameof(initialWorldObjects));
            ShredderEjectionPose = shredderEjectionPose;
        }

        public IPlacementValidator PlacementValidator { get; }
        public IReadOnlyList<Pose> SpawnPoints { get; }
        public IReadOnlyList<ItemDefinition> ItemDefinitions { get; }
        public IReadOnlyList<WorldObjectState> InitialWorldObjects { get; }
        public Pose ShredderEjectionPose { get; }
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
        private bool hasPublishedSnapshot;
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
                configuration.InitialWorldObjects);

            try
            {
                var createdRuntime = new MatchRuntimeController(
                    created.Session,
                    networkContext,
                    appFlow);

                if (!network.BindMatchSession(
                        created.Session,
                        configuration.ShredderEjectionPose) ||
                    !network.TryPublishItemAssignments(created.Session.Assignments) ||
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

        private void PublishSnapshotIfChanged()
        {
            var snapshot = composition.Session.CaptureStateSnapshot();
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
                network.UnbindMatchSession(composition.Session);
                composition.Dispose();
            }

            composition = null;
            runtime = null;
            pendingLineUp = null;
            hasPublishedSnapshot = false;
        }
    }
}
