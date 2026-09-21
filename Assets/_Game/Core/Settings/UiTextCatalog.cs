using System;
using System.Collections.Generic;

namespace Game.Core.Settings
{
    /// <summary>
    /// One line the interface can draw, in the languages it has words for.
    /// Korean is required; any other language is optional and falls back to
    /// Korean until a translation lands.
    /// </summary>
    public readonly struct UiTextLine
    {
        private readonly Dictionary<string, string> byLanguage;

        /// <summary>A short, stable identifier that screens ask for.</summary>
        public string Key { get; }

        /// <summary>The default words, and what a missing translation shows.</summary>
        public string Korean { get; }

        /// <param name="korean">Always required. The catalogue's default.</param>
        /// <param name="english">
        /// Optional. Null or blank leaves the line in Korean when English is
        /// asked for, so a screen can be moved across in pieces.
        /// </param>
        public UiTextLine(string key, string korean, string english = null)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("A line needs a key.", nameof(key));
            }

            if (string.IsNullOrWhiteSpace(korean))
            {
                throw new ArgumentException("A line needs Korean words.", nameof(korean));
            }

            Key = key;
            Korean = korean;
            byLanguage = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ko"] = korean
            };

            if (!string.IsNullOrWhiteSpace(english))
            {
                byLanguage["en"] = english;
            }
        }

        public bool TryGet(string languageCode, out string text)
        {
            if (!string.IsNullOrWhiteSpace(languageCode)
                && byLanguage.TryGetValue(languageCode, out text))
            {
                return true;
            }

            text = null;
            return false;
        }
    }

    /// <summary>
    /// The words the interface can draw, looked up by key and language.
    /// </summary>
    /// <remarks>
    /// Data rather than a fixed list, so a test can hand a couple of lines
    /// and watch the lookup; the game itself uses <see cref="Shipped"/>.
    /// Screens add their keys here in the same change that starts reading
    /// them, grouped as <see cref="UiText"/> constants.
    /// </remarks>
    public sealed class UiTextCatalog
    {
        private readonly Dictionary<string, UiTextLine> lines;

        /// <summary>
        /// What the game ships with. The settings chrome is here; other
        /// screens add their keys when they start reading them. A missing
        /// key returns the key itself so a forgotten line is visible
        /// rather than blank.
        /// </summary>
        public static UiTextCatalog Shipped { get; } = new UiTextCatalog(
            new UiTextLine(UiText.Settings.Language, "언어", "Language"),
            new UiTextLine(UiText.Settings.Apply, "적용하기", "Apply"),
            new UiTextLine(UiText.Settings.Reset, "변경 취소", "Discard Changes"),
            new UiTextLine(UiText.Settings.LeaveGame, "게임 나가기", "Leave Game"),
            new UiTextLine(UiText.Settings.TabGeneral, "일반", "General"),
            new UiTextLine(UiText.Settings.TabGraphics, "그래픽", "Graphics"),
            new UiTextLine(UiText.Settings.TabInterface, "인터페이스", "Interface"),
            new UiTextLine(UiText.Settings.TabSound, "사운드", "Sound"),
            new UiTextLine(UiText.Settings.TabControls, "컨트롤", "Controls"),
            new UiTextLine(UiText.Settings.TabNotifications, "알림", "Notifications"));

        public UiTextCatalog(params UiTextLine[] lines)
        {
            this.lines = new Dictionary<string, UiTextLine>(StringComparer.Ordinal);
            if (lines == null)
            {
                return;
            }

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line.Key))
                {
                    throw new ArgumentException("A catalogue cannot hold a line without a key.", nameof(lines));
                }

                if (this.lines.ContainsKey(line.Key))
                {
                    throw new ArgumentException(
                        $"'{line.Key}' is already in the catalogue.", nameof(lines));
                }

                this.lines[line.Key] = line;
            }
        }

        /// <summary>
        /// The words for <paramref name="key"/> in
        /// <paramref name="languageCode"/>. An unknown language, or a
        /// language this line has no words for, uses Korean. A key the
        /// catalogue does not list comes back as itself.
        /// </summary>
        public string Get(string key, string languageCode)
        {
            if (key == null)
            {
                return string.Empty;
            }

            if (!lines.TryGetValue(key, out var line))
            {
                return key;
            }

            return line.TryGet(languageCode, out var text) ? text : line.Korean;
        }
    }
}
