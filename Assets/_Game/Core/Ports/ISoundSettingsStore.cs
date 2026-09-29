using Game.Core.Settings;

namespace Game.Core.Ports
{
    /// <summary>
    /// Remembers the 사운드 settings on this machine.
    /// </summary>
    /// <remarks>
    /// A choice about this computer more than any other tab's: the microphone
    /// is a device plugged into it, and how loud is a matter of the speakers
    /// on the desk.
    /// </remarks>
    public interface ISoundSettingsStore
    {
        /// <summary>
        /// Reads what was saved. False when nothing has been saved here, which
        /// is the signal to start from the defaults rather than an error.
        /// </summary>
        bool TryLoad(out SoundSettings settings);

        void Save(SoundSettings settings);
    }
}
