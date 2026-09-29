using Game.Core.Settings;

namespace Game.Core.Ports
{
    /// <summary>
    /// Remembers the 일반 settings on this machine.
    /// </summary>
    /// <remarks>
    /// A choice about this computer rather than about the account, like the
    /// server region: the same player on another machine may well want another
    /// language, so nothing here is sent anywhere.
    /// </remarks>
    public interface IGeneralSettingsStore
    {
        /// <summary>
        /// Reads what was saved. False when nothing has been saved here, which
        /// is the signal to start from the defaults rather than an error.
        /// </summary>
        bool TryLoad(out GeneralSettings settings);

        void Save(GeneralSettings settings);
    }
}
