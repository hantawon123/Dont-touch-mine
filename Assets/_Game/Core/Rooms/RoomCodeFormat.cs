namespace Game.Core.Rooms
{
    /// <summary>
    /// The shape of a room code, for everyone who has to agree on it.
    /// </summary>
    /// <remarks>
    /// Lives here rather than beside the generator because presentation needs
    /// it too: a code entry field has to know how many characters to ask for
    /// and which ones it can be given, and it cannot reach the networking
    /// layer. Issuing codes stays with the generator; only the format is shared.
    /// </remarks>
    public static class RoomCodeFormat
    {
        /// <summary>
        /// Digits plus letters, minus the four that get misread: I and L look
        /// like 1, O looks like 0, and U is heard as You. Dropping the letters
        /// rather than the digits keeps every digit usable.
        /// </summary>
        public const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

        public const int CodeLength = 6;

        /// <summary>32^6, roughly 1.07 billion combinations.</summary>
        public static int AlphabetSize => Alphabet.Length;

        /// <summary>
        /// Puts a typed code into the form codes are issued in, so case and
        /// stray spaces do not turn a correct code into "room not found".
        /// </summary>
        public static string Normalize(string roomCode)
        {
            return string.IsNullOrWhiteSpace(roomCode)
                ? string.Empty
                : roomCode.Trim().ToUpperInvariant();
        }

        /// <summary>
        /// Whether a normalized string could have been issued as a code. Lets a
        /// typo be reported as a malformed code rather than as a room that does
        /// not exist, which are very different things to tell a player.
        /// </summary>
        public static bool IsWellFormed(string normalizedCode)
        {
            if (normalizedCode == null || normalizedCode.Length != CodeLength)
            {
                return false;
            }

            for (var index = 0; index < normalizedCode.Length; index++)
            {
                if (!IsAllowed(normalizedCode[index]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Whether one already-uppercased character can appear in a code.
        /// </summary>
        public static bool IsAllowed(char character) => Alphabet.IndexOf(character) >= 0;
    }
}
