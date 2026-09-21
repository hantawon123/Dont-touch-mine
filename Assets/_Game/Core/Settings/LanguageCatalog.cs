using System;
using System.Collections.Generic;

namespace Game.Core.Settings
{
    /// <summary>
    /// One language the interface can be drawn in.
    /// </summary>
    public readonly struct Language
    {
        /// <summary>A short, stable identifier that is what gets saved.</summary>
        public string Code { get; }

        /// <summary>The name shown in the picker, written in itself.</summary>
        public string Label { get; }

        public Language(string code, string label)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("A language needs a code.", nameof(code));
            }

            Code = code;
            Label = label ?? throw new ArgumentNullException(nameof(label));
        }
    }

    /// <summary>
    /// The languages the interface can be shown in, in the order the picker
    /// steps through them. The first is the default.
    /// </summary>
    /// <remarks>
    /// Data rather than a fixed list, so a test can hand the screen two
    /// languages and watch the picker move; the game itself uses
    /// <see cref="Shipped"/>.
    /// </remarks>
    public sealed class LanguageCatalog
    {
        private readonly Language[] languages;

        /// <summary>
        /// What the game ships with. Korean is first, so it is the default.
        /// English is offered in the picker; the rest of the interface stays
        /// Korean until a translation lands.
        /// </summary>
        public static LanguageCatalog Shipped { get; } =
            new LanguageCatalog(
                new Language("ko", "한국어"),
                new Language("en", "English"));

        public LanguageCatalog(params Language[] languages)
        {
            if (languages == null || languages.Length == 0)
            {
                throw new ArgumentException("A catalogue needs at least one language.", nameof(languages));
            }

            this.languages = (Language[])languages.Clone();
        }

        public IReadOnlyList<Language> All => languages;

        public Language Default => languages[0];

        /// <summary>
        /// Whether the picker has anywhere to go. False with one language, so
        /// the screen can draw its arrows as unavailable rather than as buttons
        /// that do nothing.
        /// </summary>
        public bool CanStep => languages.Length > 1;

        public bool TryFind(string code, out Language language)
        {
            var index = IndexOf(code);
            language = index >= 0 ? languages[index] : default;
            return index >= 0;
        }

        /// <summary>
        /// The language <paramref name="steps"/> places along from
        /// <paramref name="code"/>, wrapping at either end. A code that is not
        /// listed starts from the default, so a stale saved value cannot leave
        /// the picker stuck.
        /// </summary>
        public Language Step(string code, int steps)
        {
            var from = Math.Max(IndexOf(code), 0);
            var count = languages.Length;
            var to = (((from + steps) % count) + count) % count;
            return languages[to];
        }

        private int IndexOf(string code)
        {
            for (var index = 0; index < languages.Length; index++)
            {
                if (string.Equals(languages[index].Code, code, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
