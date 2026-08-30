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

        /// <summary>
        /// Asks to be closed as if the panel's own close button was pressed.
        /// </summary>
        /// <remarks>
        /// Esc in the lobby has to be able to back out of this panel, and the
        /// presenter that owns it tracks whether it is open. Hiding the panel
        /// from outside would leave that flag saying open, and the next request
        /// to open would read as a request to close instead.
        /// </remarks>
        void RequestClose();
    }
}
