using System;
using System.Collections.Generic;
using Game.Core.Flow;
using Game.Core.Items;
using Game.Core.Lobby;
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
            IReadOnlyList<WorldObjectState> initialWorldObjects = null)
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
                var interactions = new PlayerInteractionSystem(rules, playerCount);
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
                    initialWorldObjects);
                return new MatchSessionComposition(state, session);
            }
            catch
            {
                state.Dispose();
                throw;
            }
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
