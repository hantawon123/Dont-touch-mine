using System;
using Game.Core.Ports;

namespace Game.Core.Settings
{
    /// <summary>
    /// Remembers the control settings for as long as the process lives.
    /// </summary>
    /// <inheritdoc cref="InMemoryGraphicsSettingsStore"/>
    public sealed class InMemoryControlSettingsStore : IControlSettingsStore
    {
        private ControlSettings? saved;

        /// <summary>What was last saved, or null. For tests.</summary>
        public ControlSettings? Saved => saved;

        public bool TryLoad(out ControlSettings settings)
        {
            settings = saved ?? ControlSettings.Empty;
            return saved.HasValue;
        }

        public void Save(ControlSettings settings)
        {
            saved = settings;
        }
    }

    /// <summary>
    /// A capture that waits for a test to say what was pressed rather than for
    /// a player.
    /// </summary>
    public sealed class FakeKeyCapture : IKeyCapture
    {
        private Action<string> captured;

        public bool IsCapturing => captured != null;

        public int BeginCount { get; private set; }

        public void Begin(Action<string> onCaptured)
        {
            captured = onCaptured;
            BeginCount++;
        }

        public void Cancel() => captured = null;

        /// <summary>Answers the capture with a key, as the player would.</summary>
        public void Press(string keyCode)
        {
            var waiting = captured;
            captured = null;
            waiting?.Invoke(keyCode);
        }

        /// <summary>Answers it with Escape, which means never mind.</summary>
        public void PressEscape() => Press(null);
    }

    /// <summary>
    /// The 컨트롤 settings in force, and remembering them.
    /// </summary>
    /// <remarks>
    /// No applier. What these rows describe is which key does what during a
    /// match, and the input layer reads <see cref="Current"/> when it binds —
    /// so this only has to hold the answer and say when it changes.
    /// </remarks>
    public sealed class ControlSettingsSystem
    {
        private readonly IControlSettingsStore store;

        public ControlSettingsSystem(IControlSettingsStore store)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            Current = ControlCatalog.Normalise(
                store.TryLoad(out var saved) ? saved : ControlCatalog.Defaults);
        }

        public ControlSettings Current { get; private set; }

        /// <summary>
        /// What a player who has never opened the tab gets, and what 초기화
        /// puts back.
        /// </summary>
        public ControlSettings Defaults => ControlCatalog.Defaults;

        public event Action<ControlSettings> Changed;

        /// <summary>
        /// Settles on these values and writes them down. Nothing happens, and
        /// nobody is told, when they are already the current ones.
        /// </summary>
        public void Apply(ControlSettings settings)
        {
            var next = ControlCatalog.Normalise(settings);
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
