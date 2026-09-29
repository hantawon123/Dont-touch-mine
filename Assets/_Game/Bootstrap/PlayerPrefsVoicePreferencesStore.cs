using Game.Core.Ports;
using UnityEngine;

namespace Game.Bootstrap
{
    /// <summary>
    /// Keeps the microphone and speaker on/off in Unity's player preferences,
    /// beside the 사운드 tab.
    /// </summary>
    /// <remarks>
    /// Preferences for the same reason <see cref="PlayerPrefsSoundSettingsStore"/>
    /// uses them: two short switches, and a file would bring a format, a path
    /// and a migration story for no gain. One key per value, so adding a value
    /// later cannot invalidate what was saved before.
    /// </remarks>
    public sealed class PlayerPrefsVoicePreferencesStore : IVoicePreferencesStore
    {
        private const string MutedKey = "game.voice.muted";
        private const string ListeningKey = "game.voice.listening";

        public bool TryLoad(out bool muted, out bool listening)
        {
            muted = false;
            listening = true;
            var found = false;

            if (PlayerPrefs.HasKey(MutedKey))
            {
                muted = PlayerPrefs.GetInt(MutedKey) != 0;
                found = true;
            }

            if (PlayerPrefs.HasKey(ListeningKey))
            {
                listening = PlayerPrefs.GetInt(ListeningKey) != 0;
                found = true;
            }

            return found;
        }

        public void Save(bool muted, bool listening)
        {
            PlayerPrefs.SetInt(MutedKey, muted ? 1 : 0);
            PlayerPrefs.SetInt(ListeningKey, listening ? 1 : 0);

            // Written through immediately: Unity flushes on a clean quit, and a
            // crash right after toggling is when losing it would be most
            // confusing — the next room would come up with the other switch.
            PlayerPrefs.Save();
        }
    }
}
