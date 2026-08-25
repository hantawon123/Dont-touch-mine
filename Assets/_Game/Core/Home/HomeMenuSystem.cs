using System;

namespace Game.Core.Home
{
    public enum HomeMenuAction
    {
        QuickPlay,
        FindRoom,
        ProfileSettings,
        Friends,
        Settings,
        Quit
    }

    public sealed class HomeMenuSystem
    {
        public event Action<HomeMenuAction> ActionRequested;

        public void Request(HomeMenuAction action)
        {
            if (!Enum.IsDefined(typeof(HomeMenuAction), action))
            {
                throw new ArgumentOutOfRangeException(nameof(action));
            }

            ActionRequested?.Invoke(action);
        }
    }
}
