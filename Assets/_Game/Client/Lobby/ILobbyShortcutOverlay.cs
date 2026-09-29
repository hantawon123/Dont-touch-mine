using System;

namespace Game.Client.Lobby
{
    public interface ILobbyShortcutOverlay
    {
        event Action CloseRequested;

        LobbyShortcutKind OpenKind { get; }

        bool IsOpen { get; }

        void Show(LobbyShortcutKind kind);

        void Hide();

        /// <summary>
        /// Asks to be closed as if the overlay itself was dismissed, so Esc
        /// and the presenter's idea of whether it is open stay in step.
        /// </summary>
        void RequestClose();
    }
}
