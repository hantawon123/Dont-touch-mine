using Game.Core.Settings;

namespace Game.Core.Ports
{
    /// <summary>
    /// Remembers the 인터페이스 settings on this machine.
    /// </summary>
    /// <remarks>
    /// A choice about this computer rather than about the account, like the
    /// other settings: how big the interface should be depends on the screen
    /// it is being read on.
    /// <para>
    /// One row is arguably not: who may see the player's own nickname is about
    /// the account, and a player who hid it on one machine would probably
    /// expect it hidden on another. It is stored here with the rest until
    /// there is a server that keeps it.
    /// </para>
    /// </remarks>
    public interface IInterfaceSettingsStore
    {
        /// <summary>
        /// Reads what was saved. False when nothing has been saved here, which
        /// is the signal to start from the defaults rather than an error.
        /// </summary>
        bool TryLoad(out InterfaceSettings settings);

        void Save(InterfaceSettings settings);
    }
}
