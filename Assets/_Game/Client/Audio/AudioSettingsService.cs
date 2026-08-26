using System;
using Game.Core.Settings;

namespace Game.Client.Audio
{
    public sealed class AudioSettingsService : IAudioSettings
    {
        private readonly IAudioSettingsStore store;
        private readonly IAudioSettingsApplier applier;

        public AudioSettingsService(
            IAudioSettingsStore store,
            IAudioSettingsApplier applier)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.applier = applier ?? throw new ArgumentNullException(nameof(applier));
            Current = store.LoadOrDefault() ?? new AudioSettingsState();
            ApplyCurrent();
        }

        public AudioSettingsState Current { get; }

        public event Action<AudioSettingsState> Changed;

        public bool TrySetVolume(
            AudioChannel channel,
            int percent,
            out AudioSettingsError error)
        {
            if (!Enum.IsDefined(typeof(AudioChannel), channel))
            {
                error = AudioSettingsError.InvalidChannel;
                return false;
            }

            if (Current.GetPercent(channel) == percent)
            {
                error = AudioSettingsError.None;
                return true;
            }

            if (!Current.TrySetVolume(channel, percent, out error))
            {
                return false;
            }

            PersistAndApply();
            return true;
        }

        public bool TrySetVoiceChatEnabled(bool enabled, out AudioSettingsError error)
        {
            if (Current.VoiceChatEnabled == enabled)
            {
                error = AudioSettingsError.None;
                return true;
            }

            if (!Current.TrySetVoiceChatEnabled(enabled, out error))
            {
                return false;
            }

            PersistAndApply();
            return true;
        }

        private void PersistAndApply()
        {
            store.Save(Current);
            ApplyCurrent();
            Changed?.Invoke(Current);
        }

        private void ApplyCurrent()
        {
            applier.Apply(Current);
            AudioSettingsOutput.Publish(Current);
        }
    }
}
