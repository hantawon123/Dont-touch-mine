using System;

namespace Game.Core.Flow
{
    public enum AppFlowState
    {
        Home,
        RoomBrowser,
        Lobby,
        InGame,
        Highlight,
        Result
    }

    public sealed class AppFlowSystem
    {
        public AppFlowState CurrentState { get; private set; } = AppFlowState.Home;

        public event Action<AppFlowState> StateChanged;

        public bool TryTransitionTo(AppFlowState nextState)
        {
            if (!CanTransitionTo(nextState))
            {
                return false;
            }

            CurrentState = nextState;
            StateChanged?.Invoke(nextState);
            return true;
        }

        public bool CanTransitionTo(AppFlowState nextState)
        {
            switch (CurrentState)
            {
                case AppFlowState.Home:
                    return nextState == AppFlowState.RoomBrowser ||
                           nextState == AppFlowState.Lobby;
                case AppFlowState.RoomBrowser:
                    return nextState == AppFlowState.Home ||
                           nextState == AppFlowState.Lobby;
                case AppFlowState.Lobby:
                    return nextState == AppFlowState.RoomBrowser ||
                           nextState == AppFlowState.InGame;
                case AppFlowState.InGame:
                    return nextState == AppFlowState.Highlight ||
                           nextState == AppFlowState.Lobby;
                case AppFlowState.Highlight:
                    return nextState == AppFlowState.Result;
                case AppFlowState.Result:
                    return nextState == AppFlowState.Lobby;
                default:
                    return false;
            }
        }

        /// <summary>Aligns an existing room with confirmed state, including snapshot rollback.</summary>
        public bool TryRestoreSessionState(AppFlowState restoredState)
        {
            if (!IsSessionState(CurrentState) || !IsSessionState(restoredState)) return false;
            if (CurrentState == restoredState) return true;
            CurrentState = restoredState;
            StateChanged?.Invoke(restoredState);
            return true;
        }

        private static bool IsSessionState(AppFlowState state) =>
            state is AppFlowState.Lobby or AppFlowState.InGame or AppFlowState.Highlight or AppFlowState.Result;
    }
}
