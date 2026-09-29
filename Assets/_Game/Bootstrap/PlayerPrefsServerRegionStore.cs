using Game.Core.Ports;
using UnityEngine;

namespace Game.Bootstrap
{
    /// <summary>
    /// Keeps the chosen region in Unity's player preferences, beside the
    /// profile.
    /// </summary>
    /// <remarks>
    /// Preferences for the same reason <see cref="PlayerPrefsProfileStore"/>
    /// uses them: this is one short string, and a file would bring a format, a
    /// path and a migration story for no gain.
    /// </remarks>
    public sealed class PlayerPrefsServerRegionStore : IServerRegionStore
    {
        private const string RegionKey = "game.network.region";

        public bool TryLoad(out string code)
        {
            code = null;
            if (!PlayerPrefs.HasKey(RegionKey))
            {
                return false;
            }

            var saved = PlayerPrefs.GetString(RegionKey);
            if (string.IsNullOrWhiteSpace(saved))
            {
                return false;
            }

            code = saved;
            return true;
        }

        public void Save(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                // Keeping the last good code means a bad write cannot leave the
                // next run connecting somewhere nobody chose.
                Debug.LogWarning("[Network] Not saving a blank region code.");
                return;
            }

            PlayerPrefs.SetString(RegionKey, code.Trim());

            // Written through immediately: Unity flushes on a clean quit, and a
            // crash right after switching region is when losing it would be
            // most confusing.
            PlayerPrefs.Save();
        }
    }
}
