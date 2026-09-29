using System;
using Game.Core.Ports;

namespace Game.Core.Settings
{
    /// <summary>
    /// Remembers the graphics settings for as long as the process lives.
    /// </summary>
    /// <remarks>
    /// For containers built without a machine behind them — tests, and the
    /// service registration's own default — so that resolving the settings
    /// system never depends on player preferences existing.
    /// </remarks>
    public sealed class InMemoryGraphicsSettingsStore : IGraphicsSettingsStore
    {
        private GraphicsSettings? saved;

        /// <summary>What was last saved, or null. For tests.</summary>
        public GraphicsSettings? Saved => saved;

        public bool TryLoad(out GraphicsSettings settings)
        {
            settings = saved ?? GraphicsSettings.Empty;
            return saved.HasValue;
        }

        public void Save(GraphicsSettings settings)
        {
            saved = settings;
        }
    }

    /// <summary>
    /// Changes nothing about the picture. For containers with no renderer to
    /// speak to, which is every test and the registration's own default.
    /// </summary>
    public sealed class NullGraphicsSettingsApplier : IGraphicsSettingsApplier
    {
        public GraphicsSettings? Applied { get; private set; }

        public int ApplyCount { get; private set; }

        public void Apply(GraphicsSettings settings)
        {
            Applied = settings;
            ApplyCount++;
        }
    }

    /// <summary>
    /// The 그래픽 settings in force: remembering them, and carrying them to the
    /// renderer.
    /// </summary>
    /// <remarks>
    /// Holds only what has been applied. The settings screen keeps its own
    /// draft while the player is still deciding, and hands it here through
    /// <see cref="Apply"/> when they press 적용하기 — so closing the screen
    /// without applying costs nothing more than the draft going away.
    /// <para>
    /// Nothing is applied to the renderer at construction. What is saved has to
    /// reach the picture when the game starts, but that is a step in the
    /// application's own startup rather than a side effect of resolving a
    /// service — see <c>GraphicsSettingsStartup</c> — and a test that builds
    /// this must not resize anybody's window.
    /// </para>
    /// </remarks>
    public sealed class GraphicsSettingsSystem
    {
        private readonly IGraphicsSettingsStore store;
        private readonly IGraphicsSettingsApplier applier;

        /// <param name="catalog">
        /// What the rows offer. Null takes what the game ships with.
        /// </param>
        public GraphicsSettingsSystem(
            IGraphicsSettingsStore store,
            IGraphicsSettingsApplier applier = null,
            GraphicsCatalog catalog = null)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.applier = applier ?? new NullGraphicsSettingsApplier();
            Catalog = catalog ?? GraphicsCatalog.Shipped;
            Current = Catalog.Normalise(store.TryLoad(out var saved) ? saved : GraphicsSettings.Empty);
        }

        public GraphicsCatalog Catalog { get; }

        public GraphicsSettings Current { get; private set; }

        /// <summary>
        /// What a player who has never opened the tab gets, and what 초기화
        /// puts back.
        /// </summary>
        public GraphicsSettings Defaults => Catalog.Defaults;

        public event Action<GraphicsSettings> Changed;

        /// <summary>
        /// Settles on these values, writes them down and carries them to the
        /// renderer. Nothing happens, and nobody is told, when they are already
        /// the current ones.
        /// </summary>
        public void Apply(GraphicsSettings settings)
        {
            var next = Catalog.Normalise(settings);
            if (next == Current)
            {
                return;
            }

            Current = next;
            store.Save(next);

            // The renderer before the listeners: what is on screen should have
            // changed by the time anything reacts to the change.
            applier.Apply(next);
            Changed?.Invoke(next);
        }

        /// <summary>
        /// Carries whatever is currently in force to the renderer, without
        /// saving or telling anyone. For the application's startup, which has
        /// to make a saved choice real before the first frame anybody sees.
        /// </summary>
        public void ApplyToRenderer() => applier.Apply(Current);
    }
}
