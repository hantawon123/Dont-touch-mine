using System;

namespace Game.Core.Home
{
    public enum HomeMenuAction
    {
        CreateRoom,
        FindRoom,
        Character,
        ProfileSettings,
        Friends,
        ServerSettings,
        Settings,
        Quit,
        Tutorial
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
