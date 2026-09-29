using System;
using Game.Core.Ports;

namespace Game.Core.Settings
{
    /// <summary>
    /// Remembers the interface settings for as long as the process lives.
    /// </summary>
    /// <inheritdoc cref="InMemoryGraphicsSettingsStore"/>
    public sealed class InMemoryInterfaceSettingsStore : IInterfaceSettingsStore
    {
        private InterfaceSettings? saved;

        /// <summary>What was last saved, or null. For tests.</summary>
        public InterfaceSettings? Saved => saved;

        public bool TryLoad(out InterfaceSettings settings)
        {
            settings = saved ?? InterfaceSettings.Empty;
            return saved.HasValue;
        }

        public void Save(InterfaceSettings settings)
        {
            saved = settings;
        }
    }

    /// <summary>
    /// The 인터페이스 settings in force, and remembering them.
    /// </summary>
    /// <remarks>
    /// No applier, unlike <see cref="GraphicsSettingsSystem"/>. Nothing here
    /// changes anything by itself: what these rows describe is what the HUD
    /// draws during a match, and the HUD reads <see cref="Current"/> when it
    /// draws. So this only has to hold the answer and say when it changes.
    /// </remarks>
    public sealed class InterfaceSettingsSystem
    {
        private readonly IInterfaceSettingsStore store;

        /// <param name="catalog">
        /// What the rows offer. Null takes what the game ships with.
        /// </param>
        public InterfaceSettingsSystem(IInterfaceSettingsStore store, InterfaceCatalog catalog = null)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            Catalog = catalog ?? InterfaceCatalog.Shipped;
            Current = Catalog.Normalise(store.TryLoad(out var saved) ? saved : InterfaceSettings.Empty);
        }

        public InterfaceCatalog Catalog { get; }

        public InterfaceSettings Current { get; private set; }

        /// <summary>
        /// What a player who has never opened the tab gets, and what 초기화
        /// puts back.
        /// </summary>
        public InterfaceSettings Defaults => Catalog.Defaults;

        public event Action<InterfaceSettings> Changed;

        /// <summary>
        /// Settles on these values and writes them down. Nothing happens, and
        /// nobody is told, when they are already the current ones.
        /// </summary>
        public void Apply(InterfaceSettings settings)
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
