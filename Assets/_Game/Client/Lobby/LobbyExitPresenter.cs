using System;

namespace Game.Client.Lobby
{
    /// <summary>
    /// Reports voluntary departure. Project-owned navigation waits for network cleanup.
    /// </summary>
    /// <remarks>
    /// No view is wired here. The button that asks lives in the Esc menu, and
    /// keeping the way out a method rather than a subscription means there is
    /// one leaving path however many screens come to offer it.
    /// </remarks>
    public sealed class LobbyExitPresenter
    {
        /// <summary>The player asked to leave the room, not just this screen.</summary>
        public event Action LeaveRequested;

        /// <summary>
        /// The player asked to go. Called by whatever screen offered them the
        /// way out.
        /// </summary>
        public void RequestLeave() => LeaveRequested?.Invoke();
    }
}
