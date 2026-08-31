using System;
using System.Collections.Generic;
using Game.Core.Flow;
using Game.Core.Items;
using Game.Core.Lobby;
using Game.Core.Match;
using Game.Server.Items;
using Game.Server.Players;
using Game.SOAP.Config;
using UnityEngine;
using VContainer;

namespace Game.Server.Match
{
    public sealed class MatchRuntimeFactory
    {
        private readonly MatchRulesSO rules;

        [Inject]
        public MatchRuntimeFactory(MatchRulesSO rules)
        {
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        public MatchRuntimeComposition CreateFromParticipants(
            RoomLobbySystem lobby,
            IMatchRuntimeContext runtimeContext,
            AppFlowSystem appFlow,
            IReadOnlyList<MatchParticipant> participants,
            IPlacementValidator placementValidator,
            IReadOnlyList<Pose> spawnPoints,
            IReadOnlyList<ItemDefinition> itemDefinitions,
            System.Random random,
            IReadOnlyList<WorldObjectState> initialWorldObjects = null)
        {
            return Create(
                lobby,
                runtimeContext,
                appFlow,
                CaptureParticipantIds(participants),
                placementValidator,
                spawnPoints,
                itemDefinitions,
                random,
                initialWorldObjects);
        }

        public MatchRuntimeComposition Create(
            RoomLobbySystem lobby,
            IMatchRuntimeContext runtimeContext,
            AppFlowSystem appFlow,
            IReadOnlyList<string> participantIds,
            IPlacementValidator placementValidator,
            IReadOnlyList<Pose> spawnPoints,
            IReadOnlyList<ItemDefinition> itemDefinitions,
            System.Random random,
            IReadOnlyList<WorldObjectState> initialWorldObjects = null)
        {
            if (lobby == null)
            {
                throw new ArgumentNullException(nameof(lobby));
            }

            if (runtimeContext == null)
            {
                throw new ArgumentNullException(nameof(runtimeContext));
            }

            if (appFlow == null)
            {
                throw new ArgumentNullException(nameof(appFlow));
            }

            if (participantIds == null)
            {
                throw new ArgumentNullException(nameof(participantIds));
            }

            if (lobby.CurrentPlayerCount != participantIds.Count)
            {
                throw new ArgumentException(
                    "Lobby player count and participant count must match.",
                    nameof(participantIds));
            }

            var session = CreateSession(
                participantIds,
                placementValidator,
                spawnPoints,
                itemDefinitions,
                random,
                initialWorldObjects);

            try
            {
                var runtime = new MatchRuntimeController(
                    session.Session,
                    runtimeContext,
                    appFlow);
                var lobbyStart = new LobbyMatchStartCoordinator(
                    lobby,
                    runtime,
                    appFlow);
                return new MatchRuntimeComposition(session, runtime, lobbyStart);
            }
            catch
            {
                session.Dispose();
                throw;
            }
        }

        public MatchSessionComposition CreateSession(
            IReadOnlyList<string> participantIds,
            IPlacementValidator placementValidator,
            IReadOnlyList<Pose> spawnPoints,
            IReadOnlyList<ItemDefinition> itemDefinitions,
            System.Random random,
            IReadOnlyList<WorldObjectState> initialWorldObjects = null,
            int? destructionUsesPerPlayer = null,
            IReadOnlyList<PlayerItemAssignment> restoredAssignments = null)
        {
            if (participantIds == null)
            {
                throw new ArgumentNullException(nameof(participantIds));
            }

            var state = new MatchState();
            try
            {
                var playerCount = participantIds.Count;
                var flow = new MatchFlow(rules, state, playerCount);
                var interactions = new PlayerInteractionSystem(
                    rules,
                    playerCount,
                    destructionUsesPerPlayer ?? rules.DestructionUsesPerPlayer);
                var session = new MatchSessionCoordinator(
                    rules,
                    state,
                    flow,
                    interactions,
                    participantIds,
                    placementValidator,
                    spawnPoints,
                    itemDefinitions,
                    random,
                    initialWorldObjects,
                    restoredAssignments);
                return new MatchSessionComposition(state, session);
            }
            catch
            {
                state.Dispose();
                throw;
            }
        }

        public MatchSessionComposition CreateSessionFromParticipants(
            IReadOnlyList<MatchParticipant> participants,
            IPlacementValidator placementValidator,
            IReadOnlyList<Pose> spawnPoints,
            IReadOnlyList<ItemDefinition> itemDefinitions,
            System.Random random,
            IReadOnlyList<WorldObjectState> initialWorldObjects = null,
            int? destructionUsesPerPlayer = null)
        {
            return CreateSession(
                CaptureParticipantIds(participants),
                placementValidator,
                spawnPoints,
                itemDefinitions,
                random,
                initialWorldObjects,
                destructionUsesPerPlayer);
        }

        public MatchSessionComposition RestoreSession(
            MatchMigrationState snapshot, double now, IPlacementValidator validator,
            IReadOnlyList<Pose> spawnPoints, IReadOnlyList<ItemDefinition> itemDefinitions,
            IReadOnlyList<WorldObjectState> initialObjects, int destructionUses)
        {
            if (snapshot?.Players == null) throw new ArgumentNullException(nameof(snapshot));
            var ids = new string[snapshot.Players.Length];
            var assignments = new PlayerItemAssignment[ids.Length];
            for (var i = 0; i < ids.Length; i++)
            {
                ids[i] = snapshot.Players[i].PlayerId;
                var found = false;
                foreach (var item in itemDefinitions)
                {
                    if (item.ItemId != snapshot.Players[i].ItemId) continue;
                    assignments[i] = new PlayerItemAssignment(i, item);
                    found = true;
                    break;
                }
                if (!found) throw new ArgumentException("Unknown migrated assignment.", nameof(snapshot));
            }
            var created = CreateSession(ids, validator, spawnPoints, itemDefinitions,
                new System.Random(0), initialObjects, destructionUses, assignments);
            try
            {
                created.Session.RestoreMigration(snapshot, now);
                return created;
            }
            catch
            {
                created.Dispose();
                throw;
            }
        }

        private static string[] CaptureParticipantIds(
            IReadOnlyList<MatchParticipant> participants)
        {
            if (participants == null)
            {
                throw new ArgumentNullException(nameof(participants));
            }

            MatchRulesSO.ValidatePlayerCount(participants.Count);
            var ordered = new MatchParticipant[participants.Count];
            var playerIds = new HashSet<string>(StringComparer.Ordinal);
            var assignedIndices = new bool[participants.Count];

            for (var index = 0; index < participants.Count; index++)
            {
                var participant = participants[index];
                if (string.IsNullOrWhiteSpace(participant.PlayerId) ||
                    participant.PlayerIndex < 0 ||
                    participant.PlayerIndex >= participants.Count ||
                    !playerIds.Add(participant.PlayerId) ||
                    assignedIndices[participant.PlayerIndex])
                {
                    throw new ArgumentException(
                        "Participants require unique player ids and contiguous player indices from zero.",
                        nameof(participants));
                }

                assignedIndices[participant.PlayerIndex] = true;
                ordered[index] = participant;
            }

            Array.Sort(
                ordered,
                (left, right) => left.PlayerIndex.CompareTo(right.PlayerIndex));
            var orderedPlayerIds = new string[ordered.Length];
            for (var index = 0; index < ordered.Length; index++)
            {
                orderedPlayerIds[index] = ordered[index].PlayerId;
            }

            return orderedPlayerIds;
        }
    }

    public sealed class MatchSessionComposition : IDisposable
    {
        internal MatchSessionComposition(
            MatchState state,
            MatchSessionCoordinator session)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            Session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public MatchState State { get; }
        public MatchSessionCoordinator Session { get; }

        public void Dispose()
        {
            State.Dispose();
        }
    }

    public sealed class MatchRuntimeComposition : IDisposable
    {
        private readonly MatchSessionComposition sessionComposition;

        internal MatchRuntimeComposition(
            MatchSessionComposition sessionComposition,
            MatchRuntimeController runtime,
            LobbyMatchStartCoordinator lobbyStart)
        {
            this.sessionComposition = sessionComposition ??
                throw new ArgumentNullException(nameof(sessionComposition));
            Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            LobbyStart = lobbyStart ?? throw new ArgumentNullException(nameof(lobbyStart));
        }

        public MatchState State => sessionComposition.State;
        public MatchSessionCoordinator Session => sessionComposition.Session;
        public MatchRuntimeController Runtime { get; }
        public LobbyMatchStartCoordinator LobbyStart { get; }

        public void Dispose()
        {
            sessionComposition.Dispose();
        }
    }
}
