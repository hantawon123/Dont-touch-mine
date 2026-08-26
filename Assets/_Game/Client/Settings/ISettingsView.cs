using System;
using Game.Core.Settings;

namespace Game.Client.Settings
{
    public interface ISettingsView
    {
        event Action BackRequested;

        event Action<SettingsTab> TabSelected;

        event Action<AudioChannel, int> AudioVolumeChanged;

        event Action<bool> VoiceChatEnabledChanged;

        event Action<int> UiScaleChanged;

        event Action<int> TextScaleChanged;

        event Action<bool> HighContrastChanged;

        void SetActiveTab(SettingsTab tab);

        void SetAudioSettings(AudioSettingsState settings);

        void SetAccessibilitySettings(AccessibilitySettingsState settings);
    }
}
