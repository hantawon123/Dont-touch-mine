using System;
using System.Collections.Generic;
using Game.Core.Lobby;

namespace Game.Client.Lobby
{
    public interface IKeyGuideView
    {
        event Action OpenRequested;
        event Action CloseRequested;

        void SetVisible(bool visible);
        void SetEntries(IReadOnlyList<ControlKeyBinding> bindings);
    }
}
