using System;

namespace Game.Core.Ports
{
    /// <summary>
    /// Waits for the player to press something, so a key can be put on an
    /// action.
    /// </summary>
    /// <remarks>
    /// A port because it has to listen to the whole keyboard and mouse at
    /// once, which only the input layer can do, and because a screen's rules
    /// should be testable without one.
    /// <para>
    /// One capture at a time. Asking again while a capture is running cancels
    /// the first, which is what pressing a second key button means.
    /// </para>
    /// </remarks>
    public interface IKeyCapture
    {
        bool IsCapturing { get; }

        /// <param name="captured">
        /// Told what was pressed, as a code of the kind
        /// <see cref="Settings.ControlCatalog.KeyLabel"/> reads. Told null when
        /// the player pressed Escape, which means they thought better of it.
        /// </param>
        void Begin(Action<string> captured);

        /// <summary>
        /// Stops listening without telling anyone. For a screen closing under
        /// a capture that is still running.
        /// </summary>
        void Cancel();
    }
}
