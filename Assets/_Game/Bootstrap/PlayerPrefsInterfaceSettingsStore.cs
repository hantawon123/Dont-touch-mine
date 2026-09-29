using System;
using Game.Core.Ports;
using Game.Core.Settings;
using UnityEngine;

namespace Game.Bootstrap
{
    /// <summary>
    /// Keeps the 인터페이스 settings in Unity's player preferences, beside the
    /// other tabs'.
    /// </summary>
    /// <inheritdoc cref="PlayerPrefsGraphicsSettingsStore"/>
    public sealed class PlayerPrefsInterfaceSettingsStore : IInterfaceSettingsStore
    {
        private const string Prefix = "game.settings.interface.";

        /// <summary>
        /// What 스트리머 모드 was saved under before it was that.
        /// </summary>
        /// <remarks>
        /// 내 닉네임 표시 answered on, friends-only or off, and off meant the
        /// name was not shown at all. 스트리머 모드 asks the opposite question
        /// — whether to go by a made-up name — so a saved 끄기 has to arrive
        /// as 켜기 or somebody who had hidden their name would find it public
        /// again without touching anything.
        /// </remarks>
        private const string RetiredNicknameRow = Prefix + "OwnNickname";

        private static readonly InterfaceOption[] Options =
            (InterfaceOption[])Enum.GetValues(typeof(InterfaceOption));

        public bool TryLoad(out InterfaceSettings settings)
        {
            settings = InterfaceSettings.Empty;
            var found = false;

            foreach (var option in Options)
            {
                var key = Prefix + option;
                if (!PlayerPrefs.HasKey(key))
                {
                    continue;
                }

                var code = PlayerPrefs.GetString(key);
                if (string.IsNullOrWhiteSpace(code))
                {
                    continue;
                }

                settings = settings.With(option, code.Trim());
                found = true;
            }

            if (TryReadRetiredNickname(out var streaming))
            {
                settings = settings.With(InterfaceOption.StreamerMode, streaming);
                found = true;
            }

            return found;
        }

        /// <summary>
        /// 스트리머 모드 as the old row would have meant it, for a player who
        /// has not answered the new one.
        /// </summary>
        /// <remarks>
        /// Read rather than rewritten. The old key is left where it is and
        /// nothing is saved here — a load that wrote would put this store's
        /// opinion on disk before the player had opened the tab. It stops
        /// applying on its own the first time they settle the row, because
        /// that writes the new key and this only looks when the new key is
        /// missing.
        /// <para>
        /// Only the old 켜기 becomes 끄기. 친구만 limited who could see the
        /// name, so it lands on the side that keeps limiting it.
        /// </para>
        /// </remarks>
        private static bool TryReadRetiredNickname(out string streaming)
        {
            streaming = null;
            if (PlayerPrefs.HasKey(Prefix + InterfaceOption.StreamerMode)
                || !PlayerPrefs.HasKey(RetiredNicknameRow))
            {
                return false;
            }

            var retired = PlayerPrefs.GetString(RetiredNicknameRow).Trim();
            if (string.IsNullOrEmpty(retired))
            {
                return false;
            }

            streaming = string.Equals(retired, InterfaceCatalog.On, StringComparison.Ordinal)
                ? InterfaceCatalog.Off
                : InterfaceCatalog.On;
            return true;
        }

        public void Save(InterfaceSettings settings)
        {
            foreach (var option in Options)
            {
                var code = settings.Get(option);
                if (string.IsNullOrWhiteSpace(code))
                {
                    Debug.LogWarning($"[Settings] Not saving a blank {option}.");
                    continue;
                }

                PlayerPrefs.SetString(Prefix + option, code.Trim());
            }

            PlayerPrefs.Save();
        }
    }
}
