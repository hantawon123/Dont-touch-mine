using System.Collections.Generic;

namespace Game.Core.Ports
{
    /// <summary>
    /// The microphones this machine has, by the names the machine gives them.
    /// </summary>
    /// <remarks>
    /// Asked for rather than fixed in a catalogue because the answer is
    /// different on every computer and can change while the game runs. Read
    /// when the settings screen opens and when the saved device is checked;
    /// not watched, because a headset plugged in mid-session will be there
    /// the next time the tab is opened.
    /// </remarks>
    public interface IMicrophoneDevices
    {
        IReadOnlyList<string> Names { get; }
    }
}
