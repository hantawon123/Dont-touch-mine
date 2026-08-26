using System;
using Game.Core.Settings;

namespace Game.Client.Audio
{
    public interface IAudioSettings
    {
        AudioSettingsState Current { get; }

        event Action<AudioSettingsState> Changed;

        bool TrySetVolume(AudioChannel channel, int percent, out AudioSettingsError error);

        bool TrySetVoiceChatEnabled(bool enabled, out AudioSettingsError error);
    }
}
