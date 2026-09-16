namespace Game.Core.Settings
{
    /// <summary>
    /// The made-up name somebody in 스트리머 모드 is known by.
    /// </summary>
    /// <remarks>
    /// Shaped exactly like the nickname the server hands a new account —
    /// <c>BoldFox4821</c>, two short English words and four digits — on
    /// purpose. The room must not be able to tell a pseudonym from a real
    /// name (S15P21D205-1018); a name that announced itself would single the
    /// streamer out, which is the opposite of what the mode is for. Whether a
    /// name is a pseudonym travels beside it as
    /// <c>PlayerAvatarNaming.Pseudonymous</c>, so nothing needs to read it off
    /// the text. Until 2026-09-16 the names began with 익명 and a check for
    /// that prefix existed; both are gone.
    /// <para>
    /// The word lists are the server's
    /// (<c>backend/app/.../NicknameGenerator.java</c>), same words, same
    /// order. Change one and change the other; two codebases cannot share a
    /// file, so this comment is the link.
    /// </para>
    /// <para>
    /// Made from a seed rather than at random, so the same seed is the same
    /// name. What varies the name between visits is the seed, which
    /// <c>NetworkInterfaceSettings</c> makes afresh each time a player joins a
    /// room; the name then holds for as long as they stay, so the room can
    /// still talk to them.
    /// </para>
    /// <para>
    /// The owner works theirs out and sends it, rather than everybody deriving
    /// it from an account id. Two peers deriving it separately would have to
    /// agree on the seed as well, and the room already has a way to carry one
    /// short string from a player to everybody else.
    /// </para>
    /// <para>
    /// A pseudonym can coincide with somebody's real default nickname. With
    /// 2.3 million combinations and at most six people in a room that is a
    /// one-in-a-hundred-thousand visit, and nothing breaks when it happens:
    /// names are for people to read, ids are what the code compares.
    /// </para>
    /// </remarks>
    public static class Pseudonym
    {
        /// <summary>
        /// What every name looks like — for tests, and for any screen that
        /// wants to sanity-check one. Two capitalised words of three or four
        /// letters and four digits.
        /// </summary>
        public const string Shape = "^[A-Z][a-z]{2,3}[A-Z][a-z]{2,3}[0-9]{4}$";

        /// <summary>Same list as the server's ADJECTIVES.</summary>
        private static readonly string[] Adjectives =
        {
            "Bold", "Calm", "Cool", "Fast", "Kind", "Wild", "Warm", "Soft",
            "Tiny", "Wise", "Keen", "Neat", "Glad", "Deft", "Zany", "Spry"
        };

        /// <summary>Same list as the server's NOUNS.</summary>
        private static readonly string[] Nouns =
        {
            "Fox", "Owl", "Bear", "Wolf", "Deer", "Seal", "Lynx", "Puma",
            "Duck", "Crow", "Hawk", "Mole", "Toad", "Frog", "Hare", "Moth"
        };

        /// <summary>How many nouns there are.</summary>
        public static int NounCount => Nouns.Length;

        /// <summary>How many adjectives there are.</summary>
        public static int AdjectiveCount => Adjectives.Length;

        /// <summary>
        /// The pseudonym for a seed — <c>BoldFox4821</c> and the like.
        /// </summary>
        /// <remarks>
        /// Negative seeds are folded rather than rejected: a hash is as likely
        /// to be one as not. The digits run 1000–9999 like the server's, so
        /// the name is never shorter than a real one.
        /// </remarks>
        public static string From(int seed)
        {
            var spread = seed & int.MaxValue;
            var adjective = Adjectives[spread % Adjectives.Length];
            var noun = Nouns[spread / Adjectives.Length % Nouns.Length];
            var number = 1000 + spread / (Adjectives.Length * Nouns.Length) % 9000;
            return adjective + noun + number.ToString("0000");
        }
    }
}
