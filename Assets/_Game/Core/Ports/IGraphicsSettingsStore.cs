using Game.Core.Settings;

namespace Game.Core.Ports
{
    /// <summary>
    /// Remembers the 그래픽 settings on this machine.
    /// </summary>
    /// <remarks>
    /// A choice about this computer rather than about the account, like the
    /// server region and the 일반 settings: the same player on a weaker machine
    /// wants different answers, so nothing here is sent anywhere.
    /// </remarks>
    public interface IGraphicsSettingsStore
    {
        /// <summary>
        /// Reads what was saved. False when nothing has been saved here, which
        /// is the signal to start from the defaults rather than an error.
        /// </summary>
        bool TryLoad(out GraphicsSettings settings);

        void Save(GraphicsSettings settings);
    }
}
