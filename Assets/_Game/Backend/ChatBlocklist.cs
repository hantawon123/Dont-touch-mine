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
        /// The five readings of a message the judgement is made against.
        /// </summary>
        /// <remarks>
        /// Internal so the tests can build the same readings and put the two judgements —
        /// the automaton's and the plain scan's — side by side without stating the list twice.
        /// </remarks>
        internal static string[] Variants(string message)
        {
            var lower = message.ToLowerInvariant();
            var withoutDigits = RemoveDigits(lower);
            var leet = Leet(lower);
            return new[]
            {
                lower, withoutDigits, leet, StripSymbols(withoutDigits), StripSymbols(leet)
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
