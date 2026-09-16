using System;
using System.Collections.Generic;
using System.Text;

namespace Game.Backend
{
    /// <summary>
    /// Decides whether a chat message carries a forbidden word, and covers it (S15P21D205-1028).
    /// </summary>
    /// <remarks>
    /// The word list is never built into the player's game. The dedicated server asks the
    /// backend for it once at startup over a loopback path that a shared key locks; a list
    /// shipped in a build is a list of what to type around.
    /// <para>
    /// <b>This has to agree with the backend.</b> The backend judges the same message again
    /// when it stores it, and if the two disagree an investigator sees a message flagged as
    /// masked that the players saw in the clear, or the other way round. The rules below are
    /// the ones the backend applies (S15P21D205-1027); keep them in step.
    /// </para>
    /// </remarks>
    public sealed class ChatBlocklist
    {
        private const char MaskChar = '*';

        private readonly List<string> blocked = new();
        private readonly List<string> allowed = new();

        /// <summary>Starts empty, which forbids nothing. The list arrives later.</summary>
        /// <remarks>
        /// A server that could not reach the backend runs without filtering rather than
        /// refusing to open the room. A room that will not open is a game that will not start;
        /// a room without filtering is a room the reports still cover.
        /// </remarks>
        public bool IsLoaded { get; private set; }

        /// <summary>How many words came back. Zero means an answer arrived with nothing in it.</summary>
        public int WordCount => blocked.Count;

        public void Load(IEnumerable<string> words, IEnumerable<string> exceptions)
        {
            blocked.Clear();
            allowed.Clear();
            if (words != null)
                foreach (var word in words)
                    if (!string.IsNullOrWhiteSpace(word)) blocked.Add(word.Trim().ToLowerInvariant());
            if (exceptions != null)
                foreach (var word in exceptions)
                    if (!string.IsNullOrWhiteSpace(word)) allowed.Add(word.Trim().ToLowerInvariant());
            IsLoaded = true;
        }

        /// <summary>
        /// Does this message carry a forbidden word?
        /// </summary>
        /// <remarks>
        /// People stack their evasions. Digits alone and spacing alone each come apart with a
        /// single pass, but "시1 발" survives any pass that undoes only one of them, so the
        /// combinations are checked too.
        /// </remarks>
        public bool IsForbidden(string message)
        {
            if (blocked.Count == 0 || string.IsNullOrEmpty(message)) return false;

            var lower = message.ToLowerInvariant();
            var withoutDigits = RemoveDigits(lower);
            var leet = Leet(lower);

            return Hits(lower)
                || Hits(withoutDigits)
                || Hits(leet)
                || Hits(StripSymbols(withoutDigits))
                || Hits(StripSymbols(leet));
        }

        /// <summary>
        /// The message with forbidden words covered, or the message untouched when it is clean.
        /// </summary>
        /// <remarks>
        /// Words that appear as written are covered where they stand. A message that only
        /// tripped the filter through an evasion is covered whole: there is no way to say which
        /// characters carried it, and covering part of it leaves the rest perfectly readable.
        /// <para>
        /// The length is kept either way, so how much was said is still visible.
        /// </para>
        /// </remarks>
        public string Mask(string message)
        {
            if (!IsForbidden(message)) return message;

            // Not ToLowerInvariant: it changes length for some characters — U+0130 becomes two —
            // and a position found in that string then lands on the wrong character here, or
            // past the end. People can and do type those characters.
            var lower = AlignedLower(message);
            var masked = new StringBuilder(message);
            var covered = false;

            foreach (var bad in blocked)
            {
                var from = lower.IndexOf(bad, StringComparison.Ordinal);
                while (from >= 0)
                {
                    for (var index = from; index < from + bad.Length; index++) masked[index] = MaskChar;
                    covered = true;
                    from = lower.IndexOf(bad, from + bad.Length, StringComparison.Ordinal);
                }
            }

            return covered ? masked.ToString() : new string(MaskChar, message.Length);
        }

        private bool Hits(string candidate)
        {
            var stripped = candidate;
            foreach (var ok in allowed) stripped = stripped.Replace(ok, string.Empty);

            foreach (var bad in blocked)
            {
                if (IsAscii(bad) ? HasStandaloneAscii(stripped, bad) : stripped.Contains(bad))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Latin words have to stand alone; anything else is a plain containment test.
        /// </summary>
        /// <remarks>
        /// Korean has no word boundaries, so one rule cannot serve both. Requiring a boundary
        /// for Latin is what keeps "anal" out of "Analyst", and it is why short English words
        /// can be on the chat list at all when the nickname list had to leave them off.
        /// </remarks>
        private static bool HasStandaloneAscii(string candidate, string word)
        {
            var from = candidate.IndexOf(word, StringComparison.Ordinal);
            while (from >= 0)
            {
                var openLeft = from == 0 || !IsAsciiLetterOrDigit(candidate[from - 1]);
                var after = from + word.Length;
                var openRight = after == candidate.Length || !IsAsciiLetterOrDigit(candidate[after]);
                if (openLeft && openRight) return true;
                from = candidate.IndexOf(word, from + 1, StringComparison.Ordinal);
            }
            return false;
        }

        /// <summary>Digits that stand in for letters, the six people actually use.</summary>
        private static string Leet(string lower)
        {
            var letters = lower.ToCharArray();
            for (var index = 0; index < letters.Length; index++)
            {
                letters[index] = letters[index] switch
                {
                    '0' => 'o', '1' => 'i', '3' => 'e', '4' => 'a', '5' => 's', '7' => 't',
                    _ => letters[index]
                };
            }
            return new string(letters);
        }

        private static string RemoveDigits(string value)
        {
            var kept = new StringBuilder(value.Length);
            foreach (var letter in value)
                if (letter < '0' || letter > '9') kept.Append(letter);
            return kept.ToString();
        }

        /// <summary>Drops everything that is not a letter or a digit: "시 발", "시.발".</summary>
        private static string StripSymbols(string value)
        {
            var kept = new StringBuilder(value.Length);
            foreach (var letter in value)
                if (char.IsLetterOrDigit(letter)) kept.Append(letter);
            return kept.ToString();
        }

        /// <summary>One character down to one character, so positions still line up.</summary>
        private static string AlignedLower(string message)
        {
            var letters = message.ToCharArray();
            for (var index = 0; index < letters.Length; index++)
                letters[index] = char.ToLowerInvariant(letters[index]);
            return new string(letters);
        }

        private static bool IsAscii(string word)
        {
            foreach (var letter in word)
                if (letter > 0x7F) return false;
            return true;
        }

        private static bool IsAsciiLetterOrDigit(char letter) =>
            (letter >= 'a' && letter <= 'z') || (letter >= 'A' && letter <= 'Z')
            || (letter >= '0' && letter <= '9');
    }
}
