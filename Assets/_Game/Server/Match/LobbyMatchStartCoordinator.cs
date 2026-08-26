using System;
using Game.Core.Flow;
using Game.Core.Lobby;

namespace Game.Server.Match
{
    public sealed class LobbyMatchStartCoordinator
    {
        private readonly RoomLobbySystem lobby;
        private readonly MatchRuntimeController matchRuntime;
        private readonly AppFlowSystem appFlow;

        public LobbyMatchStartCoordinator(
            RoomLobbySystem lobby,
            MatchRuntimeController matchRuntime,
            AppFlowSystem appFlow)
        {
            this.lobby = lobby ?? throw new ArgumentNullException(nameof(lobby));
            this.matchRuntime = matchRuntime ??
                throw new ArgumentNullException(nameof(matchRuntime));
            this.appFlow = appFlow ?? throw new ArgumentNullException(nameof(appFlow));
        }

        public RoomStartResult TryStart(string requesterPlayerId)
        {
            if (lobby.IsStarted)
            {
                return RoomStartResult.AlreadyStarted;
            }

            if (!appFlow.CanTransitionTo(AppFlowState.InGame))
            {
                throw new InvalidOperationException(
                    "The application must be in the lobby before starting a match.");
            }

            var result = lobby.TryStart(requesterPlayerId);
            if (result != RoomStartResult.Started)
            {
                return result;
            }

            if (!matchRuntime.StartMatch() ||
                !appFlow.TryTransitionTo(AppFlowState.InGame))
            {
                throw new InvalidOperationException(
                    "The match session could not be initialized.");
            }

            return RoomStartResult.Started;
        }

        public bool TryPrepareRematch(MatchSessionCoordinator nextSession)
        {
            var isAlreadyInLobby = appFlow.CurrentState == AppFlowState.Lobby;
            if ((!isAlreadyInLobby && appFlow.CurrentState != AppFlowState.Result) ||
                !lobby.IsStarted ||
                !matchRuntime.TryPrepareRematch(nextSession))
            {
                return false;
            }

            if (!lobby.TryPrepareRematch() ||
                (!isAlreadyInLobby && !appFlow.TryTransitionTo(AppFlowState.Lobby)))
            {
                throw new InvalidOperationException(
                    "The completed match could not return to the lobby.");
            }

            return true;
        }
    }
}
