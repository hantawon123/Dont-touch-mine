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
        private Automaton automaton = Automaton.Of(new List<string>());

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
            // Built once, here, because the list never changes after this: a room server fetches
            // it before the room opens and quits when the room empties.
            automaton = Automaton.Of(blocked);
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

            foreach (var variant in Variants(message))
                if (Hits(variant)) return true;
            return false;
        }

        /// <summary>
        /// The twelve readings of a message the judgement is made against.
        /// </summary>
        /// <remarks>
        /// Internal so the tests can build the same readings and put the two judgements —
        /// the automaton's and the plain scan's — side by side without stating the list twice.
        /// <para>
        /// <b>The backend builds the same twelve</b> (ChatBlocklist.variants, S15P21D205-1081).
        /// The first five undo symbols and digits; the last seven undo jamo, which used to be a
        /// hole wide enough that a single character walked through it — "시ㅣ발" went out in the
        /// clear. <see cref="Hangul"/> says which jamo tricks are undone and which were measured
        /// and left alone.
        /// </para>
        /// <para>
        /// Only the digit-as-vowel reading starts from a string that still has its digits. There
        /// is nothing left to read once they are gone.
        /// </para>
        /// </remarks>
        internal static string[] Variants(string message)
        {
            var lower = message.ToLowerInvariant();
            var withoutDigits = RemoveDigits(lower);
            var leet = Leet(lower);
            var plain = StripSymbols(withoutDigits);
            var plainLeet = StripSymbols(leet);
            return new[]
            {
                lower, withoutDigits, leet, plain, plainLeet,
                Hangul.StripJamo(plain), Hangul.StripJamo(plainLeet),
                Hangul.Rejoin(plain), Hangul.Rejoin(plainLeet),
                Hangul.DigitAsVowel(StripSymbols(lower)),
                Hangul.Unstretch(plain), Hangul.Unstretch(plainLeet)
            };
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
        /// <para>
        /// The automaton finds the words, for the same reason the judgement uses it: searching
        /// the text once per word costs time in proportion to the list. It also covers <b>more</b>
        /// than the old scan did, in one narrow case — a word that can overlap itself, of which
        /// the list has 26 (딸딸, tit, poop, …). The scan skipped past each hit by the word's
        /// length, so "딸딸딸" came back "**딸"; the automaton sees the second hit too and covers
        /// all three. Covering never shrinks, and leaving a piece of the word readable was never
        /// the intent. <see cref="MaskByScan"/> keeps the old behaviour for the tests to compare
        /// against.
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
            var covered = automaton.CoverInto(lower, masked, MaskChar);

            return covered ? masked.ToString() : new string(MaskChar, message.Length);
        }

        /// <summary>The same covering written the obvious way. Only the tests call it.</summary>
        internal string MaskByScan(string message)
        {
            if (!IsForbidden(message)) return message;

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

        /// <summary>
        /// Is a forbidden word in this text, once the allowed words are taken out?
        /// </summary>
        /// <remarks>
        /// The judgement is the automaton's, which walks the text once instead of searching it
        /// for every word on the list. With 41 words either way costs nothing; at 3860 the
        /// per-word scan reached 0.77ms a message and this is about 0.004ms. The rule itself is
        /// unchanged — <see cref="HasStandaloneAscii"/> is kept as the plain statement of it,
        /// and the EditMode tests hold the two against each other.
        /// </remarks>
        private bool Hits(string candidate)
        {
            var stripped = candidate;
            foreach (var ok in allowed) stripped = stripped.Replace(ok, string.Empty);
            return automaton.ContainsWord(stripped);
        }

        /// <summary>The same judgement written the obvious way. Only the tests call it.</summary>
        internal bool HitsByScan(string candidate)
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

        /// <summary>
        /// Puts a message written with jamo back into the shape the word list is written in.
        /// </summary>
        /// <remarks>
        /// The backend has the same four in Java (HangulShapes, S15P21D205-1081) and the two have
        /// to answer identically, so the rules are written out here rather than approximated.
        /// <para>
        /// Each one covers a different trick and none of them subsumes another. Dropping jamo
        /// catches "시ㅣ발" and "시ㅋㅋ발" but not "ㅅㅣ발", where dropping leaves nothing but the
        /// last syllable. Joining jamo back into syllables catches "ㅅㅣ발" and "ㅂㅕㅇ신" but not
        /// "시ㅋㅋ발", where the stray ㅋ is taken as a final consonant and the word becomes
        /// "싴발".
        /// </para>
        /// <para>
        /// <b>Three more were built, measured and left out</b>, each against 16430 lines of Korean
        /// prose: collapsing repeated jamo (12 lines newly flagged — "롤리팝" becomes "로리팝",
        /// and the case for it is already covered by dropping jamo), collapsing a stretched vowel
        /// the first time it repeats (no new lines, but it only adds "꺼어져" and "닥아쳐" —
        /// the single-stretch words are caught by other readings already — while leaving "강아지"
        /// reduced to "강지"), and reading Latin letters as jamo (80 lines — it would catch
        /// "시bal" but puts every English word in range). Mixed spellings like "시bal" are listed
        /// by hand instead.
        /// </para>
        /// <para>
        /// It lives in this file for the same reason the automaton does: the rule and the code
        /// that runs it stay together, and the asset needs no second meta file.
        /// </para>
        /// </remarks>
        private static class Hangul
        {
            private const char SyllableFirst = (char)0xAC00;
            private const char SyllableLast = (char)0xD7A3;

            private const string Lead = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";
            private const string Vowel = "ㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣ";

            /// <summary>Final consonants. Slot 0 is "no final" and never read as a character.</summary>
            private const string Tail = "_ㄱㄲㄳㄴㄵㄶㄷㄹㄺㄻㄼㄽㄾㄿㅀㅁㅂㅄㅅㅆㅇㅈㅊㅋㅌㅍㅎ";

            /// <summary>Drops jamo standing on their own: "시ㅣㅣㅣ발" → "시발".</summary>
            /// <remarks>
            /// Composed first, so a word typed as conjoining jamo becomes syllables instead of
            /// disappearing. ㅣ is a letter, not a symbol, which is why stripping symbols left it
            /// in place and this is needed at all.
            /// </remarks>
            public static string StripJamo(string value)
            {
                var composed = value.Normalize(NormalizationForm.FormC);
                var kept = new StringBuilder(composed.Length);
                foreach (var letter in composed)
                    if (!IsJamo(letter)) kept.Append(letter);
                return kept.ToString();
            }

            /// <summary>Pulls syllables apart into jamo and puts them back: "ㅅㅣ발" → "시발".</summary>
            public static string Rejoin(string value) => Join(Flatten(value));

            /// <summary>
            /// Reads a digit right after a lone consonant as a vowel: "ㅅ1발" → "시발".
            /// </summary>
            /// <remarks>
            /// Only right after a lone consonant. Taking any digit next to Hangul turns "롤1인칭"
            /// into "로리인칭", which trips on "로리"; after a consonant is exactly where someone
            /// draws a vowel with a digit.
            /// </remarks>
            public static string DigitAsVowel(string value)
            {
                var letters = value.ToCharArray();
                for (var index = 1; index < letters.Length; index++)
                {
                    var vowel = LookalikeVowel(letters[index]);
                    if (vowel != 0 && Lead.IndexOf(letters[index - 1]) >= 0) letters[index] = vowel;
                }
                return Rejoin(new string(letters));
            }

            /// <summary>
            /// Shortens a vowel drawn out with ㅇ: "시이이발" → "시발".
            /// </summary>
            /// <remarks>
            /// Only from the second repeat. Shortening the first one turns "강아지" into "강지"
            /// and buys almost nothing — the words people actually stretch once are caught by
            /// other readings. A
            /// syllable carrying a final consonant is never read as drawn out — removing it would
            /// push that consonant onto the syllable before it and invent a word.
            /// </remarks>
            public static string Unstretch(string value)
            {
                var composed = value.Normalize(NormalizationForm.FormC);
                var kept = new StringBuilder(composed.Length);
                var held = new StringBuilder();
                var previousVowel = (char)0;
                foreach (var letter in composed)
                {
                    if (IsSyllable(letter))
                    {
                        var code = letter - SyllableFirst;
                        var lead = Lead[code / 588];
                        var vowel = Vowel[code % 588 / 28];
                        if (lead == 'ㅇ' && vowel == previousVowel && code % 28 == 0)
                        {
                            held.Append(letter);
                            continue;
                        }
                        previousVowel = vowel;
                    }
                    else
                    {
                        previousVowel = (char)0;
                    }
                    Flush(kept, held);
                    kept.Append(letter);
                }
                Flush(kept, held);
                return Rejoin(kept.ToString());
            }

            /// <summary>
            /// Pulls syllables apart into jamo, leaving everything else alone.
            /// </summary>
            /// <remarks>
            /// NFKC first, to pull full-width characters back to their plain forms. That turns
            /// standalone jamo into their conjoining forms, so <see cref="ToCompat"/> puts them
            /// back — otherwise the ㅅ a person typed and the ㅅ taken out of a syllable are two
            /// different characters and never match.
            /// </remarks>
            private static string Flatten(string value)
            {
                var normalized = value.Normalize(NormalizationForm.FormKC);
                var flat = new StringBuilder(normalized.Length * 3);
                foreach (var raw in normalized)
                {
                    var letter = ToCompat(raw);
                    if (IsSyllable(letter))
                    {
                        var code = letter - SyllableFirst;
                        flat.Append(Lead[code / 588]);
                        flat.Append(Vowel[code % 588 / 28]);
                        var tail = code % 28;
                        if (tail > 0) flat.Append(Tail[tail]);
                    }
                    else
                    {
                        flat.Append(letter);
                    }
                }
                return flat.ToString();
            }

            /// <summary>
            /// Builds syllables back out of jamo. Jamo that cannot join are dropped.
            /// </summary>
            /// <remarks>
            /// Whether a consonant is taken as a final is decided by what follows it: a vowel
            /// after it means it opens the next syllable instead. "ㅂㅏㄹㅏ" is "바라", not "발ㅏ".
            /// </remarks>
            private static string Join(string jamo)
            {
                var built = new StringBuilder(jamo.Length);
                var index = 0;
                while (index < jamo.Length)
                {
                    var letter = jamo[index];
                    var lead = Lead.IndexOf(letter);
                    var vowel = index + 1 < jamo.Length ? Vowel.IndexOf(jamo[index + 1]) : -1;
                    if (lead >= 0 && vowel >= 0)
                    {
                        var tail = 0;
                        if (index + 2 < jamo.Length)
                        {
                            var candidate = Tail.IndexOf(jamo[index + 2]);
                            var nextIsVowel = index + 3 < jamo.Length && Vowel.IndexOf(jamo[index + 3]) >= 0;
                            if (candidate > 0 && !nextIsVowel) tail = candidate;
                        }
                        built.Append((char)(SyllableFirst + (lead * 21 + vowel) * 28 + tail));
                        index += tail > 0 ? 3 : 2;
                        continue;
                    }
                    if (!IsJamo(letter)) built.Append(letter);
                    index++;
                }
                return built.ToString();
            }

            /// <summary>Conjoining jamo back to the standalone forms, undoing what NFKC did.</summary>
            private static char ToCompat(char letter)
            {
                if (letter >= 0x1100 && letter <= 0x1112) return Lead[letter - 0x1100];
                if (letter >= 0x1161 && letter <= 0x1175) return Vowel[letter - 0x1161];
                if (letter >= 0x11A8 && letter <= 0x11C2) return Tail[letter - 0x11A8 + 1];
                return letter;
            }

            /// <summary>Digits and letters drawn as vowels, the ones people actually use.</summary>
            private static char LookalikeVowel(char letter) => letter switch
            {
                '1' or 'l' or 'i' or '|' => 'ㅣ',
                '0' or 'o' => 'ㅗ',
                _ => (char)0
            };

            /// <summary>Fewer than two was not a drawn-out vowel after all, so put it back.</summary>
            private static void Flush(StringBuilder kept, StringBuilder held)
            {
                if (held.Length < 2) kept.Append(held);
                held.Length = 0;
            }

            private static bool IsSyllable(char letter) =>
                letter >= SyllableFirst && letter <= SyllableLast;

            private static bool IsJamo(char letter) =>
                (letter >= 0x3131 && letter <= 0x318E) || (letter >= 0x1100 && letter <= 0x11FF)
                || (letter >= 0xA960 && letter <= 0xA97C) || (letter >= 0xD7B0 && letter <= 0xD7FB);
        }

        /// <summary>
        /// Every forbidden word in one trie, walked once (Aho-Corasick).
        /// </summary>
        /// <remarks>
        /// Searching the text once for each word costs time in proportion to the list; this
        /// costs time in proportion to the message and nothing else. The backend has the same
        /// structure in Java (WordAutomaton), and the two have to answer identically — a message
        /// recorded as masked that the players read in the clear is worse than no record.
        /// <para>
        /// It lives in this file rather than its own so that the rule and the machine that runs
        /// it stay in one place, and so the asset does not need a second meta file.
        /// </para>
        /// </remarks>
        private sealed class Automaton
        {
            private static readonly int[] None = new int[0];

            private readonly Node root = new();

            public static Automaton Of(List<string> words)
            {
                var automaton = new Automaton();
                foreach (var word in words)
                    if (!string.IsNullOrEmpty(word)) automaton.Add(word);
                automaton.Link();
                return automaton;
            }

            /// <summary>Same answer as walking the list with the rule written out.</summary>
            public bool ContainsWord(string candidate)
            {
                var node = root;
                for (var index = 0; index < candidate.Length; index++)
                {
                    node = Step(node, candidate[index]);
                    if (node.EndsFree) return true;
                    foreach (var length in node.AsciiLengths)
                    {
                        var from = index - length + 1;
                        var openLeft = from == 0 || !IsAsciiLetterOrDigit(candidate[from - 1]);
                        var openRight = index + 1 == candidate.Length
                            || !IsAsciiLetterOrDigit(candidate[index + 1]);
                        if (openLeft && openRight) return true;
                    }
                }
                return false;
            }

            /// <summary>
            /// Covers every forbidden word found in <paramref name="candidate"/>, and says whether
            /// anything was covered.
            /// </summary>
            /// <remarks>
            /// <b>No word boundary here, unlike the judgement.</b> Covering runs only on a message
            /// already judged forbidden, and at that point the question is which characters to
            /// hide, not whether the message is clean — so "anal" inside a longer run of letters
            /// is covered as the old scan covered it.
            /// <para>
            /// <paramref name="canvas"/> has to be the same length as <paramref name="candidate"/>,
            /// which is why the caller lowercases one character to one character.
            /// </para>
            /// </remarks>
            public bool CoverInto(string candidate, StringBuilder canvas, char mark)
            {
                var covered = false;
                var node = root;
                for (var index = 0; index < candidate.Length; index++)
                {
                    node = Step(node, candidate[index]);
                    foreach (var length in node.AllLengths)
                    {
                        for (var at = index - length + 1; at <= index; at++) canvas[at] = mark;
                        covered = true;
                    }
                }
                return covered;
            }

            private void Add(string word)
            {
                var node = root;
                foreach (var letter in word)
                {
                    if (!node.Next.TryGetValue(letter, out var child))
                    {
                        child = new Node();
                        node.Next[letter] = child;
                    }
                    node = child;
                }
                node.OwnAll.Add(word.Length);
                if (IsAscii(word)) node.OwnAscii.Add(word.Length);
                else node.OwnFree = true;
            }

            /// <summary>
            /// Adds the fail links, and folds what they reach into each node.
            /// </summary>
            /// <remarks>
            /// Folding now means the judgement never has to climb the links. Breadth-first order
            /// guarantees a node's fail target is already folded when its turn comes.
            /// </remarks>
            private void Link()
            {
                var queue = new Queue<Node>();
                root.Fail = root;
                foreach (var child in root.Next.Values)
                {
                    child.Fail = root;
                    queue.Enqueue(child);
                }
                while (queue.Count > 0)
                {
                    var node = queue.Dequeue();
                    node.EndsFree = node.OwnFree || node.Fail.EndsFree;
                    node.AsciiLengths = Merge(node.OwnAscii, node.Fail.AsciiLengths);
                    node.AllLengths = Merge(node.OwnAll, node.Fail.AllLengths);
                    foreach (var pair in node.Next)
                    {
                        var fail = node.Fail;
                        while (fail != root && !fail.Next.ContainsKey(pair.Key)) fail = fail.Fail;
                        pair.Value.Fail = fail.Next.TryGetValue(pair.Key, out var target)
                            && target != pair.Value ? target : root;
                        queue.Enqueue(pair.Value);
                    }
                }
            }

            private Node Step(Node from, char letter)
            {
                var node = from;
                while (node != root && !node.Next.ContainsKey(letter)) node = node.Fail;
                return node.Next.TryGetValue(letter, out var target) ? target : root;
            }

            private static int[] Merge(List<int> own, int[] inherited)
            {
                if (own.Count == 0) return inherited;
                var all = new List<int>(own);
                foreach (var length in inherited)
                    if (!all.Contains(length)) all.Add(length);
                return all.ToArray();
            }

            private sealed class Node
            {
                public readonly Dictionary<char, Node> Next = new();
                public readonly List<int> OwnAscii = new();
                public readonly List<int> OwnAll = new();
                public Node Fail;
                public bool OwnFree;

                /// <summary>A word needing no boundary ends here, fail links included.</summary>
                public bool EndsFree;

                /// <summary>Lengths of the words that do need one, fail links included.</summary>
                public int[] AsciiLengths = None;

                /// <summary>
                /// Lengths of every word ending here, boundary or not. Covering uses this.
                /// </summary>
                public int[] AllLengths = None;
            }
        }
    }
}
