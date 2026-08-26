using System;
using Game.Core.Settings;
using UnityEngine;

namespace Game.Client.Audio
{
    public sealed class PlayerPrefsAudioSettingsStore : IAudioSettingsStore
    {
        private const string MasterKey = "Game.Audio.Master";
        private const string BgmKey = "Game.Audio.Bgm";
        private const string SfxKey = "Game.Audio.Sfx";
        private const string UiKey = "Game.Audio.Ui";
        private const string VoiceKey = "Game.Audio.Voice";
        private const string MicKey = "Game.Audio.Mic";
        private const string VoiceChatKey = "Game.Audio.VoiceChat";

        public AudioSettingsState LoadOrDefault()
        {
            return new AudioSettingsState(
                PlayerPrefs.GetInt(MasterKey, AudioSettingsState.DefaultVolume),
                PlayerPrefs.GetInt(BgmKey, AudioSettingsState.DefaultVolume),
                PlayerPrefs.GetInt(SfxKey, AudioSettingsState.DefaultVolume),
                PlayerPrefs.GetInt(UiKey, AudioSettingsState.DefaultVolume),
                PlayerPrefs.GetInt(VoiceChatKey, 1) != 0,
                PlayerPrefs.GetInt(VoiceKey, AudioSettingsState.DefaultVolume),
                PlayerPrefs.GetInt(MicKey, AudioSettingsState.DefaultVolume));
        }

        public void Save(AudioSettingsState settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            PlayerPrefs.SetInt(MasterKey, settings.MasterVolume);
            PlayerPrefs.SetInt(BgmKey, settings.BgmVolume);
            PlayerPrefs.SetInt(SfxKey, settings.SfxVolume);
            PlayerPrefs.SetInt(UiKey, settings.UiVolume);
            PlayerPrefs.SetInt(VoiceKey, settings.VoiceVolume);
            PlayerPrefs.SetInt(MicKey, settings.MicVolume);
            PlayerPrefs.SetInt(VoiceChatKey, settings.VoiceChatEnabled ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
