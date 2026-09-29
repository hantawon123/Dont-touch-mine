using Game.Core.Settings;

namespace Game.Core.Ports
{
    /// <summary>
    /// Remembers the 알림 settings on this machine.
    /// </summary>
    /// <remarks>
    /// Arguably about the account rather than the computer — a player who
    /// turned invitations off probably means it wherever they play — but it is
    /// kept here with the rest of the tabs until there is a server that keeps
    /// it, which is the same bargain <see cref="IInterfaceSettingsStore"/>
    /// makes for the nickname's visibility.
    /// </remarks>
    public interface INotificationSettingsStore
    {
        /// <summary>
        /// Reads what was saved. False when nothing has been saved here, which
        /// is the signal to start from the defaults rather than an error.
        /// </summary>
        bool TryLoad(out NotificationSettings settings);

        void Save(NotificationSettings settings);
    }
}
