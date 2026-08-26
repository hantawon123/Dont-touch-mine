using Game.Core.Settings;

namespace Game.Client.Audio
{
    public interface IAudioSettingsApplier
    {
        void Apply(AudioSettingsState settings);
    }
}
