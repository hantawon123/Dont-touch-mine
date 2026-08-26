using Game.Core.Ports;
using UnityEngine;

namespace Game.Bootstrap
{
    /// <summary>
    /// Keeps the local profile in Unity's player preferences.
    /// </summary>
    /// <remarks>
    /// Preferences rather than a file: this is one nickname and one level, and a
    /// file would bring a format, a path, and a migration story for no gain. When
    /// a Steam or backend profile arrives it replaces this class, not its callers.
    /// </remarks>
    public sealed class PlayerPrefsProfileStore : IProfileStore
    {
        /// <summary>
        /// Prefixed because preferences are shared by everything the player runs
        /// from this company, and an unprefixed "nickname" would collide.
        /// </summary>
        private const string NicknameKey = "game.profile.nickname";

        private const string LevelKey = "game.profile.level";

        public bool TryLoad(out string nickname, out int level)
        {
            nickname = null;
            level = 0;

            if (!PlayerPrefs.HasKey(NicknameKey))
            {
                return false;
            }

            var saved = PlayerPrefs.GetString(NicknameKey);

            if (string.IsNullOrWhiteSpace(saved))
            {
                // A blank saved name is the same as none. Handing it back would
                // fail PlayerProfile's own check and take the application down on
                // a value this machine wrote.
                return false;
            }

            nickname = saved;

            // Defaulted rather than required. A level lost while a nickname
            // survives should not discard the nickname too.
            level = Mathf.Max(1, PlayerPrefs.GetInt(LevelKey, 1));
            return true;
        }

        public void Save(string nickname, int level)
        {
            if (string.IsNullOrWhiteSpace(nickname))
            {
                // Refusing to store a blank keeps the last good name, so a bad
                // write cannot leave the next run without one.
                Debug.LogWarning("[Profile] Not saving a blank nickname.");
                return;
            }

            PlayerPrefs.SetString(NicknameKey, nickname.Trim());
            PlayerPrefs.SetInt(LevelKey, Mathf.Max(1, level));

            // Written through immediately. Unity flushes on a clean quit, and a
            // crash after a rename is exactly when losing it would be noticed.
            PlayerPrefs.Save();
        }
    }
}
