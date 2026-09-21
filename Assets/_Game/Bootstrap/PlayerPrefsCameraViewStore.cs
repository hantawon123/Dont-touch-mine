using Game.Core.Ports;
using UnityEngine;

namespace Game.Bootstrap
{
    /// <summary>
    /// Keeps first person versus third person in Unity's player preferences,
    /// beside the 컨트롤 tab.
    /// </summary>
    /// <remarks>
    /// Preferences for the same reason <see cref="PlayerPrefsVoicePreferencesStore"/>
    /// uses them: one short switch, and a file would bring a format, a path
    /// and a migration story for no gain.
    /// </remarks>
    public sealed class PlayerPrefsCameraViewStore : ICameraViewStore
    {
        private const string FirstPersonKey = "game.camera.firstPerson";

        public bool TryLoad(out bool firstPerson)
        {
            firstPerson = false;
            if (!PlayerPrefs.HasKey(FirstPersonKey))
            {
                return false;
            }

            firstPerson = PlayerPrefs.GetInt(FirstPersonKey) != 0;
            return true;
        }

        public void Save(bool firstPerson)
        {
            PlayerPrefs.SetInt(FirstPersonKey, firstPerson ? 1 : 0);

            // Written through immediately: Unity flushes on a clean quit, and a
            // crash right after toggling is when losing it would be most
            // confusing — the next room would come up in the other view.
            PlayerPrefs.Save();
        }
    }
}
