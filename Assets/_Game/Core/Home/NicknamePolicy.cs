using System.Text;

namespace Game.Core.Home
{
    /// <summary>
    /// What a nickname may be: Hangul, Latin letters and digits, 2 to 12 of
    /// them. No spaces, no punctuation.
    /// </summary>
    /// <remarks>
    /// The same rule as the server's <c>NicknamePolicy</c>, written out here so
    /// the client can refuse a name before spending a round trip on it. The
    /// server stays the authority; this only saves the trip.
    /// <para>
    /// Length counts the way <c>string.Length</c> counts. Every allowed
    /// character is a single UTF-16 unit, so twelve characters is twelve units
    /// and an emoji cannot arrive to spoil that.
    /// </para>
    /// </remarks>
    public static class NicknamePolicy
    {
        public const int MinLength = 2;
        public const int MaxLength = 12;

        public static bool IsValid(string nickname)
        {
            if (nickname == null
                || nickname.Length < MinLength
                || nickname.Length > MaxLength)
            {
                return false;
            }

            foreach (var character in nickname)
            {
                if (!IsAllowed(character))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Complete syllables only. A name of bare jamo — "ㅋㅋㅋㅋ" — is noise
        /// rather than a name, and the server refuses it.
        /// </summary>
        public static bool IsAllowed(char character)
        {
            return IsDigitOrLatin(character) || (character >= '가' && character <= '힣');
        }

        /// <summary>
        /// Also lets through the bare jamo an IME shows mid-composition.
        /// </summary>
        /// <remarks>
        /// A Korean keyboard hands over a jamo per keystroke and only settles
        /// into a syllable once the next one arrives. Filtering those out as
        /// they are typed makes the field look broken, so they are allowed into
        /// the field and refused by <see cref="IsValid"/> if any survive to the
        /// moment the name is used.
        /// </remarks>
        public static bool IsAllowedWhileTyping(char character)
        {
            return IsAllowed(character)
                || (character >= 'ᄀ' && character <= 'ᇿ')
                || (character >= 'ㄱ' && character <= 'ㆎ');
        }

        /// <summary>
        /// Cuts a typed string down to what may stay in the field, and says
        /// which rule did the cutting.
        /// </summary>
        /// <remarks>
        /// Refusing by removing rather than by rejecting the whole edit is what
        /// lets the field keep the good half of a paste. Both flags can come
        /// back set at once, and the caller decides which to say out loud.
        /// </remarks>
        public static string Filter(string typed, out bool hadBadCharacter, out bool wasTooLong)
        {
            hadBadCharacter = false;
            wasTooLong = false;

            if (string.IsNullOrEmpty(typed))
            {
                return string.Empty;
            }

            var accepted = new StringBuilder(typed.Length);
            foreach (var character in typed)
            {
                if (!IsAllowedWhileTyping(character))
                {
                    hadBadCharacter = true;
                    continue;
                }

                if (accepted.Length == MaxLength)
                {
                    wasTooLong = true;
                    continue;
                }

                accepted.Append(character);
            }

            return accepted.ToString();
        }

        private static bool IsDigitOrLatin(char character)
        {
            return (character >= '0' && character <= '9')
                || (character >= 'a' && character <= 'z')
                || (character >= 'A' && character <= 'Z');
        }
    }
}
