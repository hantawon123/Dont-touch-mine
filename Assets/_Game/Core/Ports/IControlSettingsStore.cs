using Game.Core.Settings;

namespace Game.Core.Ports
{
    /// <summary>
    /// Remembers the 컨트롤 settings on this machine.
    /// </summary>
    /// <inheritdoc cref="IInterfaceSettingsStore"/>
    public interface IControlSettingsStore
    {
        /// <summary>
        /// Reads what was saved. False when nothing has been saved here, which
        /// is the signal to start from the defaults rather than an error.
        /// </summary>
        bool TryLoad(out ControlSettings settings);

        void Save(ControlSettings settings);
    }
}
