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
                    return nextState == AppFlowState.Highlight;
                case AppFlowState.Highlight:
                    return nextState == AppFlowState.Result;
                case AppFlowState.Result:
                    return nextState == AppFlowState.Home;
                default:
                    return false;
            }
        }
    }
}
