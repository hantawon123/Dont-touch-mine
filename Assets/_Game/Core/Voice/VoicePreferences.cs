using Game.Core.Ports;

namespace Game.Core.Voice
{
    /// <summary>
    /// For containers built without a machine behind them — tests, and the
    /// dedicated server — so that resolving the preferences never depends on
    /// player preferences existing.
    /// </summary>
    public sealed class InMemoryVoicePreferencesStore : IVoicePreferencesStore
    {
        private bool saved;
        private bool muted;
        private bool listening = true;

        /// <summary>What was last saved, or null. For tests.</summary>
        public (bool muted, bool listening)? Saved =>
            saved ? (muted, listening) : null;

        public bool TryLoad(out bool muted, out bool listening)
        {
            muted = this.muted;
            listening = this.listening;
            return saved;
        }

        public void Save(bool muted, bool listening)
        {
            this.muted = muted;
            this.listening = listening;
            saved = true;
        }
    }

    /// <summary>
    /// What the player decided about their own microphone and speaker, kept
    /// across the screens they carry it through and the rooms they join later.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="IVoiceControl"/> because the two live for
    /// different lengths of time. The control mirrors a voice rig that is built
    /// and destroyed with each session, and only exists on screens that offer a
    /// microphone button. This outlives all of them: a player who muted
    /// themselves in the lobby meant it for the match as well, and for the next
    /// room after that.
    /// <para>
    /// Mute and listen so far. A chosen input device or an output volume
    /// would belong here too.
    /// </para>
    /// </remarks>
    public sealed class VoicePreferences
    {
        private readonly IVoicePreferencesStore store;
        private bool muted;
        private bool listening = true;

        public VoicePreferences() : this(null)
        {
        }

        public VoicePreferences(IVoicePreferencesStore store)
        {
            this.store = store;
            if (store != null && store.TryLoad(out var savedMuted, out var savedListening))
            {
                muted = savedMuted;
                listening = savedListening;
            }
        }

        /// <summary>
        /// True while the player has silenced themselves. Survives the walk from
        /// the lobby into a match and back, and the next room after that.
        /// </summary>
        public bool Muted
        {
            get => muted;
            set
            {
                if (muted == value)
                {
                    return;
                }

                muted = value;
                Persist();
            }
        }

        /// <summary>
        /// True while other people's voice should play. Starts on because the
        /// room already joins voice; this only decides whether this machine
        /// hears it.
        /// </summary>
        public bool Listening
        {
            get => listening;
            set
            {
                if (listening == value)
                {
                    return;
                }

                listening = value;
                Persist();
            }
        }

        private void Persist() => store?.Save(muted, listening);
    }
}
