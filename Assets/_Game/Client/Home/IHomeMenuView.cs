using System;
using Game.Core.Home;

namespace Game.Client.Home
{
    public interface IHomeMenuView
    {
        event Action<HomeMenuAction> ActionClicked;

        void SetNickname(string nickname);

        void SetLevel(int level);
    }
}
