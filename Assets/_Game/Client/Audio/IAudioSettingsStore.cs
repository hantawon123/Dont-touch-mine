using Game.Core.Settings;

namespace Game.Client.Audio
{
    public interface IAudioSettingsStore
    {
        AudioSettingsState LoadOrDefault();

        void Save(AudioSettingsState settings);
    }
}
