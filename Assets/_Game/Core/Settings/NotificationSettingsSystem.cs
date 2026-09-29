using System;
using Game.Core.Ports;

namespace Game.Core.Settings
{
    /// <summary>
    /// Remembers the notification settings for as long as the process lives.
    /// </summary>
    /// <inheritdoc cref="InMemoryGraphicsSettingsStore"/>
    public sealed class InMemoryNotificationSettingsStore : INotificationSettingsStore
    {
        private NotificationSettings? saved;

        /// <summary>What was last saved, or null. For tests.</summary>
        public NotificationSettings? Saved => saved;

        public bool TryLoad(out NotificationSettings settings)
        {
            settings = saved ?? NotificationSettings.Empty;
            return saved.HasValue;
        }

        public void Save(NotificationSettings settings)
        {
            saved = settings;
        }
    }

    /// <summary>
    /// The 알림 settings in force, and remembering them.
    /// </summary>
    /// <remarks>
    /// No applier, for the reason <see cref="InterfaceSettingsSystem"/> gives:
    /// whatever raises a notice asks <see cref="Current"/> whether it is
    /// wanted, at the moment it would raise it.
    /// </remarks>
    public sealed class NotificationSettingsSystem
    {
        private readonly INotificationSettingsStore store;

        /// <param name="catalog">
        /// What the rows offer. Null takes what the game ships with.
        /// </param>
        public NotificationSettingsSystem(
            INotificationSettingsStore store, NotificationCatalog catalog = null)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            Catalog = catalog ?? NotificationCatalog.Shipped;
            Current = Catalog.Normalise(
                store.TryLoad(out var saved) ? saved : NotificationSettings.Empty);
        }

        public NotificationCatalog Catalog { get; }

        public NotificationSettings Current { get; private set; }

        /// <summary>
        /// What a player who has never opened the tab gets, and what 초기화
        /// puts back.
        /// </summary>
        public NotificationSettings Defaults => Catalog.Defaults;

        public event Action<NotificationSettings> Changed;

        /// <summary>
        /// Settles on these values and writes them down. Nothing happens, and
        /// nobody is told, when they are already the current ones.
        /// </summary>
        public void Apply(NotificationSettings settings)
        {
            var next = Catalog.Normalise(settings);
            if (next == Current)
            {
                return;
            }

            Current = next;
            store.Save(next);
            Changed?.Invoke(next);
        }
    }
}
