using System;
using System.Collections.Generic;
using Game.Core.Ports;

namespace Game.Core.Settings
{
    /// <summary>
    /// Remembers the sound settings for as long as the process lives.
    /// </summary>
    /// <inheritdoc cref="InMemoryGraphicsSettingsStore"/>
    public sealed class InMemorySoundSettingsStore : ISoundSettingsStore
    {
        private SoundSettings? saved;

        /// <summary>What was last saved, or null. For tests.</summary>
        public SoundSettings? Saved => saved;

        public bool TryLoad(out SoundSettings settings)
        {
            settings = saved ?? SoundSettings.Empty;
            return saved.HasValue;
        }

        public void Save(SoundSettings settings)
        {
            saved = settings;
        }
    }

    /// <summary>
    /// Changes nothing about what is heard. For containers with no audio to
    /// speak to, which is every test and the registration's own default.
    /// </summary>
    public sealed class NullSoundSettingsApplier : ISoundSettingsApplier
    {
        public SoundSettings? Applied { get; private set; }

        public int ApplyCount { get; private set; }

        public void Apply(SoundSettings settings)
        {
            Applied = settings;
            ApplyCount++;
        }
    }

    /// <summary>
    /// A microphone test that tests nothing but remembers being asked. For
    /// containers with no audio, and for the game until the test's behaviour is
    /// decided — see <see cref="IMicrophoneTest"/>.
    /// </summary>
    public sealed class NullMicrophoneTest : IMicrophoneTest
    {
        public bool IsRunning { get; private set; }

        /// <summary>The device the last test was started on. For tests.</summary>
        public string StartedOn { get; private set; }

        public int StartCount { get; private set; }

        public void Start(string deviceName)
        {
            IsRunning = true;
            StartedOn = deviceName;
            StartCount++;
        }

        public void Stop() => IsRunning = false;
    }

    /// <summary>
    /// A fixed list of microphones. For tests, and for the registration's own
    /// default, where there is no machine to ask.
    /// </summary>
    public sealed class FixedMicrophoneDevices : IMicrophoneDevices
    {
        public FixedMicrophoneDevices(params string[] names)
        {
            Names = names ?? Array.Empty<string>();
        }

        public IReadOnlyList<string> Names { get; }
    }

    /// <summary>
    /// The 사운드 settings in force: remembering them, and carrying them to the
    /// mixer and the microphone.
    /// </summary>
    /// <remarks>
    /// Holds only what has been applied. The settings screen keeps its own
    /// draft while a slider is being dragged, and hands it here through
    /// <see cref="Apply"/> when 적용하기 is pressed — so a volume dragged and
    /// then abandoned costs nothing more than the draft going away.
    /// <para>
    /// Nothing is applied at construction, for the reason
    /// <see cref="GraphicsSettingsSystem"/> gives: what is saved has to reach
    /// the mixer when the game starts, but that is a step in startup, and a
    /// test that builds this must not change anybody's volume.
    /// </para>
    /// </remarks>
    public sealed class SoundSettingsSystem
    {
        private readonly ISoundSettingsStore store;
        private readonly ISoundSettingsApplier applier;
        private readonly IMicrophoneDevices devices;

        public SoundSettingsSystem(
            ISoundSettingsStore store,
            ISoundSettingsApplier applier = null,
            IMicrophoneDevices devices = null)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.applier = applier ?? new NullSoundSettingsApplier();
            this.devices = devices ?? new FixedMicrophoneDevices();
            Current = SoundCatalog.Normalise(
                store.TryLoad(out var saved) ? saved : SoundSettings.Empty,
                this.devices.Names);
        }

        public SoundSettings Current { get; private set; }

        /// <summary>
        /// What a player who has never opened the tab gets, and what 초기화
        /// puts back.
        /// </summary>
        public SoundSettings Defaults => SoundCatalog.Defaults;

        /// <summary>
        /// What the microphone picker offers right now: the default, then the
        /// machine's microphones by name. Asked afresh each time, because a
        /// headset may have arrived since the last look.
        /// </summary>
        public OptionChoices DeviceChoices => SoundCatalog.DeviceChoices(devices.Names);

        public event Action<SoundSettings> Changed;
        public event Action<SoundSettings> AudioChanged;

        /// <summary>Audition speaker volumes without saving the draft.</summary>
        public void Preview(SoundSettings draft)
        {
            var preview = Current
                .With(SoundVolume.Master, SoundCatalog.Clamp(draft.Get(SoundVolume.Master)))
                .With(SoundVolume.Music, SoundCatalog.Clamp(draft.Get(SoundVolume.Music)))
                .With(SoundVolume.Ambience, SoundCatalog.Clamp(draft.Get(SoundVolume.Ambience)))
                .With(SoundVolume.Effects, SoundCatalog.Clamp(draft.Get(SoundVolume.Effects)));
            applier.Apply(preview);
            AudioChanged?.Invoke(preview);
        }

        /// <summary>
        /// Settles on these values, writes them down and carries them to the
        /// audio. Nothing happens, and nobody is told, when they are already
        /// the current ones.
        /// </summary>
        public void Apply(SoundSettings settings)
        {
            var next = SoundCatalog.Normalise(settings, devices.Names);
            if (next == Current)
            {
                return;
            }

            Current = next;
            store.Save(next);

            // The audio before the listeners: what is heard should have changed
            // by the time anything reacts to the change.
            applier.Apply(next);
            AudioChanged?.Invoke(next);
            Changed?.Invoke(next);
        }

        /// <summary>
        /// Carries whatever is currently in force to the audio, without saving
        /// or telling anyone. For the application's startup.
        /// </summary>
        public void ApplyToAudio()
        {
            applier.Apply(Current);
            AudioChanged?.Invoke(Current);
        }
    }
}
